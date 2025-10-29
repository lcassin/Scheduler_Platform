using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Services;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.ScheduleSync;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting Schedule Sync process...");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Connection string not found");
            int batchSize = configuration.GetValue<int>("SyncSettings:BatchSize", 1000);
            string defaultTimeZone = configuration.GetValue<string>("SyncSettings:DefaultTimeZone", "Central Standard Time")
                ?? "Central Standard Time";

            var optionsBuilder = new DbContextOptionsBuilder<SchedulerDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            await using var dbContext = new SchedulerDbContext(optionsBuilder.Options);

            var syncGroups = await dbContext.ScheduleSyncSources
                .Where(s => !s.IsDeleted)
                .GroupBy(s => new { s.ClientId, s.Vendor, s.AccountNumber, s.ScheduleFrequency })
                .Select(g => new
                {
                    g.Key.ClientId,
                    g.Key.Vendor,
                    g.Key.AccountNumber,
                    g.Key.ScheduleFrequency,
                    EarliestDate = g.Min(s => s.ScheduleDate),
                    RecordCount = g.Count()
                })
                .ToListAsync();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {syncGroups.Count} unique schedule groups to process");

            int created = 0;
            int updated = 0;
            int errors = 0;

            foreach (var group in syncGroups)
            {
                try
                {
                    string vendorTrimmed = group.Vendor.Trim();
                    string accountTrimmed = group.AccountNumber.Trim();
                    string scheduleName = $"{vendorTrimmed}_{accountTrimmed}";

                    var existingSchedule = await dbContext.Schedules
                        .Where(s => s.ClientId == group.ClientId && s.Name.Contains(scheduleName))
                        .FirstOrDefaultAsync();

                    string cronExpression = CronExpressionGenerator.GenerateFromFrequency(
                        group.ScheduleFrequency, 
                        group.EarliestDate);

                    DateTime? nextRunTime = CalculateNextRunTime(cronExpression, defaultTimeZone);

                    if (existingSchedule != null)
                    {
                        existingSchedule.CronExpression = cronExpression;
                        existingSchedule.NextRunTime = nextRunTime;
                        existingSchedule.Frequency = group.ScheduleFrequency;
                        existingSchedule.UpdatedAt = DateTime.UtcNow;
                        existingSchedule.UpdatedBy = "ScheduleSync";

                        dbContext.Schedules.Update(existingSchedule);
                        updated++;

                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Updated schedule: {scheduleName} (ID: {existingSchedule.Id})");
                    }
                    else
                    {
                        var newSchedule = new Schedule
                        {
                            Name = scheduleName,
                            Description = $"Auto-synced schedule for {vendorTrimmed} - Account {accountTrimmed} ({group.RecordCount} records)",
                            ClientId = group.ClientId,
                            JobType = JobType.StoredProcedure,
                            Frequency = group.ScheduleFrequency,
                            CronExpression = cronExpression,
                            NextRunTime = nextRunTime,
                            IsEnabled = true,
                            MaxRetries = 3,
                            RetryDelayMinutes = 5,
                            TimeZone = defaultTimeZone,
                            JobConfiguration = "{\"ConnectionString\":\"\",\"ProcedureName\":\"TBD\",\"TimeoutSeconds\":300}",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "ScheduleSync",
                            IsDeleted = false
                        };

                        await dbContext.Schedules.AddAsync(newSchedule);
                        created++;

                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Created schedule: {scheduleName}");
                    }

                    if ((created + updated) % batchSize == 0)
                    {
                        await dbContext.SaveChangesAsync();
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved batch (Total: {created + updated})");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing group (Client: {group.ClientId}, Vendor: {group.Vendor}, Account: {group.AccountNumber}): {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule Sync completed successfully");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules created: {created}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules updated: {updated}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Errors: {errors}");

            return errors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fatal error: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static DateTime? CalculateNextRunTime(string cronExpression, string timeZone)
    {
        try
        {
            var trigger = TriggerBuilder.Create()
                .WithCronSchedule(cronExpression, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZone)))
                .Build();
            return trigger.GetNextFireTimeUtc()?.DateTime;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Could not calculate NextRunTime for CRON {cronExpression}: {ex.Message}");
            return null;
        }
    }
}
