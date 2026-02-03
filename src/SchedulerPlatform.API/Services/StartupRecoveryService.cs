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
using SchedulerPlatform.Core.Domain.Interfaces;
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
        /// This marks the old run as interrupted and sends a notification to administrators.
        /// Orchestrations are NOT automatically restarted to prevent issues with cancelled runs being restarted
        /// and to ensure testing limits are consciously considered before restarting.
        /// </summary>
        private async Task RecoverOrphanedOrchestrationsAsync(SchedulerDbContext dbContext, DateTime now, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("StartupRecoveryService: checking for orphaned ADR orchestration runs");

                if (_orchestrationQueue.IsOrchestrationRunningInMemory())
                {
                    var currentRun = _orchestrationQueue.GetCurrentRun();
                    _logger.LogInformation(
                        "StartupRecoveryService: orchestration {RequestId} is currently running or queued in memory, skipping orchestration recovery",
                        currentRun?.RequestId ?? "unknown");
                    return;
                }
                
                // Any orchestration that started BEFORE the app started is orphaned (the app restarted while it was running)
                // We use the app start time as the cutoff, not a fixed 30-minute window
                var appStartCutoff = _appLifetime.StartUtc;
                
                // Check if there's an orchestration that started AFTER the app started (meaning it's legitimately running)
                var runningAfterAppStart = await dbContext.AdrOrchestrationRuns
                    .Where(r => !r.IsDeleted 
                             && r.Status == "Running" 
                             && r.StartedDateTime.HasValue 
                             && r.StartedDateTime >= appStartCutoff
                             && r.CompletedDateTime == null)
                    .AnyAsync(cancellationToken);
                    
                if (runningAfterAppStart)
                {
                    _logger.LogInformation(
                        "StartupRecoveryService: an orchestration started after app startup is running, skipping orchestration recovery");
                    return;
                }

                // Find orphaned runs - any orchestration that started BEFORE the app started and is still "Running"
                var orphanedRuns = await dbContext.AdrOrchestrationRuns
                    .Where(r => !r.IsDeleted 
                             && r.Status == "Running" 
                             && r.StartedDateTime.HasValue 
                             && r.StartedDateTime < appStartCutoff 
                             && r.CompletedDateTime == null)
                    .OrderByDescending(r => r.StartedDateTime)
                    .ToListAsync(cancellationToken);

                if (!orphanedRuns.Any())
                {
                    _logger.LogInformation("StartupRecoveryService: no orphaned ADR orchestration runs found");
                    return;
                }

                _logger.LogWarning(
                    "StartupRecoveryService: found {Count} orphaned ADR orchestration run(s)",
                    orphanedRuns.Count);

                foreach (var orphan in orphanedRuns)
                {
                    // Mark as "Interrupted" instead of "Failed" to distinguish from actual failures
                    orphan.Status = "Interrupted";
                    orphan.CompletedDateTime = now;
                    orphan.ErrorMessage = "Application restarted while orchestration was running. Please manually restart the orchestration if needed.";
                    orphan.ModifiedDateTime = now;
                    orphan.ModifiedBy = "StartupRecovery";
                    
                    _logger.LogWarning(
                        "StartupRecoveryService: marking orphaned orchestration {RequestId} (started {StartedAt}, requested by {RequestedBy}) as Interrupted",
                        orphan.RequestId, orphan.StartedDateTime, orphan.RequestedBy);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                // Send notification email instead of auto-restarting
                // This allows administrators to decide whether to restart (considering testing limits, etc.)
                var mostRecentOrphan = orphanedRuns.First();
                await SendOrchestrationInterruptedNotificationAsync(mostRecentOrphan, orphanedRuns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartupRecoveryService: error during ADR orchestration recovery");
            }
        }
        
        /// <summary>
        /// Sends a notification email when an orchestration was interrupted by an app restart.
        /// </summary>
        private async Task SendOrchestrationInterruptedNotificationAsync(
            SchedulerPlatform.Core.Domain.Entities.AdrOrchestrationRun orphanedRun, 
            int totalOrphanedCount)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                
                var errorMessage = $"The ADR orchestration was interrupted when the application restarted.\n\n" +
                    $"Request ID: {orphanedRun.RequestId}\n" +
                    $"Requested By: {orphanedRun.RequestedBy}\n" +
                    $"Started At: {orphanedRun.StartedDateTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                    $"Total Interrupted Runs: {totalOrphanedCount}\n\n" +
                    $"The orchestration has been marked as 'Interrupted' and was NOT automatically restarted.\n" +
                    $"Please manually restart the orchestration from the ADR Monitor page if needed.\n\n" +
                    $"Note: If this orchestration was intentionally cancelled, no action is required.";
                
                await emailService.SendOrchestrationFailureNotificationAsync(
                    "ADR Full Cycle",
                    orphanedRun.RequestId,
                    errorMessage,
                    null, // No stack trace for interruption
                    "Application Restart");
                    
                _logger.LogInformation(
                    "StartupRecoveryService: sent notification email for interrupted orchestration {RequestId}",
                    orphanedRun.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartupRecoveryService: failed to send notification email for interrupted orchestration");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
