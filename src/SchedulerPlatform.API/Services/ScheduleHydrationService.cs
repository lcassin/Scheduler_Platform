using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.API.Services;

public class ScheduleHydrationService : IHostedService
{
    private readonly ILogger<ScheduleHydrationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ScheduleHydrationService(
        ILogger<ScheduleHydrationService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ScheduleHydrationService: Starting schedule hydration...");
            
            await Task.Delay(TimeSpan.FromSeconds(7), cancellationToken);
            
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();

            var allSchedules = await unitOfWork.Schedules.GetAllAsync();
            var enabledSchedules = allSchedules.Where(s => s.IsEnabled).ToList();

            _logger.LogInformation(
                "ScheduleHydrationService: Found {TotalSchedules} total schedules, {EnabledSchedules} enabled",
                allSchedules.Count(), enabledSchedules.Count);

            if (!enabledSchedules.Any())
            {
                _logger.LogInformation("ScheduleHydrationService: No enabled schedules to hydrate");
                return;
            }

            int successCount = 0;
            int failureCount = 0;

            foreach (var schedule in enabledSchedules)
            {
                try
                {
                    if (schedule.NextRunTime == null || schedule.NextRunTime < DateTime.UtcNow)
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
                                    _logger.LogWarning(tzEx, "Invalid time zone {TimeZone} for schedule {ScheduleId}, using UTC", 
                                        schedule.TimeZone, schedule.Id);
                                }
                            }
                            
                            var nextOccurrence = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
                            if (nextOccurrence.HasValue)
                            {
                                schedule.NextRunTime = nextOccurrence.Value.UtcDateTime;
                                schedule.UpdatedAt = DateTime.UtcNow;
                                await unitOfWork.Schedules.UpdateAsync(schedule);
                                
                                _logger.LogDebug(
                                    "ScheduleHydrationService: Recomputed NextRunTime for schedule {ScheduleId} to {NextRunTime}",
                                    schedule.Id, schedule.NextRunTime.Value.ToString("o"));
                            }
                        }
                        catch (Exception cronEx)
                        {
                            _logger.LogWarning(cronEx, 
                                "ScheduleHydrationService: Could not recompute NextRunTime for schedule {ScheduleId} with cron {CronExpression}",
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
            
            await unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "ScheduleHydrationService: Hydration complete. Successfully scheduled {SuccessCount} jobs, {FailureCount} failures",
                successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScheduleHydrationService: Critical error during schedule hydration");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ScheduleHydrationService: Stopping");
        return Task.CompletedTask;
    }
}
