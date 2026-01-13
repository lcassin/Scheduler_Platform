using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchedulerPlatform.API.Configuration;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Services
{
    public class StartupRecoveryService : IHostedService
    {
        private readonly ILogger<StartupRecoveryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly AppLifetimeInfo _appLifetime;
        private readonly StartupRecoverySettings _settings;
        private readonly IAdrOrchestrationQueue _orchestrationQueue;

        public StartupRecoveryService(
            ILogger<StartupRecoveryService> logger, 
            IServiceProvider serviceProvider,
            AppLifetimeInfo appLifetime,
            IOptions<SchedulerSettings> schedulerSettings,
            IAdrOrchestrationQueue orchestrationQueue)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _appLifetime = appLifetime;
            _settings = schedulerSettings.Value.StartupRecovery;
            _orchestrationQueue = orchestrationQueue;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                _logger.LogInformation("StartupRecoveryService: disabled via configuration");
                return;
            }

            try
            {
                if (_settings.DelaySeconds > 0)
                {
                    _logger.LogInformation("StartupRecoveryService: waiting {DelaySeconds} seconds before recovery to allow app to stabilize",
                        _settings.DelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_settings.DelaySeconds), cancellationToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

                var cutoffTime = _appLifetime.StartUtc.AddMinutes(-_settings.GracePeriodMinutes);
                var now = DateTime.UtcNow;

                _logger.LogInformation(
                    "StartupRecoveryService: starting recovery. AppStartUtc={AppStartUtc}, GracePeriodMinutes={GracePeriod}, CutoffTime={CutoffTime}",
                    _appLifetime.StartUtc, _settings.GracePeriodMinutes, cutoffTime);

                var affectedRows = await dbContext.JobExecutions
                    .Where(e => e.Status == JobStatus.Running 
                             && e.EndDateTime == null 
                             && e.StartDateTime < cutoffTime)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.Status, JobStatus.Failed)
                        .SetProperty(e => e.EndDateTime, now)
                        .SetProperty(e => e.ErrorMessage, "Application restarted while job was running; marked as Failed by startup recovery.")
                        .SetProperty(e => e.DurationSeconds, e => (int)EF.Functions.DateDiffSecond(e.StartDateTime, now)),
                        cancellationToken);

                if (affectedRows > 0)
                {
                    _logger.LogWarning(
                        "StartupRecoveryService: marked {AffectedRows} job execution(s) as Failed that were Running before cutoff time {CutoffTime}",
                        affectedRows, cutoffTime);
                }
                else
                {
                    _logger.LogInformation("StartupRecoveryService: no stuck Running executions found before cutoff time {CutoffTime}", cutoffTime);
                }

                await RecoverOrphanedOrchestrationsAsync(dbContext, now, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartupRecoveryService encountered an error during recovery");
            }
        }

        /// <summary>
        /// Recovers orphaned ADR orchestration runs that were left in "Running" status after an app restart.
        /// This marks the old run as failed and queues a new orchestration request if no orchestration is currently running.
        /// </summary>
        private async Task RecoverOrphanedOrchestrationsAsync(SchedulerDbContext dbContext, DateTime now, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("StartupRecoveryService: checking for orphaned ADR orchestration runs");

                var currentRun = _orchestrationQueue.GetCurrentRun();
                if (currentRun != null)
                {
                    _logger.LogInformation(
                        "StartupRecoveryService: orchestration {RequestId} is currently running, skipping orchestration recovery",
                        currentRun.RequestId);
                    return;
                }

                var orchestrationCutoff = now.AddMinutes(-30);
                var orphanedRuns = await dbContext.AdrOrchestrationRuns
                    .Where(r => !r.IsDeleted 
                             && r.Status == "Running" 
                             && r.StartedDateTime.HasValue 
                             && r.StartedDateTime < orchestrationCutoff 
                             && r.CompletedDateTime == null)
                    .OrderByDescending(r => r.StartedDateTime)
                    .ToListAsync(cancellationToken);

                if (!orphanedRuns.Any())
                {
                    _logger.LogInformation("StartupRecoveryService: no orphaned ADR orchestration runs found");
                    return;
                }

                _logger.LogWarning(
                    "StartupRecoveryService: found {Count} orphaned ADR orchestration run(s) to recover",
                    orphanedRuns.Count);

                var mostRecentOrphan = orphanedRuns.First();
                
                foreach (var orphan in orphanedRuns)
                {
                    var isRecoveryCandidate = orphan == mostRecentOrphan;
                    orphan.Status = "Failed";
                    orphan.CompletedDateTime = now;
                    orphan.ErrorMessage = isRecoveryCandidate
                        ? "Application restarted while orchestration was running. A new orchestration has been queued to continue processing."
                        : "Application restarted while orchestration was running. Marked as failed by startup recovery.";
                    orphan.ModifiedDateTime = now;
                    orphan.ModifiedBy = "StartupRecovery";
                    
                    _logger.LogWarning(
                        "StartupRecoveryService: marking orphaned orchestration {RequestId} (started {StartedAt}) as Failed",
                        orphan.RequestId, orphan.StartedDateTime);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                var recentRunsInLastHour = await dbContext.AdrOrchestrationRuns
                    .Where(r => !r.IsDeleted 
                             && r.Status == "Completed" 
                             && r.CompletedDateTime.HasValue 
                             && r.CompletedDateTime > now.AddHours(-1))
                    .AnyAsync(cancellationToken);

                if (recentRunsInLastHour)
                {
                    _logger.LogInformation(
                        "StartupRecoveryService: a completed orchestration run exists within the last hour, skipping re-queue to avoid duplicate processing");
                    return;
                }

                var newRequest = new AdrOrchestrationRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    RequestedBy = "StartupRecovery",
                    RequestedAt = now,
                    RunSync = true,
                    RunCreateJobs = true,
                    RunCredentialVerification = true,
                    RunScraping = true,
                    RunStatusCheck = true
                };

                await _orchestrationQueue.QueueAsync(newRequest, cancellationToken);

                _logger.LogWarning(
                    "StartupRecoveryService: queued new orchestration {NewRequestId} to recover from orphaned run {OldRequestId}",
                    newRequest.RequestId, mostRecentOrphan.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartupRecoveryService: error during ADR orchestration recovery");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
