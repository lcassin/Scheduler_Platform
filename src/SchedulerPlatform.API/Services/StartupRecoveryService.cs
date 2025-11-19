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

        public StartupRecoveryService(
            ILogger<StartupRecoveryService> logger, 
            IServiceProvider serviceProvider,
            AppLifetimeInfo appLifetime,
            IOptions<SchedulerSettings> schedulerSettings)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _appLifetime = appLifetime;
            _settings = schedulerSettings.Value.StartupRecovery;
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
                             && e.EndTime == null 
                             && e.StartTime < cutoffTime)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.Status, JobStatus.Failed)
                        .SetProperty(e => e.EndTime, now)
                        .SetProperty(e => e.ErrorMessage, "Application restarted while job was running; marked as Failed by startup recovery.")
                        .SetProperty(e => e.DurationSeconds, e => (int)EF.Functions.DateDiffSecond(e.StartTime, now)),
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartupRecoveryService encountered an error during recovery");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
