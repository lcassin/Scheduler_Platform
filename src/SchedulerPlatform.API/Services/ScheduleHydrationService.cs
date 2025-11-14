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
