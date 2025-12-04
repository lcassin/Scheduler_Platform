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

    public async Task<ClientSyncResult> SyncClientsAsync(List<AccountData> accounts, DateTime? runStart = null, bool performSoftDelete = true)
    {
        var syncRunStart = runStart ?? DateTime.UtcNow;
        var result = new ClientSyncResult { StartTime = syncRunStart };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting client sync...");

        try
        {
            var uniqueClients = accounts
                .GroupBy(a => a.ClientName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {uniqueClients.Count} unique clients in API data");

            var clientNames = uniqueClients.Select(c => c.ClientName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            
            var existingList = await _dbContext.Clients
                .Where(c => clientNames.Contains(c.ClientName))
                .ToListAsync();
            
            var existing = existingList
                .GroupBy(c => c.ClientName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(c => c.IsDeleted).ThenBy(c => c.Id).First(),
                    StringComparer.OrdinalIgnoreCase);
            
            if (existingList.Count != existing.Count)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Found {existingList.Count - existing.Count} duplicate client names in database (using canonical records)");
            }

            foreach (var clientData in uniqueClients)
            {
                var clientName = clientData.ClientName;
                
                if (string.IsNullOrWhiteSpace(clientName))
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Skipping client with null/empty name");
                    continue;
                }
                
                if (existing.TryGetValue(clientName, out var existingClient))
                {
                    var wasDeleted = existingClient.IsDeleted;

                    if (wasDeleted)
                    {
                        existingClient.IsDeleted = false;
                            existingClient.ModifiedDateTime = DateTime.UtcNow;
                            existingClient.ModifiedBy = "ApiSync";
                            existingClient.LastSyncedDateTime = syncRunStart;
                        result.Reactivated++;
                        result.Updated++;
                    }
                    else
                    {
                        existingClient.LastSyncedDateTime = syncRunStart;
                    }
                }
                else
                {
                    var newClient = new Client
                    {
                        ClientName = clientName,
                        ClientCode = clientName.Length > 50 ? clientName.Substring(0, 50) : clientName,
                        IsActive = true,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedBy = "ApiSync",
                        LastSyncedDateTime = syncRunStart,
                        IsDeleted = false
                    };

                    await _dbContext.Clients.AddAsync(newClient);
                    result.Added++;
                }
            }

            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Client sync: Added {result.Added}, Updated {result.Updated}, Reactivated {result.Reactivated}");

            if (performSoftDelete)
            {
                var deletedCount = await _dbContext.Clients
                    .Where(c => !c.IsDeleted && (c.LastSyncedDateTime == null || c.LastSyncedDateTime < syncRunStart))
                    .ExecuteUpdateAsync(c => c
                        .SetProperty(x => x.IsDeleted, true)
                        .SetProperty(x => x.ModifiedDateTime, DateTime.UtcNow)
                        .SetProperty(x => x.ModifiedBy, "ApiSync"));

                result.Deleted = deletedCount;
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Marked {deletedCount} clients as deleted");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Soft delete skipped (performSoftDelete=false)");
            }

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

    public async Task<ClientSyncResult> SyncClientsFromDatabaseAsync(DateTime runStart, bool performSoftDelete = true)
    {
        var result = new ClientSyncResult { StartTime = runStart };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting client sync from database...");

        try
        {
            var originalTimeout = _dbContext.Database.GetCommandTimeout();
            _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

            var oldAutoDetect = _dbContext.ChangeTracker.AutoDetectChangesEnabled;
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                var uniqueClientsDict = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
                const int pageSize = 100000;
                int lastId = 0;
                int totalScanned = 0;

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scanning ScheduleSyncSources in batches of {pageSize:N0}...");

                while (true)
                {
                    var page = await _dbContext.ScheduleSyncSources
                        .AsNoTracking()
                        .Where(s => !s.IsDeleted && s.Id > lastId)
                        .OrderBy(s => s.Id)
                        .Select(s => new
                        {
                            s.Id,
                            s.ClientName,
                            s.LastSyncedDateTime
                        })
                        .Take(pageSize)
                        .ToListAsync();

                    if (page.Count == 0)
                        break;

                    foreach (var row in page)
                    {
                        var clientName = row.ClientName?.Trim();
                        if (string.IsNullOrWhiteSpace(clientName))
                            continue;

                        if (uniqueClientsDict.TryGetValue(clientName, out var existing))
                        {
                            if ((row.LastSyncedDateTime ?? DateTime.MinValue) > (existing ?? DateTime.MinValue))
                            {
                                uniqueClientsDict[clientName] = row.LastSyncedDateTime;
                            }
                        }
                        else
                        {
                            uniqueClientsDict[clientName] = row.LastSyncedDateTime;
                        }
                    }

                    lastId = page[^1].Id;
                    totalScanned += page.Count;

                    if (totalScanned % 500000 == 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scanned {totalScanned:N0} rows, found {uniqueClientsDict.Count:N0} unique clients so far...");
                    }
                }

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scanned {totalScanned:N0} total rows, found {uniqueClientsDict.Count:N0} unique clients");

                var clientNames = uniqueClientsDict.Keys.ToList();
                var existingClients = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
                const int chunkSize = 2000;
                int duplicatesFound = 0;

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fetching existing clients in chunks of {chunkSize:N0}...");

                for (int i = 0; i < clientNames.Count; i += chunkSize)
                {
                    var chunk = clientNames.Skip(i).Take(chunkSize).ToList();
                    var chunkClients = await _dbContext.Clients
                        .Where(c => chunk.Contains(c.ClientName))
                        .ToListAsync();

                    foreach (var client in chunkClients)
                    {
                        var clientName = client.ClientName?.Trim();
                        if (string.IsNullOrWhiteSpace(clientName))
                            continue;
                            
                        if (existingClients.ContainsKey(clientName))
                        {
                            duplicatesFound++;
                            var existing = existingClients[clientName];
                            if (client.IsDeleted && !existing.IsDeleted)
                                continue;
                            if (!client.IsDeleted && existing.IsDeleted)
                            {
                                existingClients[clientName] = client;
                                continue;
                            }
                            if (client.Id < existing.Id)
                            {
                                existingClients[clientName] = client;
                            }
                        }
                        else
                        {
                            existingClients[clientName] = client;
                        }
                    }

                    if ((i + chunkSize) % 10000 == 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fetched {Math.Min(i + chunkSize, clientNames.Count):N0} / {clientNames.Count:N0} client records...");
                    }
                }

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {existingClients.Count:N0} existing clients");
                if (duplicatesFound > 0)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Found {duplicatesFound} duplicate client names in database (using canonical records)");
                }

                foreach (var kvp in uniqueClientsDict)
                {
                    var clientName = kvp.Key;

                    if (existingClients.TryGetValue(clientName, out var existingClient))
                    {
                        var wasDeleted = existingClient.IsDeleted;

                        if (wasDeleted)
                        {
                            existingClient.IsDeleted = false;
                                existingClient.ModifiedDateTime = DateTime.UtcNow;
                                existingClient.ModifiedBy = "ApiSync";
                                existingClient.LastSyncedDateTime = runStart;
                            result.Reactivated++;
                            result.Updated++;
                        }
                        else
                        {
                            existingClient.LastSyncedDateTime = runStart;
                        }
                    }
                    else
                    {
                        var newClient = new Client
                        {
                            ClientName = clientName,
                            ClientCode = clientName.Length > 50 ? clientName.Substring(0, 50) : clientName,
                            IsActive = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedBy = "ApiSync",
                            LastSyncedDateTime = runStart,
                            IsDeleted = false
                        };

                        await _dbContext.Clients.AddAsync(newClient);
                        result.Added++;
                    }
                }

                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Client sync: Added {result.Added}, Updated {result.Updated}, Reactivated {result.Reactivated}");

                if (performSoftDelete)
                {
                    var deletedCount = await _dbContext.Clients
                        .Where(c => !c.IsDeleted && (c.LastSyncedDateTime == null || c.LastSyncedDateTime < runStart))
                        .ExecuteUpdateAsync(c => c
                            .SetProperty(x => x.IsDeleted, true)
                            .SetProperty(x => x.ModifiedDateTime, DateTime.UtcNow)
                            .SetProperty(x => x.ModifiedBy, "ApiSync"));

                    result.Deleted = deletedCount;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Marked {deletedCount} clients as deleted");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Soft delete skipped (performSoftDelete=false)");
                }

                result.EndTime = DateTime.UtcNow;
                result.Success = true;

                return result;
            }
            finally
            {
                _dbContext.ChangeTracker.AutoDetectChangesEnabled = oldAutoDetect;
                _dbContext.Database.SetCommandTimeout(originalTimeout);
            }
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

            await foreach (var page in _apiClient.GetAllAccountsAsync(includeOnlyTandemAccounts))
            {
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

                var accountsWithDates = page.Data.Count(a => a.LastInvoiceDate.HasValue);
                var accountsWithValidDates = page.Data.Count(a => a.LastInvoiceDate.HasValue && a.LastInvoiceDate.Value != DateTime.MinValue);
                var accountsWithUnknownDates = page.Data.Count(a => !a.LastInvoiceDate.HasValue || a.LastInvoiceDate.Value == DateTime.MinValue);
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Processing page {page.Page}: {page.Data.Count} records (Found {existing.Count} existing, {accountsWithValidDates} valid dates, {accountsWithUnknownDates} unknown/null dates)");

                var pageUpdated = 0;
                var pageUnchanged = 0;
                var pageAdded = 0;
                
                foreach (var account in page.Data)
                {
                    if (existing.TryGetValue(account.AccountId, out var existingRecord))
                    {
                        var lastInvoiceDateChanged = account.LastInvoiceDate.HasValue && 
                                                    existingRecord.LastInvoiceDateTime != account.LastInvoiceDate.Value;
                        var wasDeleted = existingRecord.IsDeleted;

                        if (lastInvoiceDateChanged || wasDeleted)
                        {
                            existingRecord.AccountNumber = account.AccountNumber;
                            existingRecord.ExternalVendorId = account.VendorId;
                            existingRecord.ExternalClientId = (int)account.ClientId;
                            existingRecord.CredentialId = account.CredentialId;
                            
                            if (account.LastInvoiceDate.HasValue)
                            {
                                existingRecord.LastInvoiceDateTime = account.LastInvoiceDate.Value;
                            }
                            
                            existingRecord.AccountName = account.AccountName;
                            existingRecord.VendorName = account.VendorName;
                            existingRecord.ClientName = account.ClientName;
                            existingRecord.TandemAccountId = account.TandemAcctId;
                            existingRecord.ModifiedDateTime = DateTime.UtcNow;
                            existingRecord.ModifiedBy = "ApiSync";
                            existingRecord.LastSyncedDateTime = runStart;

                            if (wasDeleted)
                            {
                                existingRecord.IsDeleted = false;
                                result.Reactivated++;
                            }

                            result.Updated++;
                            pageUpdated++;
                        }
                        else
                        {
                            existingRecord.LastSyncedDateTime = runStart;
                            pageUnchanged++;
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
                            LastInvoiceDateTime = account.LastInvoiceDate ?? DateTime.MinValue,
                            AccountName = account.AccountName,
                            VendorName = account.VendorName,
                            ClientName = account.ClientName,
                            TandemAccountId = account.TandemAcctId,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedBy = "ApiSync",
                            LastSyncedDateTime = runStart,
                            IsDeleted = false
                        };

                        await _dbContext.ScheduleSyncSources.AddAsync(newRecord);
                        result.Added++;
                        pageAdded++;
                    }

                    processedCount++;
                }
                
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Page {page.Page} results: {pageUpdated} updated, {pageUnchanged} unchanged, {pageAdded} added");

                batchCount++;

                if (batchCount % (_saveBatchSize / page.Batch) == 0 || processedCount >= expectedTotal)
                {
                    await _dbContext.SaveChangesAsync();
                    
                    var elapsedTime = DateTime.UtcNow - runStart;
                    var recordsRemaining = expectedTotal - processedCount;
                    
                    if (elapsedTime.TotalSeconds > 0 && processedCount > 0)
                    {
                        var recordsPerSecond = processedCount / elapsedTime.TotalSeconds;
                        var estimatedSecondsRemaining = recordsRemaining / recordsPerSecond;
                        var estimatedTimeRemaining = TimeSpan.FromSeconds(estimatedSecondsRemaining);
                        
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved batch. Progress: {processedCount:N0}/{expectedTotal:N0} ({(processedCount * 100.0 / expectedTotal):F1}%)");
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Elapsed: {FormatTimeSpan(elapsedTime)} | Rate: {recordsPerSecond:F1} rec/s | ETA: {FormatTimeSpan(estimatedTimeRemaining)}");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved batch. Progress: {processedCount:N0}/{expectedTotal:N0} ({(processedCount * 100.0 / expectedTotal):F1}%)");
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Elapsed: {FormatTimeSpan(elapsedTime)} | Calculating ETA...");
                    }
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
                    .Where(s => !s.IsDeleted && (s.LastSyncedDateTime == null || s.LastSyncedDateTime < runStart))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsDeleted, true)
                        .SetProperty(x => x.ModifiedDateTime, DateTime.UtcNow)
                        .SetProperty(x => x.ModifiedBy, "ApiSync"));

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
