using Microsoft.EntityFrameworkCore;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Services;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.ScheduleSync.Services;

public class ScheduleGenerationService
{
    private readonly SchedulerDbContext _dbContext;
    private readonly int _batchSize;
    private readonly string _defaultTimeZone;

    public ScheduleGenerationService(SchedulerDbContext dbContext, int batchSize, string defaultTimeZone)
    {
        _dbContext = dbContext;
        _batchSize = batchSize;
        _defaultTimeZone = defaultTimeZone;
    }

    public async Task<ScheduleGenerationResult> GenerateSchedulesAsync(DateTime syncRunStart)
    {
        var result = new ScheduleGenerationResult { StartTime = DateTime.UtcNow };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting schedule generation from synced data...");

        try
        {
            var syncGroups = await _dbContext.ScheduleSyncSources
                .Where(s => !s.IsDeleted && s.LastSyncedAt >= syncRunStart)
                .GroupBy(s => new { s.ExternalClientId, s.ExternalVendorId, s.VendorName, s.AccountNumber, s.ScheduleFrequency })
                .Select(g => new
                {
                    g.Key.ExternalClientId,
                    g.Key.ExternalVendorId,
                    g.Key.VendorName,
                    g.Key.AccountNumber,
                    g.Key.ScheduleFrequency,
                    EarliestDate = g.Min(s => s.LastInvoiceDate),
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
                    string vendorName = group.VendorName?.Trim() ?? $"Vendor{group.ExternalVendorId}";
                    string accountTrimmed = group.AccountNumber.Trim();
                    string scheduleName = $"{vendorName}_{accountTrimmed}";

                    var client = await _dbContext.Clients
                        .Where(c => c.ExternalClientId == group.ExternalClientId)
                        .FirstOrDefaultAsync();

                    if (client == null)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Client not found for ExternalClientId {group.ExternalClientId}. Skipping schedule creation.");
                        errors++;
                        continue;
                    }

                    var existingSchedule = await _dbContext.Schedules
                        .Where(s => s.ClientId == client.Id && s.Name.Contains(scheduleName))
                        .FirstOrDefaultAsync();

                    string cronExpression = CronExpressionGenerator.GenerateFromFrequency(
                        (ScheduleFrequency)group.ScheduleFrequency,
                        group.EarliestDate);

                    DateTime? nextRunTime = CalculateNextRunTime(cronExpression, _defaultTimeZone);

                    if (existingSchedule != null)
                    {
                        existingSchedule.CronExpression = cronExpression;
                        existingSchedule.NextRunTime = nextRunTime;
                        existingSchedule.Frequency = (ScheduleFrequency)group.ScheduleFrequency;
                        existingSchedule.UpdatedAt = DateTime.UtcNow;
                        existingSchedule.UpdatedBy = "ScheduleSync";

                        _dbContext.Schedules.Update(existingSchedule);
                        updated++;

                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Updated schedule: {scheduleName} (ID: {existingSchedule.Id})");
                    }
                    else
                    {
                        var newSchedule = new Schedule
                        {
                            Name = scheduleName,
                            Description = $"Auto-synced schedule for {vendorName} - Account {accountTrimmed} ({group.RecordCount} records)",
                            ClientId = client.Id,
                            JobType = JobType.StoredProcedure,
                            Frequency = (ScheduleFrequency)group.ScheduleFrequency,
                            CronExpression = cronExpression,
                            NextRunTime = nextRunTime,
                            IsEnabled = true,
                            MaxRetries = 3,
                            RetryDelayMinutes = 5,
                            TimeZone = _defaultTimeZone,
                            JobConfiguration = "{\"ConnectionString\":\"\",\"ProcedureName\":\"TBD\",\"TimeoutSeconds\":300}",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "ScheduleSync",
                            IsDeleted = false
                        };

                        await _dbContext.Schedules.AddAsync(newSchedule);
                        created++;

                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Created schedule: {scheduleName}");
                    }

                    if ((created + updated) % _batchSize == 0)
                    {
                        await _dbContext.SaveChangesAsync();
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved batch (Total: {created + updated})");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing group (Client: {group.ExternalClientId}, Vendor: {group.ExternalVendorId}, Account: {group.AccountNumber}): {ex.Message}");
                }
            }

            await _dbContext.SaveChangesAsync();

            result.Created = created;
            result.Updated = updated;
            result.Errors = errors;
            result.EndTime = DateTime.UtcNow;
            result.Success = errors == 0;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule generation completed");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules created: {created}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules updated: {updated}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Errors: {errors}");

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule generation failed: {ex.Message}");
            throw;
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

public class ScheduleGenerationResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Errors { get; set; }
    public string? ErrorMessage { get; set; }
}
