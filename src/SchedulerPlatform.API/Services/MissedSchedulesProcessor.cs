using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using SchedulerPlatform.API.Configuration;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Services;

public class MissedSchedulesProcessor : IHostedService
{
    private readonly ILogger<MissedSchedulesProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;
    private readonly SchedulerSettings _settings;

    public MissedSchedulesProcessor(
        ILogger<MissedSchedulesProcessor> logger,
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        IOptions<SchedulerSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _environment = environment;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_settings.MissedScheduleHandling.EnableAutoFire)
            {
                _logger.LogInformation("MissedSchedulesProcessor: Auto-fire is disabled in configuration");
                return;
            }

            if (_environment.IsDevelopment() && !_settings.MissedScheduleHandling.EnableInDevelopment)
            {
                _logger.LogInformation("MissedSchedulesProcessor: Auto-fire is disabled in Development environment");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

            _logger.LogInformation("MissedSchedulesProcessor: Starting missed schedule processing...");
            _logger.LogInformation("MissedSchedulesProcessor: Window = {WindowDays} days, Throttle = {ThrottlePerSecond}/sec",
                _settings.MissedScheduleHandling.MissedScheduleWindowDays,
                _settings.MissedScheduleHandling.ThrottlePerSecond);

            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();

            var windowStart = DateTime.UtcNow.AddDays(-_settings.MissedScheduleHandling.MissedScheduleWindowDays);
            var now = DateTime.UtcNow;

            var missedSchedules = await unitOfWork.Schedules.FindAsync(s =>
                s.IsEnabled &&
                !s.IsDeleted &&
                s.NextRunTime.HasValue &&
                s.NextRunTime.Value < now &&
                s.NextRunTime.Value >= windowStart);

            var missedList = missedSchedules.ToList();

            _logger.LogInformation(
                "MissedSchedulesProcessor: Found {MissedCount} schedules within window (last {WindowDays} days)",
                missedList.Count, _settings.MissedScheduleHandling.MissedScheduleWindowDays);

            if (!missedList.Any())
            {
                _logger.LogInformation("MissedSchedulesProcessor: No missed schedules to process");
                return;
            }

            int triggered = 0;
            int failed = 0;
            var delayBetweenTriggers = TimeSpan.FromMilliseconds(1000.0 / _settings.MissedScheduleHandling.ThrottlePerSecond);

            foreach (var schedule in missedList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("MissedSchedulesProcessor: Cancellation requested, stopping");
                    break;
                }

                try
                {
                    var jobKey = new JobKey($"Job_{schedule.Id}", $"Group_{schedule.ClientId}");

                    if (await scheduler.CheckExists(jobKey, cancellationToken))
                    {
                        var jobDataMap = new JobDataMap
                        {
                            { "ScheduleId", schedule.Id.ToString() },
                            { "TriggeredBy", "MissedSchedulesProcessor" }
                        };

                        await scheduler.TriggerJob(jobKey, jobDataMap, cancellationToken);
                        triggered++;

                        _logger.LogDebug(
                            "MissedSchedulesProcessor: Triggered missed schedule {ScheduleId} ({ScheduleName}), was due {MinutesLate} minutes ago",
                            schedule.Id, schedule.Name, (now - schedule.NextRunTime.Value).TotalMinutes);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "MissedSchedulesProcessor: Job not found for schedule {ScheduleId} ({ScheduleName})",
                            schedule.Id, schedule.Name);
                        failed++;
                    }

                    await Task.Delay(delayBetweenTriggers, cancellationToken);
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "MissedSchedulesProcessor: Failed to trigger schedule {ScheduleId} ({ScheduleName})",
                        schedule.Id, schedule.Name);
                }
            }

            _logger.LogInformation(
                "MissedSchedulesProcessor: Processing complete. Triggered {TriggeredCount} schedules, {FailedCount} failures",
                triggered, failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MissedSchedulesProcessor: Critical error during missed schedule processing");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MissedSchedulesProcessor: Stopping");
        return Task.CompletedTask;
    }
}
