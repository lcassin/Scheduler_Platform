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

    public async Task<ClientSyncResult> SyncClientsAsync(List<AccountData> accounts)
    {
        var runStart = DateTime.UtcNow;
        var result = new ClientSyncResult { StartTime = runStart };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting client sync...");

        try
        {
            var uniqueClients = accounts
                .GroupBy(a => a.ClientId)
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {uniqueClients.Count} unique clients in API data");

            var externalClientIds = uniqueClients.Select(c => (int)c.ClientId).ToList();
            
            var existing = await _dbContext.Clients
                .Where(c => externalClientIds.Contains(c.ExternalClientId))
                .ToDictionaryAsync(c => c.ExternalClientId);

            foreach (var clientData in uniqueClients)
            {
                var externalClientId = (int)clientData.ClientId;
                
                if (existing.TryGetValue(externalClientId, out var existingClient))
                {
                    var nameChanged = existingClient.ClientName != clientData.ClientName;
                    var wasDeleted = existingClient.IsDeleted;

                    if (nameChanged || wasDeleted)
                    {
                        existingClient.ClientName = clientData.ClientName ?? $"Client {externalClientId}";
                        existingClient.UpdatedAt = DateTime.UtcNow;
                        existingClient.UpdatedBy = "ApiSync";
                        existingClient.LastSyncedAt = runStart;

                        if (wasDeleted)
                        {
                            existingClient.IsDeleted = false;
                            result.Reactivated++;
                        }

                        result.Updated++;
                    }
                    else
                    {
                        existingClient.LastSyncedAt = runStart;
                    }
                }
                else
                {
                    var newClient = new Client
                    {
                        ExternalClientId = externalClientId,
                        ClientName = clientData.ClientName ?? $"Client {externalClientId}",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "ApiSync",
                        LastSyncedAt = runStart,
                        IsDeleted = false
                    };

                    await _dbContext.Clients.AddAsync(newClient);
                    result.Added++;
                }
            }

            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Client sync: Added {result.Added}, Updated {result.Updated}, Reactivated {result.Reactivated}");

            var deletedCount = await _dbContext.Clients
                .Where(c => !c.IsDeleted && (c.LastSyncedAt == null || c.LastSyncedAt < runStart))
                .ExecuteUpdateAsync(c => c
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(x => x.UpdatedBy, "ApiSync"));

            result.Deleted = deletedCount;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Marked {deletedCount} clients as deleted");

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] !!! Client sync failed !!!");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Exception Type: {ex.GetType().FullName}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner Exception: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}");
            }
            throw;
        }
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
            var allAccounts = new List<AccountData>();
            var batchStartTime = DateTime.UtcNow;
            var totalBatchTime = TimeSpan.Zero;
            var batchesProcessed = 0;

            await foreach (var page in _apiClient.GetAllAccountsAsync(includeOnlyTandemAccounts))
            {
                var pageBatchStart = DateTime.UtcNow;
                
                if (expectedTotal == 0)
                {
                    expectedTotal = page.Total;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Expected total records: {expectedTotal:N0}");
                }

                allAccounts.AddRange(page.Data);

                var accountIds = page.Data.Select(a => a.AccountId).ToList();
                
                var existing = await _dbContext.ScheduleSyncSources
                    .Where(s => accountIds.Contains(s.ExternalAccountId))
                    .ToDictionaryAsync(s => s.ExternalAccountId);

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
                            existingRecord.ExternalVendorId = account.VendorId;
                            existingRecord.ExternalClientId = (int)account.ClientId;
                            existingRecord.CredentialId = account.CredentialId;
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
                            ExternalAccountId = account.AccountId,
                            AccountNumber = account.AccountNumber,
                            ExternalVendorId = account.VendorId,
                            ExternalClientId = (int)account.ClientId,
                            ClientId = null,
                            CredentialId = account.CredentialId,
                            ScheduleFrequency = (int)ScheduleFrequency.Monthly,
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
                
                var pageBatchTime = DateTime.UtcNow - pageBatchStart;
                totalBatchTime += pageBatchTime;
                batchesProcessed++;

                if (batchCount % (_saveBatchSize / page.Batch) == 0 || processedCount >= expectedTotal)
                {
                    await _dbContext.SaveChangesAsync();
                    
                    var elapsedTime = DateTime.UtcNow - runStart;
                    var avgBatchTime = totalBatchTime / batchesProcessed;
                    var recordsRemaining = expectedTotal - processedCount;
                    var estimatedBatchesRemaining = (double)recordsRemaining / page.Batch;
                    var estimatedTimeRemaining = TimeSpan.FromTicks((long)(avgBatchTime.Ticks * estimatedBatchesRemaining));
                    
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved batch. Progress: {processedCount:N0}/{expectedTotal:N0} ({(processedCount * 100.0 / expectedTotal):F1}%)");
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Elapsed: {FormatTimeSpan(elapsedTime)} | Avg batch: {avgBatchTime.TotalSeconds:F1}s | ETA: {FormatTimeSpan(estimatedTimeRemaining)}");
                }
            }

            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] All pages processed. Total records: {processedCount}");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Syncing clients from {allAccounts.Count} accounts...");
            var clientSyncResult = await SyncClientsAsync(allAccounts);
            result.ClientSyncResult = clientSyncResult;

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
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Accounts - Added: {result.Added}, Updated: {result.Updated}, Reactivated: {result.Reactivated}, Deleted: {result.Deleted}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Clients - Added: {clientSyncResult.Added}, Updated: {clientSyncResult.Updated}, Reactivated: {clientSyncResult.Reactivated}, Deleted: {clientSyncResult.Deleted}");

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] !!! Account sync failed !!!");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Exception Type: {ex.GetType().FullName}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner Exception: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner Stack Trace: {ex.InnerException.StackTrace}");
            }
            throw;
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
    public ClientSyncResult? ClientSyncResult { get; set; }
}

public class ClientSyncResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Reactivated { get; set; }
    public int Deleted { get; set; }
    public string? ErrorMessage { get; set; }
}
