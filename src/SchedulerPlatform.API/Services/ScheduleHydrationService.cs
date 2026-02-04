using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchedulerPlatform.API.Configuration;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.API.Services;

public class ScheduleHydrationService : IHostedService
{
    private readonly ILogger<ScheduleHydrationService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SchedulerSettings _settings;
    private Task? _hydrationTask;
    private CancellationTokenSource? _cts;

    public ScheduleHydrationService(
        ILogger<ScheduleHydrationService> logger,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime lifetime,
        IOptions<SchedulerSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _lifetime = lifetime;
        _settings = settings.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Hydration.Enabled)
        {
            _logger.LogInformation("ScheduleHydrationService: Disabled via configuration");
            return Task.CompletedTask;
        }

        _logger.LogInformation("ScheduleHydrationService: Service registered, will start after application startup");
        
        _lifetime.ApplicationStarted.Register(() =>
        {
            _cts = new CancellationTokenSource();
            _hydrationTask = Task.Run(() => HydrateSchedulesAsync(_cts.Token), _cts.Token);
        });

        return Task.CompletedTask;
    }

    private async Task HydrateSchedulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ScheduleHydrationService: Starting schedule hydration in background...");
            
            await Task.Delay(TimeSpan.FromSeconds(_settings.Hydration.DelaySeconds), cancellationToken);
            
            var now = DateTime.UtcNow;
            
            _logger.LogInformation(
                "ScheduleHydrationService: Loading ALL enabled schedules to ensure Quartz registration");

            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();

            // Load ALL enabled schedules to ensure they are registered with Quartz
            // This is critical because Quartz persistent store may not have all jobs after deployment
            // Missed schedules (NextRunDateTime < now) are handled by MissedSchedulesProcessor
            var allEnabledSchedules = await unitOfWork.Schedules.FindAsync(s =>
                s.IsEnabled &&
                !s.IsDeleted &&
                !string.IsNullOrEmpty(s.CronExpression));

            var schedulesList = allEnabledSchedules.ToList();
            
            _logger.LogInformation(
                "ScheduleHydrationService: Found {Count} enabled schedules to register with Quartz",
                schedulesList.Count);

            if (!schedulesList.Any())
            {
                _logger.LogInformation("ScheduleHydrationService: No enabled schedules found to hydrate");
                return;
            }

            int successCount = 0;
            int failureCount = 0;
            int recomputedCount = 0;
            int batchNumber = 0;
            var batchSize = _settings.Hydration.BatchSize;

            for (int i = 0; i < schedulesList.Count; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("ScheduleHydrationService: Hydration cancelled");
                    break;
                }

                batchNumber++;
                var batch = schedulesList.Skip(i).Take(batchSize).ToList();
                var totalBatches = (int)Math.Ceiling((double)schedulesList.Count / batchSize);

                _logger.LogInformation(
                    "ScheduleHydrationService: Processing batch {BatchNumber}/{TotalBatches} ({BatchSize} schedules)",
                    batchNumber, totalBatches, batch.Count);

                var batchRecomputedCount = 0;

                foreach (var schedule in batch)
                {
                    try
                    {
                        if (schedule.NextRunDateTime == null || schedule.NextRunDateTime < now)
                        {
                            try
                            {
                                var cronExpression = new Quartz.CronExpression(schedule.CronExpression);
                                
                                if (!string.IsNullOrWhiteSpace(schedule.TimeZone))
                                {
                                    try
                                    {
                                        cronExpression.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone);
                                    }
                                    catch (Exception tzEx)
                                    {
                                        _logger.LogWarning(tzEx, 
                                            "Invalid time zone {TimeZone} for schedule {ScheduleId}, using UTC", 
                                            schedule.TimeZone, schedule.Id);
                                    }
                                }
                                
                                var nextOccurrence = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
                                if (nextOccurrence.HasValue)
                                {
                                    schedule.NextRunDateTime = nextOccurrence.Value.UtcDateTime;
                                    schedule.ModifiedDateTime = DateTime.UtcNow;
                                    await unitOfWork.Schedules.UpdateAsync(schedule);
                                    batchRecomputedCount++;
                                    
                                    _logger.LogDebug(
                                        "ScheduleHydrationService: Recomputed NextRunDateTime for schedule {ScheduleId} to {NextRunDateTime}",
                                        schedule.Id, schedule.NextRunDateTime.Value.ToString("o"));
                                }
                            }
                            catch (Exception cronEx)
                            {
                                _logger.LogWarning(cronEx, 
                                    "ScheduleHydrationService: Could not recompute NextRunDateTime for schedule {ScheduleId} with cron {CronExpression}",
                                    schedule.Id, schedule.CronExpression);
                            }
                        }
                        
                        await schedulerService.ScheduleJob(schedule);
                        successCount++;
                        
                        _logger.LogDebug(
                            "ScheduleHydrationService: Scheduled job for schedule {ScheduleId} ({ScheduleName})",
                            schedule.Id, schedule.Name);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex,
                            "ScheduleHydrationService: Failed to schedule job for schedule {ScheduleId} ({ScheduleName})",
                            schedule.Id, schedule.Name);
                    }
                }

                if (batchRecomputedCount > 0)
                {
                    await unitOfWork.SaveChangesAsync();
                    recomputedCount += batchRecomputedCount;
                    _logger.LogInformation(
                        "ScheduleHydrationService: Batch {BatchNumber}: Recomputed {Count} NextRunTime values",
                        batchNumber, batchRecomputedCount);
                }

                _logger.LogInformation(
                    "ScheduleHydrationService: Batch {BatchNumber}/{TotalBatches} complete. Progress: {Success} scheduled, {Failures} failures",
                    batchNumber, totalBatches, successCount, failureCount);

                if (i + batchSize < schedulesList.Count)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            _logger.LogInformation(
                "ScheduleHydrationService: Hydration complete. Successfully scheduled {SuccessCount} jobs, recomputed {RecomputedCount} NextRunTime values, {FailureCount} failures",
                successCount, recomputedCount, failureCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ScheduleHydrationService: Hydration cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScheduleHydrationService: Critical error during schedule hydration");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ScheduleHydrationService: Stopping");
        
        _cts?.Cancel();
        
        if (_hydrationTask != null)
        {
            try
            {
                await _hydrationTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        _cts?.Dispose();
    }
}
