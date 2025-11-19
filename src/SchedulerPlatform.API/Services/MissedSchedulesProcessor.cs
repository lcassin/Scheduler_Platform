using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using SchedulerPlatform.API.Configuration;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.API.Services;

public class MissedSchedulesProcessor : IHostedService
{
    private readonly ILogger<MissedSchedulesProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SchedulerSettings _settings;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    public MissedSchedulesProcessor(
        ILogger<MissedSchedulesProcessor> logger,
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        IHostApplicationLifetime lifetime,
        IOptions<SchedulerSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _environment = environment;
        _lifetime = lifetime;
        _settings = settings.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.MissedScheduleHandling.EnableAutoFire)
        {
            _logger.LogInformation("MissedSchedulesProcessor: Auto-fire is disabled in configuration");
            return Task.CompletedTask;
        }

        if (_environment.IsDevelopment() && !_settings.MissedScheduleHandling.EnableInDevelopment)
        {
            _logger.LogInformation("MissedSchedulesProcessor: Auto-fire is disabled in Development environment");
            return Task.CompletedTask;
        }

        _logger.LogInformation("MissedSchedulesProcessor: Service registered, will start after application startup");
        
        _lifetime.ApplicationStarted.Register(() =>
        {
            _cts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessMissedSchedulesAsync(_cts.Token), _cts.Token);
        });

        return Task.CompletedTask;
    }

    private async Task ProcessMissedSchedulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

            _logger.LogInformation("MissedSchedulesProcessor: Starting missed schedule processing in background...");
            _logger.LogInformation("MissedSchedulesProcessor: Window = {WindowDays} days, Throttle = {ThrottlePerSecond}/sec",
                _settings.MissedScheduleHandling.MissedScheduleWindowDays,
                _settings.MissedScheduleHandling.ThrottlePerSecond);

            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();

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
            int scheduled = 0;
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

                    if (!await scheduler.CheckExists(jobKey, cancellationToken))
                    {
                        _logger.LogDebug(
                            "MissedSchedulesProcessor: Job not in Quartz for schedule {ScheduleId} ({ScheduleName}), scheduling it now",
                            schedule.Id, schedule.Name);
                        
                        await schedulerService.ScheduleJob(schedule);
                        scheduled++;
                    }

                    var jobDataMap = new JobDataMap
                    {
                        { "ScheduleId", schedule.Id.ToString() },
                        { "TriggeredBy", "MissedSchedulesProcessor" }
                    };

                    await scheduler.TriggerJob(jobKey, jobDataMap, cancellationToken);
                    triggered++;

                    _logger.LogDebug(
                        "MissedSchedulesProcessor: Triggered missed schedule {ScheduleId} ({ScheduleName}), was due {MinutesLate:F1} minutes ago",
                        schedule.Id, schedule.Name, (now - schedule.NextRunTime.Value).TotalMinutes);

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
                "MissedSchedulesProcessor: Processing complete. Scheduled {ScheduledCount} jobs, triggered {TriggeredCount} schedules, {FailedCount} failures",
                scheduled, triggered, failed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("MissedSchedulesProcessor: Processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MissedSchedulesProcessor: Critical error during missed schedule processing");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MissedSchedulesProcessor: Stopping");
        
        _cts?.Cancel();
        
        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        _cts?.Dispose();
    }
}
