using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.API.Services;

/// <summary>
/// Seeds system schedules on application startup.
/// Creates required system schedules if they don't exist.
/// </summary>
public class SystemScheduleSeeder : IHostedService
{
    private readonly ILogger<SystemScheduleSeeder> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SystemScheduleSeeder(
        ILogger<SystemScheduleSeeder> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("SystemScheduleSeeder: checking for required system schedules...");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();

            await SeedMaintenanceScheduleAsync(dbContext, schedulerService, cancellationToken);

            _logger.LogInformation("SystemScheduleSeeder: completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemScheduleSeeder encountered an error during seeding");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedMaintenanceScheduleAsync(
        SchedulerDbContext dbContext, 
        ISchedulerService schedulerService,
        CancellationToken cancellationToken)
    {
        const string maintenanceScheduleName = "System Maintenance";

        var existingSchedule = await dbContext.Schedules
            .FirstOrDefaultAsync(s => s.Name == maintenanceScheduleName && s.IsSystemSchedule && !s.IsDeleted, cancellationToken);

        if (existingSchedule != null)
        {
            _logger.LogInformation("SystemScheduleSeeder: Maintenance schedule already exists (Id: {ScheduleId})", existingSchedule.Id);
            return;
        }

        var systemClient = await dbContext.Clients
            .FirstOrDefaultAsync(c => c.ClientName == "System" && !c.IsDeleted, cancellationToken);

        if (systemClient == null)
        {
            systemClient = await dbContext.Clients
                .FirstOrDefaultAsync(c => !c.IsDeleted, cancellationToken);

            if (systemClient == null)
            {
                _logger.LogWarning("SystemScheduleSeeder: No client found to assign maintenance schedule. Skipping seeding.");
                return;
            }
        }

        var now = DateTime.UtcNow;
        var maintenanceSchedule = new Schedule
        {
            Name = maintenanceScheduleName,
            Description = "System maintenance job that archives old data, purges old archives, and cleans up log files. Runs daily at 2 AM UTC.",
            ClientId = systemClient.Id,
            JobType = JobType.Maintenance,
            Frequency = ScheduleFrequency.Daily,
            CronExpression = "0 0 2 * * ?",
            IsEnabled = true,
            IsSystemSchedule = true,
            MaxRetries = 3,
            RetryDelayMinutes = 30,
            TimeoutMinutes = 60,
            TimeZone = "UTC",
            JobConfiguration = "{}",
            CreatedDateTime = now,
            CreatedBy = "System",
            ModifiedDateTime = now,
            ModifiedBy = "System"
        };

        try
        {
            var cronExpression = new Quartz.CronExpression(maintenanceSchedule.CronExpression);
            var nextOccurrence = cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
            maintenanceSchedule.NextRunDateTime = nextOccurrence?.UtcDateTime;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SystemScheduleSeeder: Could not calculate NextRunDateTime for maintenance schedule");
        }

        dbContext.Schedules.Add(maintenanceSchedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SystemScheduleSeeder: Created maintenance schedule (Id: {ScheduleId}, NextRun: {NextRun})",
            maintenanceSchedule.Id, maintenanceSchedule.NextRunDateTime);

        try
        {
            await schedulerService.ScheduleJob(maintenanceSchedule);
            _logger.LogInformation("SystemScheduleSeeder: Registered maintenance schedule with Quartz scheduler");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemScheduleSeeder: Failed to register maintenance schedule with Quartz scheduler");
        }
    }
}
