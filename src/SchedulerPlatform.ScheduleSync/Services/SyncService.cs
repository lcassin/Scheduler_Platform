using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.ScheduleSync.Models;

namespace SchedulerPlatform.ScheduleSync.Services;

public class SyncService
{
    private readonly SchedulerDbContext _dbContext;
    private readonly AccountsApiClient _apiClient;
    private readonly int _saveBatchSize;

    public SyncService(SchedulerDbContext dbContext, AccountsApiClient apiClient, int saveBatchSize = 5000)
    {
        _dbContext = dbContext;
        _apiClient = apiClient;
        _saveBatchSize = saveBatchSize;
    }

    public async Task<SyncResult> RunSyncAsync(bool includeOnlyTandemAccounts = false)
    {
        var runStart = DateTime.UtcNow;
        var result = new SyncResult { StartTime = runStart };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting account sync process...");
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Run timestamp: {runStart:yyyy-MM-dd HH:mm:ss} UTC");

        try
        {
            var processedCount = 0;
            var expectedTotal = 0;
            var batchCount = 0;

            await foreach (var page in _apiClient.GetAllAccountsAsync(includeOnlyTandemAccounts))
            {
                if (expectedTotal == 0)
                {
                    expectedTotal = page.Total;
                }

                var accountIds = page.Data.Select(a => a.AccountId).ToList();
                
                var existing = await _dbContext.ScheduleSyncSources
                    .Where(s => accountIds.Contains(s.AccountId))
                    .ToDictionaryAsync(s => s.AccountId);

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Processing page {page.Page}: {page.Data.Count} records (Found {existing.Count} existing)");

                foreach (var account in page.Data)
                {
                    if (existing.TryGetValue(account.AccountId, out var existingRecord))
                    {
                        var lastInvoiceDateChanged = account.LastInvoiceDate.HasValue && 
                                                    existingRecord.LastInvoiceDate != account.LastInvoiceDate.Value;
                        var wasDeleted = existingRecord.IsDeleted;

                        if (lastInvoiceDateChanged || wasDeleted)
                        {
                            existingRecord.AccountNumber = account.AccountNumber;
                            existingRecord.VendorId = account.VendorId;
                            existingRecord.ClientId = account.ClientId;
                            existingRecord.LastInvoiceDate = account.LastInvoiceDate ?? DateTime.MinValue;
                            existingRecord.AccountName = account.AccountName;
                            existingRecord.VendorName = account.VendorName;
                            existingRecord.ClientName = account.ClientName;
                            existingRecord.TandemAccountId = account.TandemAcctId;
                            existingRecord.UpdatedAt = DateTime.UtcNow;
                            existingRecord.UpdatedBy = "ApiSync";
                            existingRecord.LastSyncedAt = runStart;

                            if (wasDeleted)
                            {
                                existingRecord.IsDeleted = false;
                                result.Reactivated++;
                            }

                            result.Updated++;
                        }
                        else
                        {
                            existingRecord.LastSyncedAt = runStart;
                        }
                    }
                    else
                    {
                        var newRecord = new ScheduleSyncSource
                        {
                            AccountId = account.AccountId,
                            AccountNumber = account.AccountNumber,
                            VendorId = account.VendorId,
                            ClientId = account.ClientId,
                            ScheduleFrequency = (int)ScheduleFrequency.Monthly, // Default to monthly as per user
                            LastInvoiceDate = account.LastInvoiceDate ?? DateTime.MinValue,
                            AccountName = account.AccountName,
                            VendorName = account.VendorName,
                            ClientName = account.ClientName,
                            TandemAccountId = account.TandemAcctId,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "ApiSync",
                            LastSyncedAt = runStart,
                            IsDeleted = false
                        };

                        await _dbContext.ScheduleSyncSources.AddAsync(newRecord);
                        result.Added++;
                    }

                    processedCount++;
                }

                batchCount++;

                if (batchCount % (_saveBatchSize / page.Batch) == 0 || processedCount >= expectedTotal)
                {
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved batch. Progress: {processedCount}/{expectedTotal} ({(processedCount * 100.0 / expectedTotal):F1}%)");
                }
            }

            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] All pages processed. Total records: {processedCount}");

            result.ProcessedCount = processedCount;
            result.ExpectedTotal = expectedTotal;

            if (processedCount == expectedTotal && expectedTotal > 0)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Processed count matches expected total. Marking untouched records as deleted...");
                
                var deletedCount = await _dbContext.ScheduleSyncSources
                    .Where(s => !s.IsDeleted && (s.LastSyncedAt == null || s.LastSyncedAt < runStart))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsDeleted, true)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(x => x.UpdatedBy, "ApiSync"));

                result.Deleted = deletedCount;
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Marked {deletedCount} records as deleted");
            }
            else if (processedCount != expectedTotal)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: Processed count ({processedCount}) does not match expected total ({expectedTotal}). Skipping soft delete to avoid false positives.");
                result.Warnings.Add($"Count mismatch: processed {processedCount}, expected {expectedTotal}. Soft delete skipped.");
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync completed successfully");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Duration: {(result.EndTime - result.StartTime).TotalMinutes:F2} minutes");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Added: {result.Added}, Updated: {result.Updated}, Reactivated: {result.Reactivated}, Deleted: {result.Deleted}");

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync failed: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

public class SyncResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public int ProcessedCount { get; set; }
    public int ExpectedTotal { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Reactivated { get; set; }
    public int Deleted { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}
