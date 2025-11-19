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
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule staggering: 4:00 AM - midnight (72,000 possible time slots)");

        try
        {
            var syncGroups = await _dbContext.ScheduleSyncSources
                .Where(s => !s.IsDeleted && s.LastSyncedAt >= syncRunStart)
                .GroupBy(s => new { s.ClientName, s.ExternalVendorId, s.VendorName, s.AccountNumber, s.ScheduleFrequency })
                .Select(g => new
                {
                    g.Key.ClientName,
                    g.Key.ExternalVendorId,
                    g.Key.VendorName,
                    g.Key.AccountNumber,
                    g.Key.ScheduleFrequency,
                    EarliestDate = g.Min(s => s.LastInvoiceDate),
                    RecordCount = g.Count()
                })
                .ToListAsync();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {syncGroups.Count:N0} unique schedule groups to process");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Preloading clients into memory...");
            var uniqueClientNames = syncGroups
                .Select(g => g.ClientName?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();
            var clientMap = new Dictionary<string, (int ClientId, string ClientName)>();
            
            const int clientChunkSize = 2000;
            for (int i = 0; i < uniqueClientNames.Count; i += clientChunkSize)
            {
                var chunk = uniqueClientNames.Skip(i).Take(clientChunkSize).ToList();
                var clients = await _dbContext.Clients
                    .Where(c => chunk.Contains(c.ClientName))
                    .Select(c => new { c.Id, c.ClientName })
                    .ToListAsync();
                
                foreach (var client in clients)
                {
                    clientMap[client.ClientName] = (client.Id, client.ClientName);
                }
            }
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Loaded {clientMap.Count:N0} clients into memory");

            int created = 0;
            int updated = 0;
            int unchanged = 0;
            int errors = 0;
            int processed = 0;
            
            const int chunkSize = 5000;
            var totalGroups = syncGroups.Count;

            for (int chunkStart = 0; chunkStart < totalGroups; chunkStart += chunkSize)
            {
                var chunk = syncGroups.Skip(chunkStart).Take(chunkSize).ToList();
                
                var clientNamesInChunk = chunk
                    .Select(g => g.ClientName)
                    .Where(n => !string.IsNullOrWhiteSpace(n) && clientMap.ContainsKey(n))
                    .Distinct()
                    .ToList();
                
                var clientIdsInChunk = clientNamesInChunk
                    .Select(n => clientMap[n].ClientId)
                    .ToList();
                
                var existingSchedules = await _dbContext.Schedules
                    .Where(s => clientIdsInChunk.Contains(s.ClientId))
                    .Select(s => new { s.Id, s.ClientId, s.Name, s.CronExpression, s.Frequency, s.NextRunTime })
                    .ToListAsync();
                
                var scheduleMap = existingSchedules
                    .GroupBy(s => (s.ClientId, s.Name))
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var group in chunk)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(group.ClientName) || !clientMap.TryGetValue(group.ClientName, out var clientInfo))
                        {
                            errors++;
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Client not found for ClientName '{group.ClientName}'");
                            continue;
                        }

                        string vendorName = group.VendorName?.Trim() ?? $"Vendor{group.ExternalVendorId}";
                        string accountTrimmed = group.AccountNumber.Trim();
                        string scheduleName = $"{vendorName}_{accountTrimmed}";

                        string stableKey = $"{clientInfo.ClientId}|{scheduleName}";
                        string cronExpression = CronExpressionGenerator.GenerateFromFrequency(
                            (ScheduleFrequency)group.ScheduleFrequency,
                            stableKey,
                            startHour: 4,
                            endHour: 24,
                            referenceDate: group.EarliestDate);

                        DateTime? nextRunTime = CalculateNextRunTime(cronExpression, _defaultTimeZone);

                        if (scheduleMap.TryGetValue((clientInfo.ClientId, scheduleName), out var existingSchedule))
                        {
                            bool cronChanged = existingSchedule.CronExpression != cronExpression;
                            bool frequencyChanged = existingSchedule.Frequency != (ScheduleFrequency)group.ScheduleFrequency;
                            bool nextRunChanged = existingSchedule.NextRunTime != nextRunTime;

                            if (cronChanged || frequencyChanged || nextRunChanged)
                            {
                                var scheduleToUpdate = await _dbContext.Schedules.FindAsync(existingSchedule.Id);
                                if (scheduleToUpdate != null)
                                {
                                    scheduleToUpdate.CronExpression = cronExpression;
                                    scheduleToUpdate.NextRunTime = nextRunTime;
                                    scheduleToUpdate.Frequency = (ScheduleFrequency)group.ScheduleFrequency;
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
                        else
                        {
                            var newSchedule = new Schedule
                            {
                                Name = scheduleName,
                                Description = $"Auto-synced schedule for {vendorName} - Account {accountTrimmed} ({group.RecordCount} records)",
                                ClientId = clientInfo.ClientId,
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
                        }

                        processed++;
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing group (Client: {group.ClientName}, Vendor: {group.ExternalVendorId}, Account: {group.AccountNumber}): {ex.Message}");
                    }
                }

                await _dbContext.SaveChangesAsync();
                
                var elapsedTime = DateTime.UtcNow - result.StartTime;
                var groupsRemaining = totalGroups - processed;
                
                if (elapsedTime.TotalSeconds > 0 && processed > 0)
                {
                    var groupsPerSecond = processed / elapsedTime.TotalSeconds;
                    var estimatedSecondsRemaining = groupsRemaining / groupsPerSecond;
                    var estimatedTimeRemaining = TimeSpan.FromSeconds(estimatedSecondsRemaining);
                    
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Progress: {processed:N0}/{totalGroups:N0} ({(processed * 100.0 / totalGroups):F1}%) | Created: {created:N0}, Updated: {updated:N0}, Unchanged: {unchanged:N0}, Errors: {errors:N0}");
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Rate: {groupsPerSecond:F1} groups/s | Elapsed: {FormatTimeSpan(elapsedTime)} | ETA: {FormatTimeSpan(estimatedTimeRemaining)}");
                }
            }

            result.Created = created;
            result.Updated = updated;
            result.Unchanged = unchanged;
            result.Errors = errors;
            result.EndTime = DateTime.UtcNow;
            result.Success = errors == 0;

            var totalDuration = result.EndTime - result.StartTime;
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SCHEDULE GENERATION SUMMARY");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total groups processed: {processed:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules created: {created:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules updated: {updated:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedules unchanged: {unchanged:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Errors: {errors:N0}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Duration: {FormatTimeSpan(totalDuration)}");
            if (totalDuration.TotalSeconds > 0)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Average rate: {processed / totalDuration.TotalSeconds:F1} groups/sec");
            }
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");

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

public class ScheduleGenerationResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Errors { get; set; }
    public string? ErrorMessage { get; set; }
}
