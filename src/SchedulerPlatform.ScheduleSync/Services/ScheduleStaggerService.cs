using Microsoft.EntityFrameworkCore;
using Quartz;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Services;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.ScheduleSync.Services;

public class ScheduleStaggerService
{
    private readonly SchedulerDbContext _dbContext;
    private readonly int _batchSize;

    public ScheduleStaggerService(SchedulerDbContext dbContext, int batchSize = 10000)
    {
        _dbContext = dbContext;
        _batchSize = batchSize;
    }

    public async Task<StaggerResult> StaggerExistingSchedulesAsync(int startHour = 4, int endHour = 24)
    {
        var result = new StaggerResult { StartTime = DateTime.UtcNow };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STAGGER EXISTING SCHEDULES");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Time window: {startHour:D2}:00 - {(endHour == 24 ? "midnight" : $"{endHour:D2}:00")}");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Batch size: {_batchSize:N0}");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");

        try
        {
            var totalSchedules = await _dbContext.Schedules
                .Where(s => s.CreatedBy == "ScheduleSync" && !s.IsDeleted && s.IsEnabled)
                .CountAsync();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {totalSchedules:N0} enabled schedules to process");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");

            if (totalSchedules == 0)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No schedules to stagger.");
                result.EndTime = DateTime.UtcNow;
                result.Success = true;
                return result;
            }

            int updated = 0;
            int unchanged = 0;
            int errors = 0;
            int lastId = 0;

            while (true)
            {
                var batch = await _dbContext.Schedules
                    .Where(s => s.Id > lastId && s.CreatedBy == "ScheduleSync" && !s.IsDeleted && s.IsEnabled)
                    .OrderBy(s => s.Id)
                    .Take(_batchSize)
                    .Select(s => new
                    {
                        s.Id,
                        s.ClientId,
                        s.Name,
                        s.Frequency,
                        s.TimeZone,
                        s.CronExpression,
                        s.NextRunTime
                    })
                    .ToListAsync();

                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var schedule in batch)
                {
                    try
                    {
                        string stableKey = $"{schedule.ClientId}|{schedule.Name}";
                        string newCronExpression = CronExpressionGenerator.GenerateFromFrequency(
                            schedule.Frequency,
                            stableKey,
                            startHour,
                            endHour);

                        bool cronChanged = newCronExpression != schedule.CronExpression;
                        bool needsNextRunTime = schedule.NextRunTime == null;

                        if (cronChanged || needsNextRunTime)
                        {
                            DateTime? newNextRunTime = CalculateNextRunTime(
                                cronChanged ? newCronExpression : schedule.CronExpression, 
                                schedule.TimeZone);

                            var scheduleToUpdate = await _dbContext.Schedules.FindAsync(schedule.Id);
                            if (scheduleToUpdate != null)
                            {
                                if (cronChanged)
                                {
                                    scheduleToUpdate.CronExpression = newCronExpression;
                                }
                                scheduleToUpdate.NextRunTime = newNextRunTime;
                                scheduleToUpdate.UpdatedAt = DateTime.UtcNow;
                                scheduleToUpdate.UpdatedBy = "ScheduleSync";
                                updated++;
                            }
                        }
                        else
                        {
                            unchanged++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing schedule {schedule.Id} ({schedule.Name}): {ex.Message}");
                    }
                }

                await _dbContext.SaveChangesAsync();

                lastId = batch.Max(s => s.Id);
                int processed = updated + unchanged + errors;

                var elapsedTime = DateTime.UtcNow - result.StartTime;
                var schedulesRemaining = totalSchedules - processed;

                if (elapsedTime.TotalSeconds > 0 && processed > 0)
                {
                    var schedulesPerSecond = processed / elapsedTime.TotalSeconds;
                    var estimatedSecondsRemaining = schedulesRemaining / schedulesPerSecond;
                    var estimatedTimeRemaining = TimeSpan.FromSeconds(estimatedSecondsRemaining);

                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Progress: {processed:N0}/{totalSchedules:N0} ({(processed * 100.0 / totalSchedules):F1}%) | Updated: {updated:N0}, Unchanged: {unchanged:N0}, Errors: {errors:N0}");
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Rate: {schedulesPerSecond:F1} schedules/s | Elapsed: {FormatTimeSpan(elapsedTime)} | ETA: {FormatTimeSpan(estimatedTimeRemaining)}");
                }

                if (batch.Count < _batchSize)
                {
                    break;
                }
            }

            result.Updated = updated;
            result.Unchanged = unchanged;
            result.Errors = errors;
            result.EndTime = DateTime.UtcNow;
            result.Success = errors == 0;

            var totalDuration = result.EndTime - result.StartTime;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STAGGER SUMMARY");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total schedules processed: {(updated + unchanged + errors):N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules updated: {updated:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules unchanged: {unchanged:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Errors: {errors:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Duration: {FormatTimeSpan(totalDuration)}");
            if (totalDuration.TotalSeconds > 0)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Average rate: {(updated + unchanged + errors) / totalDuration.TotalSeconds:F1} schedules/sec");
            }
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stagger operation failed: {ex.Message}");
            throw;
        }
    }

    private static DateTime? CalculateNextRunTime(string cronExpression, string timeZone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            var cron = new CronExpression(cronExpression) { TimeZone = tz };
            return cron.GetNextValidTimeAfter(DateTimeOffset.UtcNow)?.UtcDateTime;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Could not calculate NextRunTime for CRON '{cronExpression}' with TimeZone '{timeZone}': {ex.Message}");
            return null;
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
        }
        else if (ts.TotalMinutes >= 1)
        {
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
        else
        {
            return $"{ts.Seconds}s";
        }
    }
}

public class StaggerResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Errors { get; set; }
    public string? ErrorMessage { get; set; }
}
