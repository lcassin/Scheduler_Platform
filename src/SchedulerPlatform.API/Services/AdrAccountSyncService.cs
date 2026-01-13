using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Services;
using SchedulerPlatform.Infrastructure.Data;
using System.Data;

namespace SchedulerPlatform.API.Services;

public interface IAdrAccountSyncService
{
    Task<AdrAccountSyncResult> SyncAccountsAsync(
        Action<int, int>? progressCallback = null, 
        Action<string, int, int>? subStepCallback = null,
        CancellationToken cancellationToken = default);
}

public class AdrAccountSyncResult
{
    public int TotalAccountsProcessed { get; set; }
    public int AccountsInserted { get; set; }
    public int AccountsUpdated { get; set; }
    public int AccountsMarkedDeleted { get; set; }
    public int ClientsCreated { get; set; }
    public int ClientsUpdated { get; set; }
    public int RulesCreated { get; set; }
    public int RulesUpdated { get; set; }
    public int RulesSkippedOverridden { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public DateTime SyncStartDateTime { get; set; }
    public DateTime SyncEndDateTime { get; set; }
    public TimeSpan Duration => SyncEndDateTime - SyncStartDateTime;
}

public class AdrAccountSyncService : IAdrAccountSyncService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly SchedulerDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdrAccountSyncService> _logger;

    public AdrAccountSyncService(
        IUnitOfWork unitOfWork,
        SchedulerDbContext dbContext,
        IConfiguration configuration,
        ILogger<AdrAccountSyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdrAccountSyncResult> SyncAccountsAsync(
        Action<int, int>? progressCallback = null, 
        Action<string, int, int>? subStepCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new AdrAccountSyncResult
        {
            SyncStartDateTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting ADR account sync");

            var externalConnectionString = _configuration.GetConnectionString("VendorCredential");
            if (string.IsNullOrEmpty(externalConnectionString))
            {
                throw new InvalidOperationException("VendorCredential connection string not configured");
            }

            // PERFORMANCE OPTIMIZATION: Stream external data instead of loading all into memory
            // This reduces memory usage from 2x170k full records to streaming + lookups
            
            // Step 1: Get total count for progress reporting (lightweight query)
            var totalExternalCount = await GetExternalAccountCountAsync(externalConnectionString, cancellationToken);
            _logger.LogInformation("External database has {Count} accounts to sync", totalExternalCount);
            
            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, totalExternalCount);

            // Step 2: Sync Clients first - fetch unique clients from VendorCred (lightweight query)
            var externalClientIdToInternalClientId = await SyncClientsFromExternalAsync(externalConnectionString, result, cancellationToken);
            _logger.LogInformation("Client sync complete. Created: {Created}, Updated: {Updated}", 
                result.ClientsCreated, result.ClientsUpdated);

            // Step 3: Load existing local accounts into dictionary for lookups
            // We need this to determine updates vs inserts and for deletion detection
            const int batchSize = 5000;
            
            var existingAccountList = await _dbContext.AdrAccounts
                .Where(a => !a.IsDeleted)
                .ToListAsync(cancellationToken);

            var existingAccounts = existingAccountList
                .GroupBy(a => (a.VMAccountId, a.VMAccountNumber))
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        if (g.Count() > 1)
                        {
                            _logger.LogWarning(
                                "Found {Count} AdrAccount rows with VMAccountId {VMAccountId} and VMAccountNumber {VMAccountNumber}. " +
                                "Using the most recently modified one.",
                                g.Count(),
                                g.Key.VMAccountId,
                                g.Key.VMAccountNumber);
                        }
                        return g.OrderByDescending(a => a.ModifiedDateTime).First();
                    });

            _logger.LogInformation("Loaded {Count} existing accounts from local database", existingAccounts.Count);

            // Track processed accounts and scheduling data for rule sync
            var processedAccountKeys = new HashSet<(long VMAccountId, string VMAccountNumber)>();
            var schedulingDataLookup = new Dictionary<(long VMAccountId, string VMAccountNumber), SchedulingData>();
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Step 4: Stream external accounts and process in batches
            _logger.LogInformation("Streaming and processing {Count} accounts in batches of {BatchSize}", totalExternalCount, batchSize);

            await using var connection = new SqlConnection(externalConnectionString);
            await connection.OpenAsync(cancellationToken);

            var query = GetAccountSyncQuery();
            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 300;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                try
                {
                    var externalAccount = ReadExternalAccountFromReader(reader);
                    var accountKey = (externalAccount.VMAccountId, externalAccount.VMAccountNumber);
                    processedAccountKeys.Add(accountKey);

                    // Store scheduling data for rule sync (lightweight - only fields needed for rules)
                    schedulingDataLookup[accountKey] = new SchedulingData
                    {
                        PeriodType = externalAccount.PeriodType,
                        PeriodDays = externalAccount.PeriodDays,
                        NextRunDateTime = externalAccount.NextRunDateTime,
                        NextRangeStartDateTime = externalAccount.NextRangeStartDateTime,
                        NextRangeEndDateTime = externalAccount.NextRangeEndDateTime
                    };

                    // Look up the internal ClientId using the ExternalClientId mapping
                    int? internalClientId = null;
                    if (externalAccount.ClientId.HasValue && 
                        externalClientIdToInternalClientId.TryGetValue(externalAccount.ClientId.Value, out var mappedClientId))
                    {
                        internalClientId = mappedClientId;
                    }

                    if (existingAccounts.TryGetValue(accountKey, out var existingAccount))
                    {
                        UpdateExistingAccount(existingAccount, externalAccount, internalClientId);
                        result.AccountsUpdated++;
                    }
                    else
                    {
                        var newAccount = CreateNewAccount(externalAccount, internalClientId);
                        await _dbContext.AdrAccounts.AddAsync(newAccount, cancellationToken);
                        existingAccounts[accountKey] = newAccount;
                        result.AccountsInserted++;
                    }

                    result.TotalAccountsProcessed++;
                    processedSinceLastSave++;

                    // Save in batches
                    if (processedSinceLastSave >= batchSize)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Batch {BatchNumber} saved: {Count} accounts processed so far", 
                            batchNumber, result.TotalAccountsProcessed);
                        
                        progressCallback?.Invoke(result.TotalAccountsProcessed, totalExternalCount);
                        
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing account from external data");
                    result.Errors++;
                    result.ErrorMessages.Add($"Error processing account: {ex.Message}");
                }
            }

            // Mark accounts not in external data as deleted
            int deletedSinceLastSave = 0;
            foreach (var kvp in existingAccounts)
            {
                var accountKey = kvp.Key;
                var existingAccount = kvp.Value;
                
                if (!processedAccountKeys.Contains(accountKey))
                {
                    existingAccount.IsDeleted = true;
                    existingAccount.ModifiedDateTime = DateTime.UtcNow;
                    existingAccount.ModifiedBy = "System Created";
                    result.AccountsMarkedDeleted++;
                    deletedSinceLastSave++;

                    if (deletedSinceLastSave >= batchSize)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Deletion batch saved: {Count} accounts marked deleted so far", 
                            result.AccountsMarkedDeleted);
                        deletedSinceLastSave = 0;
                    }
                }
            }

            // Final save for any remaining changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Final batch saved. Total batches: {BatchCount}", batchNumber);
            
            progressCallback?.Invoke(totalExternalCount, totalExternalCount);

            // Step 5: Sync AdrAccountRules using lightweight scheduling data
            // PERFORMANCE OPTIMIZATION: Detach account entities from change tracker before rule sync
            var accountsList = existingAccounts.Values.ToList();
            foreach (var account in accountsList)
            {
                _dbContext.Entry(account).State = EntityState.Detached;
            }
            _logger.LogInformation("Detached {Count} account entities from change tracker before rule sync", accountsList.Count);
            
            await SyncAccountRulesOptimizedAsync(accountsList, schedulingDataLookup, result, cancellationToken);

            result.SyncEndDateTime = DateTime.UtcNow;
            _logger.LogInformation(
                "ADR account sync completed. Clients: {ClientsCreated} created/{ClientsUpdated} updated. Accounts: {Processed} processed, {Inserted} inserted, {Updated} updated, {Deleted} deleted. Rules: {RulesCreated} created, {RulesUpdated} updated, {RulesSkipped} skipped (overridden). Errors: {Errors}. Duration: {Duration}",
                result.ClientsCreated, result.ClientsUpdated, result.TotalAccountsProcessed, result.AccountsInserted, 
                result.AccountsUpdated, result.AccountsMarkedDeleted, result.RulesCreated, result.RulesUpdated, 
                result.RulesSkippedOverridden, result.Errors, result.Duration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ADR account sync failed");
            result.SyncEndDateTime = DateTime.UtcNow;
            result.Errors++;
            result.ErrorMessages.Add($"Sync failed: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Lightweight class to store only the scheduling fields needed for rule sync.
    /// This reduces memory usage compared to storing full ExternalAccountData objects.
    /// </summary>
    private class SchedulingData
    {
        public string? PeriodType { get; set; }
        public int? PeriodDays { get; set; }
        public DateTime? NextRunDateTime { get; set; }
        public DateTime? NextRangeStartDateTime { get; set; }
        public DateTime? NextRangeEndDateTime { get; set; }
    }
    
    /// <summary>
    /// Gets the count of accounts from the external database for progress reporting.
    /// </summary>
    private async Task<int> GetExternalAccountCountAsync(string connectionString, CancellationToken cancellationToken)
    {
        // Use a simplified count query that mirrors the main query's filtering logic
        var countQuery = @"
SELECT COUNT(DISTINCT CONCAT(CA.AccountId, '_', A.AccountNumber))
FROM CredentialAccount CA
INNER JOIN Account A ON CA.AccountId = A.AccountId
INNER JOIN [Credential] C ON C.IsActive = 1 AND C.CredentialId = CA.CredentialId
WHERE EXISTS (SELECT 1 FROM ADRInvoiceAccountData AD WHERE AD.VCAccountId = CA.AccountId)";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = new SqlCommand(countQuery, connection);
        command.CommandTimeout = 120;
        
        var countResult = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(countResult);
    }
    
    /// <summary>
    /// Syncs clients directly from the external database without loading all account data.
    /// </summary>
    private async Task<Dictionary<int, int>> SyncClientsFromExternalAsync(
        string connectionString,
        AdrAccountSyncResult result,
        CancellationToken cancellationToken)
    {
        // Query unique clients from external database
        var clientQuery = @"
SELECT DISTINCT CL.ClientId, CL.ClientName
FROM [dbo].[ADRInvoiceAccountData] AD
    LEFT OUTER JOIN Account A ON AD.VCAccountId = A.AccountId
    LEFT OUTER JOIN Client CL ON A.ClientId = CL.ClientId
WHERE CL.ClientId IS NOT NULL";

        var uniqueClients = new List<(int ExternalClientId, string ClientName)>();
        
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var command = new SqlCommand(clientQuery, connection);
        command.CommandTimeout = 120;
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var clientId = reader.GetInt32(0);
            var clientName = reader.IsDBNull(1) ? $"Client {clientId}" : reader.GetString(1);
            uniqueClients.Add((clientId, clientName));
        }

        _logger.LogInformation("Found {Count} unique clients in external data", uniqueClients.Count);

        // Load existing clients by ExternalClientId
        var externalClientIds = uniqueClients.Select(c => c.ExternalClientId).ToList();
        var clientList = await _dbContext.Clients
            .Where(c => externalClientIds.Contains(c.ExternalClientId))
            .ToListAsync(cancellationToken);

        var existingClients = clientList
            .GroupBy(c => c.ExternalClientId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    if (g.Count() > 1)
                    {
                        _logger.LogWarning(
                            "Found {Count} Client rows with ExternalClientId {ExternalClientId}. Using the most recently modified one.",
                            g.Count(), g.Key);
                    }
                    return g.OrderBy(c => c.IsDeleted).ThenByDescending(c => c.ModifiedDateTime).First();
                });

        var now = DateTime.UtcNow;

        foreach (var (externalClientId, clientName) in uniqueClients)
        {
            if (existingClients.TryGetValue(externalClientId, out var existingClient))
            {
                if (existingClient.ClientName != clientName)
                {
                    existingClient.ClientName = clientName;
                    existingClient.ModifiedDateTime = now;
                    existingClient.ModifiedBy = "System Created";
                    existingClient.LastSyncedDateTime = now;
                    result.ClientsUpdated++;
                }
                else
                {
                    existingClient.LastSyncedDateTime = now;
                }
            }
            else
            {
                var newClient = new Client
                {
                    ExternalClientId = externalClientId,
                    ClientName = clientName,
                    ClientCode = clientName.Length > 50 ? clientName.Substring(0, 50) : clientName,
                    IsActive = true,
                    CreatedDateTime = now,
                    CreatedBy = "System Created",
                    ModifiedDateTime = now,
                    ModifiedBy = "System Created",
                    LastSyncedDateTime = now,
                    IsDeleted = false
                };

                await _dbContext.Clients.AddAsync(newClient, cancellationToken);
                existingClients[externalClientId] = newClient;
                result.ClientsCreated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingClients.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id);
    }
    
    /// <summary>
    /// Reads a single external account record from the data reader.
    /// </summary>
    private ExternalAccountData ReadExternalAccountFromReader(SqlDataReader reader)
    {
        var account = new ExternalAccountData
        {
            VMAccountId = reader.GetInt64(reader.GetOrdinal("VMAccountId")),
            CredentialId = reader.GetInt32(reader.GetOrdinal("CredentialId")),
            ClientId = reader.IsDBNull(reader.GetOrdinal("ClientId")) ? null : reader.GetInt32(reader.GetOrdinal("ClientId")),
            ClientName = reader.IsDBNull(reader.GetOrdinal("ClientName")) ? null : reader.GetString(reader.GetOrdinal("ClientName")),
            VendorCode = reader.IsDBNull(reader.GetOrdinal("VendorCode")) ? null : reader.GetString(reader.GetOrdinal("VendorCode")),
            VMAccountNumber = reader.GetString(reader.GetOrdinal("VMAccountNumber")),
            InterfaceAccountId = reader.IsDBNull(reader.GetOrdinal("InterfaceAccountId")) ? null : reader.GetString(reader.GetOrdinal("InterfaceAccountId")),
            PeriodType = reader.IsDBNull(reader.GetOrdinal("PeriodType")) ? null : reader.GetString(reader.GetOrdinal("PeriodType")),
            PeriodDays = reader.IsDBNull(reader.GetOrdinal("PeriodDays")) ? null : reader.GetInt32(reader.GetOrdinal("PeriodDays")),
            MedianDays = reader.IsDBNull(reader.GetOrdinal("MedianDays")) ? null : reader.GetDouble(reader.GetOrdinal("MedianDays")),
            InvoiceCount = reader.GetInt32(reader.GetOrdinal("InvoiceCount")),
            LastInvoiceDateTime = reader.IsDBNull(reader.GetOrdinal("LastInvoiceDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastInvoiceDate")),
            ExpectedRangeStartDateTime = reader.IsDBNull(reader.GetOrdinal("ExpectedRangeStart")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpectedRangeStart")),
            ExpectedNextDateTime = reader.IsDBNull(reader.GetOrdinal("ExpectedNextDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpectedNextDate")),
            ExpectedRangeEndDateTime = reader.IsDBNull(reader.GetOrdinal("ExpectedRangeEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpectedRangeEnd")),
            NextRangeStartDateTime = reader.IsDBNull(reader.GetOrdinal("NextRangeStart")) ? null : reader.GetDateTime(reader.GetOrdinal("NextRangeStart")),
            NextRunDateTime = reader.IsDBNull(reader.GetOrdinal("NextRunDate")) ? null : reader.GetDateTime(reader.GetOrdinal("NextRunDate")),
            NextRangeEndDateTime = reader.IsDBNull(reader.GetOrdinal("NextRangeEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("NextRangeEnd")),
            DaysUntilNextRun = reader.IsDBNull(reader.GetOrdinal("DaysUntilNextRun")) ? null : reader.GetInt32(reader.GetOrdinal("DaysUntilNextRun")),
            NextRunStatus = reader.IsDBNull(reader.GetOrdinal("NextRunStatus")) ? null : reader.GetString(reader.GetOrdinal("NextRunStatus")),
            HistoricalBillingStatus = reader.IsDBNull(reader.GetOrdinal("HistoricalBillingStatus")) ? null : reader.GetString(reader.GetOrdinal("HistoricalBillingStatus"))
        };
        
        // Recalculate dates using BillingPeriodCalculator to prevent date creep
        if (account.LastInvoiceDateTime.HasValue)
        {
            var today = DateTime.UtcNow.Date;
            var anchorDayOfMonth = BillingPeriodCalculator.GetAnchorDayOfMonth(account.LastInvoiceDateTime.Value);
            var (windowBefore, windowAfter) = BillingPeriodCalculator.GetDefaultWindowDays(account.PeriodType);
            
            var expectedNext = BillingPeriodCalculator.CalculateNextRunDate(
                account.PeriodType,
                account.LastInvoiceDateTime.Value,
                anchorDayOfMonth);
            
            var nextRun = BillingPeriodCalculator.CalculateNextRunDateOnOrAfterToday(
                account.PeriodType,
                account.LastInvoiceDateTime.Value,
                today,
                anchorDayOfMonth);
            
            account.ExpectedNextDateTime = expectedNext;
            account.ExpectedRangeStartDateTime = expectedNext.AddDays(-windowBefore);
            account.ExpectedRangeEndDateTime = expectedNext.AddDays(windowAfter);
            account.NextRunDateTime = nextRun;
            account.NextRangeStartDateTime = nextRun.AddDays(-windowBefore);
            account.NextRangeEndDateTime = nextRun.AddDays(windowAfter);
            account.DaysUntilNextRun = (int)(nextRun - today).TotalDays;
            
            var periodDays = BillingPeriodCalculator.GetApproximatePeriodDays(account.PeriodType);
            var daysUntilExpected = (int)(expectedNext - today).TotalDays;
            
            var missingThreshold = -(periodDays * 2);
            if (daysUntilExpected < missingThreshold)
                account.HistoricalBillingStatus = "Missing";
            else if (daysUntilExpected < -windowBefore)
                account.HistoricalBillingStatus = "Overdue";
            else if (daysUntilExpected < 0)
                account.HistoricalBillingStatus = "Due Now";
            else if (daysUntilExpected <= windowBefore)
                account.HistoricalBillingStatus = "Due Soon";
            else if (daysUntilExpected <= 30)
                account.HistoricalBillingStatus = "Upcoming";
            else
                account.HistoricalBillingStatus = "Future";
            
            if (account.HistoricalBillingStatus == "Missing")
            {
                account.NextRunStatus = "Missing";
            }
            else
            {
                var daysUntilRun = account.DaysUntilNextRun ?? 0;
                if (daysUntilRun <= 0)
                    account.NextRunStatus = "Run Now";
                else if (daysUntilRun <= windowBefore)
                    account.NextRunStatus = "Due Soon";
                else if (daysUntilRun <= 30)
                    account.NextRunStatus = "Upcoming";
                else
                    account.NextRunStatus = "Future";
            }
        }
        
        return account;
    }
    
    /// <summary>
    /// Optimized rule sync that uses lightweight scheduling data instead of full ExternalAccountData.
    /// </summary>
    private async Task SyncAccountRulesOptimizedAsync(
        List<AdrAccount> accounts,
        Dictionary<(long VMAccountId, string VMAccountNumber), SchedulingData> schedulingDataLookup,
        AdrAccountSyncResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting optimized rule sync for {Count} accounts", accounts.Count);

        var accountIds = accounts.Where(a => !a.IsDeleted).Select(a => a.Id).ToList();

        var existingRules = await _dbContext.AdrAccountRules
            .Where(r => accountIds.Contains(r.AdrAccountId) && !r.IsDeleted && r.JobTypeId == 2)
            .ToListAsync(cancellationToken);

        var existingRulesByAccountId = existingRules
            .GroupBy(r => r.AdrAccountId)
            .ToDictionary(g => g.Key, g => g.First());

        const int batchSize = 5000;
        int processedSinceLastSave = 0;
        int batchNumber = 1;

        foreach (var account in accounts.Where(a => !a.IsDeleted))
        {
            try
            {
                var accountKey = (account.VMAccountId, account.VMAccountNumber ?? string.Empty);
                if (!schedulingDataLookup.TryGetValue(accountKey, out var schedulingData))
                {
                    continue;
                }

                if (existingRulesByAccountId.TryGetValue(account.Id, out var existingRule))
                {
                    if (existingRule.IsManuallyOverridden)
                    {
                        result.RulesSkippedOverridden++;
                    }
                    else
                    {
                        existingRule.PeriodType = schedulingData.PeriodType;
                        existingRule.PeriodDays = schedulingData.PeriodDays;
                        existingRule.NextRunDateTime = schedulingData.NextRunDateTime;
                        existingRule.NextRangeStartDateTime = schedulingData.NextRangeStartDateTime;
                        existingRule.NextRangeEndDateTime = schedulingData.NextRangeEndDateTime;
                        existingRule.ModifiedDateTime = DateTime.UtcNow;
                        existingRule.ModifiedBy = "System Created";
                        result.RulesUpdated++;
                    }
                }
                else
                {
                    var newRule = new AdrAccountRule
                    {
                        AdrAccountId = account.Id,
                        JobTypeId = 2,
                        PeriodType = schedulingData.PeriodType,
                        PeriodDays = schedulingData.PeriodDays,
                        NextRunDateTime = schedulingData.NextRunDateTime,
                        NextRangeStartDateTime = schedulingData.NextRangeStartDateTime,
                        NextRangeEndDateTime = schedulingData.NextRangeEndDateTime,
                        IsEnabled = true,
                        IsManuallyOverridden = false,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedBy = "System Created",
                        ModifiedDateTime = DateTime.UtcNow,
                        ModifiedBy = "System Created",
                        IsDeleted = false
                    };
                    await _dbContext.AdrAccountRules.AddAsync(newRule, cancellationToken);
                    result.RulesCreated++;
                }

                processedSinceLastSave++;

                if (processedSinceLastSave >= batchSize)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Rule sync batch {BatchNumber} saved: {Created} created, {Updated} updated, {Skipped} skipped so far",
                        batchNumber, result.RulesCreated, result.RulesUpdated, result.RulesSkippedOverridden);
                    processedSinceLastSave = 0;
                    batchNumber++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing rule for account {AccountId}", account.Id);
                result.Errors++;
                result.ErrorMessages.Add($"Rule sync for AccountId {account.Id}: {ex.Message}");
            }
        }

        if (processedSinceLastSave > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Rule sync completed. Created: {Created}, Updated: {Updated}, Skipped (overridden): {Skipped}",
            result.RulesCreated, result.RulesUpdated, result.RulesSkippedOverridden);
    }

    /// <summary>
    /// Syncs Client records from external account data.
    /// Creates new Clients for unknown ExternalClientIds, updates existing ones.
    /// Returns a mapping from ExternalClientId to internal ClientId.
    /// </summary>
    private async Task<Dictionary<int, int>> SyncClientsAsync(
        List<ExternalAccountData> externalAccounts, 
        AdrAccountSyncResult result,
        CancellationToken cancellationToken)
    {
        // Extract unique ExternalClientId -> ClientName pairs from external data
        var uniqueClients = externalAccounts
            .Where(a => a.ClientId.HasValue)
            .GroupBy(a => a.ClientId!.Value)
            .Select(g => new { ExternalClientId = g.Key, ClientName = g.First().ClientName ?? $"Client {g.Key}" })
            .ToList();

        _logger.LogInformation("Found {Count} unique clients in external data", uniqueClients.Count);

        // Load existing clients by ExternalClientId
        // Use ToListAsync + GroupBy to handle potential duplicates gracefully
        var externalClientIds = uniqueClients.Select(c => c.ExternalClientId).ToList();
        var clientList = await _dbContext.Clients
            .Where(c => externalClientIds.Contains(c.ExternalClientId))
            .ToListAsync(cancellationToken);

        var existingClients = clientList
            .GroupBy(c => c.ExternalClientId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    if (g.Count() > 1)
                    {
                        _logger.LogWarning(
                            "Found {Count} Client rows with ExternalClientId {ExternalClientId}. " +
                            "Using the most recently modified one. Please clean up duplicate records.",
                            g.Count(),
                            g.Key);
                    }
                    // Prefer non-deleted, then most recently modified
                    return g
                        .OrderBy(c => c.IsDeleted)
                        .ThenByDescending(c => c.ModifiedDateTime)
                        .First();
                });

        _logger.LogInformation("Found {Count} existing clients by ExternalClientId", existingClients.Count);

        var now = DateTime.UtcNow;

        foreach (var clientData in uniqueClients)
        {
            if (existingClients.TryGetValue(clientData.ExternalClientId, out var existingClient))
            {
                // Update existing client if name changed
                if (existingClient.ClientName != clientData.ClientName)
                {
                    existingClient.ClientName = clientData.ClientName;
                    existingClient.ModifiedDateTime = now;
                    existingClient.ModifiedBy = "System Created";
                    existingClient.LastSyncedDateTime = now;
                    result.ClientsUpdated++;
                }
                else
                {
                    existingClient.LastSyncedDateTime = now;
                }
            }
            else
            {
                // Create new client
                var newClient = new Client
                {
                    ExternalClientId = clientData.ExternalClientId,
                    ClientName = clientData.ClientName,
                    ClientCode = clientData.ClientName.Length > 50 
                        ? clientData.ClientName.Substring(0, 50) 
                        : clientData.ClientName,
                    IsActive = true,
                    CreatedDateTime = now,
                    CreatedBy = "System Created",
                    ModifiedDateTime = now,
                    ModifiedBy = "System Created",
                    LastSyncedDateTime = now,
                    IsDeleted = false
                };

                await _dbContext.Clients.AddAsync(newClient, cancellationToken);
                existingClients[clientData.ExternalClientId] = newClient;
                result.ClientsCreated++;
            }
        }

        // Save client changes so we have ClientIds for the new records
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Build the ExternalClientId -> internal ClientId mapping
        // Note: Client.Id is the internal ClientId (from BaseEntity)
        var mapping = existingClients.ToDictionary(
            kvp => kvp.Key,  // ExternalClientId
            kvp => kvp.Value.Id  // Internal ClientId (from BaseEntity.Id)
        );

        return mapping;
    }

    private async Task<List<ExternalAccountData>> FetchExternalAccountsAsync(
        string connectionString, 
        CancellationToken cancellationToken)
    {
        var accounts = new List<ExternalAccountData>();

        var query = GetAccountSyncQuery();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(query, connection);
        command.CommandTimeout = 300;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var account = new ExternalAccountData
            {
                VMAccountId = reader.GetInt64(reader.GetOrdinal("VMAccountId")),
                CredentialId = reader.GetInt32(reader.GetOrdinal("CredentialId")),
                ClientId = reader.IsDBNull(reader.GetOrdinal("ClientId")) ? null : reader.GetInt32(reader.GetOrdinal("ClientId")),
                ClientName = reader.IsDBNull(reader.GetOrdinal("ClientName")) ? null : reader.GetString(reader.GetOrdinal("ClientName")),
                VendorCode = reader.IsDBNull(reader.GetOrdinal("VendorCode")) ? null : reader.GetString(reader.GetOrdinal("VendorCode")),
                VMAccountNumber = reader.GetString(reader.GetOrdinal("VMAccountNumber")),
                InterfaceAccountId = reader.IsDBNull(reader.GetOrdinal("InterfaceAccountId")) ? null : reader.GetString(reader.GetOrdinal("InterfaceAccountId")),
                PeriodType = reader.IsDBNull(reader.GetOrdinal("PeriodType")) ? null : reader.GetString(reader.GetOrdinal("PeriodType")),
                PeriodDays = reader.IsDBNull(reader.GetOrdinal("PeriodDays")) ? null : reader.GetInt32(reader.GetOrdinal("PeriodDays")),
                MedianDays = reader.IsDBNull(reader.GetOrdinal("MedianDays")) ? null : reader.GetDouble(reader.GetOrdinal("MedianDays")),
                InvoiceCount = reader.GetInt32(reader.GetOrdinal("InvoiceCount")),
                LastInvoiceDateTime = reader.IsDBNull(reader.GetOrdinal("LastInvoiceDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastInvoiceDate")),
                // Read SQL-computed dates (we'll recalculate these below using calendar-based arithmetic)
                ExpectedRangeStartDateTime = reader.IsDBNull(reader.GetOrdinal("ExpectedRangeStart")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpectedRangeStart")),
                ExpectedNextDateTime = reader.IsDBNull(reader.GetOrdinal("ExpectedNextDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpectedNextDate")),
                ExpectedRangeEndDateTime = reader.IsDBNull(reader.GetOrdinal("ExpectedRangeEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("ExpectedRangeEnd")),
                NextRangeStartDateTime = reader.IsDBNull(reader.GetOrdinal("NextRangeStart")) ? null : reader.GetDateTime(reader.GetOrdinal("NextRangeStart")),
                NextRunDateTime = reader.IsDBNull(reader.GetOrdinal("NextRunDate")) ? null : reader.GetDateTime(reader.GetOrdinal("NextRunDate")),
                NextRangeEndDateTime = reader.IsDBNull(reader.GetOrdinal("NextRangeEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("NextRangeEnd")),
                DaysUntilNextRun = reader.IsDBNull(reader.GetOrdinal("DaysUntilNextRun")) ? null : reader.GetInt32(reader.GetOrdinal("DaysUntilNextRun")),
                NextRunStatus = reader.IsDBNull(reader.GetOrdinal("NextRunStatus")) ? null : reader.GetString(reader.GetOrdinal("NextRunStatus")),
                HistoricalBillingStatus = reader.IsDBNull(reader.GetOrdinal("HistoricalBillingStatus")) ? null : reader.GetString(reader.GetOrdinal("HistoricalBillingStatus"))
            };
            
            // Recalculate dates using BillingPeriodCalculator to prevent date creep
            // The SQL query uses DATEADD(DAY, PeriodDays, ...) which causes drift for month-based periods
            // We recalculate using calendar-based arithmetic (AddMonths/AddYears) with anchor day preservation
            if (account.LastInvoiceDateTime.HasValue)
            {
                var today = DateTime.UtcNow.Date;
                var anchorDayOfMonth = BillingPeriodCalculator.GetAnchorDayOfMonth(account.LastInvoiceDateTime.Value);
                var (windowBefore, windowAfter) = BillingPeriodCalculator.GetDefaultWindowDays(account.PeriodType);
                
                // Calculate expected next date using calendar-based arithmetic
                var expectedNext = BillingPeriodCalculator.CalculateNextRunDate(
                    account.PeriodType,
                    account.LastInvoiceDateTime.Value,
                    anchorDayOfMonth);
                
                // Calculate next run date (on or after today) using calendar-based arithmetic
                var nextRun = BillingPeriodCalculator.CalculateNextRunDateOnOrAfterToday(
                    account.PeriodType,
                    account.LastInvoiceDateTime.Value,
                    today,
                    anchorDayOfMonth);
                
                // Override SQL-computed dates with calendar-based calculations
                account.ExpectedNextDateTime = expectedNext;
                account.ExpectedRangeStartDateTime = expectedNext.AddDays(-windowBefore);
                account.ExpectedRangeEndDateTime = expectedNext.AddDays(windowAfter);
                account.NextRunDateTime = nextRun;
                account.NextRangeStartDateTime = nextRun.AddDays(-windowBefore);
                account.NextRangeEndDateTime = nextRun.AddDays(windowAfter);
                account.DaysUntilNextRun = (int)(nextRun - today).TotalDays;
                
                // Recalculate status based on new dates
                var periodDays = BillingPeriodCalculator.GetApproximatePeriodDays(account.PeriodType);
                var daysUntilExpected = (int)(expectedNext - today).TotalDays;
                
                // Calculate HistoricalBillingStatus based on days until expected
                var missingThreshold = -(periodDays * 2);
                if (daysUntilExpected < missingThreshold)
                    account.HistoricalBillingStatus = "Missing";
                else if (daysUntilExpected < -windowBefore)
                    account.HistoricalBillingStatus = "Overdue";
                else if (daysUntilExpected < 0)
                    account.HistoricalBillingStatus = "Due Now";
                else if (daysUntilExpected <= windowBefore)
                    account.HistoricalBillingStatus = "Due Soon";
                else if (daysUntilExpected <= 30)
                    account.HistoricalBillingStatus = "Upcoming";
                else
                    account.HistoricalBillingStatus = "Future";
                
                // NextRunStatus: If HistoricalBillingStatus is Missing, NextRunStatus should also be Missing
                if (account.HistoricalBillingStatus == "Missing")
                {
                    account.NextRunStatus = "Missing";
                }
                else
                {
                    var daysUntilRun = account.DaysUntilNextRun ?? 0;
                    if (daysUntilRun <= 0)
                        account.NextRunStatus = "Run Now";
                    else if (daysUntilRun <= windowBefore)
                        account.NextRunStatus = "Due Soon";
                    else if (daysUntilRun <= 30)
                        account.NextRunStatus = "Upcoming";
                    else
                        account.NextRunStatus = "Future";
                }
            }
            
            accounts.Add(account);
        }

        return accounts;
    }

    private static string GetAccountSyncQuery()
    {
        return @"
DROP TABLE IF EXISTS #tmpCredentialAccountBilling;

SELECT [RecId]
      ,AD.[InterfaceAccountId]
      ,[BillId]
      ,[InvoiceDate]
      ,AD.[AccountNumber]
      ,AD.[VendorCode]
      ,[VCAccountId] AS AccountId
      ,C.[CredentialId]
      ,C.ExpirationDate
      ,CL.ClientId
      ,CL.ClientName
INTO #tmpCredentialAccountBilling
FROM [dbo].[ADRInvoiceAccountData] AD
    LEFT OUTER JOIN Account A ON AD.VCAccountId = A.AccountId
    LEFT OUTER JOIN Client CL ON A.ClientId = CL.ClientId
    INNER JOIN CredentialAccount CA ON AD.VCAccountId = CA.AccountId
    INNER JOIN [Credential] C ON C.IsActive = 1 AND C.CredentialId = CA.CredentialId;

;WITH AccountStats AS (
    SELECT AccountId,
           MAX(InvoiceDate) AS LastInvoiceDate,
           COUNT(*) AS InvoiceCount
    FROM #tmpCredentialAccountBilling
    GROUP BY AccountId
),
InvoiceIntervals AS (
    SELECT AccountId,
           DATEDIFF(DAY, LAG(InvoiceDate) OVER (PARTITION BY AccountId ORDER BY InvoiceDate), InvoiceDate) AS DaysBetween
    FROM #tmpCredentialAccountBilling
),
IntervalStats AS (
    SELECT DISTINCT
        AccountId,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY DaysBetween) 
            OVER (PARTITION BY AccountId) AS MedianDays
    FROM InvoiceIntervals
    WHERE DaysBetween IS NOT NULL AND DaysBetween > 0
),
AccountPeriods AS (
    SELECT 
        a.AccountId,
        a.LastInvoiceDate,
        a.InvoiceCount,
        COALESCE(i.MedianDays, 30) AS MedianDays,
        CASE 
            WHEN i.MedianDays >= 7 AND i.MedianDays <= 21 THEN 'Bi-Weekly'
            WHEN i.MedianDays >= 22 AND i.MedianDays <= 45 THEN 'Monthly'
            WHEN i.MedianDays >= 46 AND i.MedianDays <= 75 THEN 'Bi-Monthly'
            WHEN i.MedianDays >= 76 AND i.MedianDays <= 135 THEN 'Quarterly'
            WHEN i.MedianDays >= 136 AND i.MedianDays <= 270 THEN 'Semi-Annually'
            WHEN i.MedianDays >= 271 THEN 'Annually'
            ELSE 'Monthly'
        END AS PeriodType,
        CASE 
            WHEN i.MedianDays >= 7 AND i.MedianDays <= 21 THEN 14
            WHEN i.MedianDays >= 22 AND i.MedianDays <= 45 THEN 30
            WHEN i.MedianDays >= 46 AND i.MedianDays <= 75 THEN 60
            WHEN i.MedianDays >= 76 AND i.MedianDays <= 135 THEN 90
            WHEN i.MedianDays >= 136 AND i.MedianDays <= 270 THEN 180
            WHEN i.MedianDays >= 271 THEN 365
            ELSE 30
        END AS PeriodDays,
        CASE 
            WHEN i.MedianDays >= 7 AND i.MedianDays <= 21 THEN 3
            WHEN i.MedianDays >= 22 AND i.MedianDays <= 45 THEN 5
            WHEN i.MedianDays >= 46 AND i.MedianDays <= 75 THEN 7
            WHEN i.MedianDays >= 76 AND i.MedianDays <= 135 THEN 10
            WHEN i.MedianDays >= 136 AND i.MedianDays <= 270 THEN 14
            WHEN i.MedianDays >= 271 THEN 21
            ELSE 5
        END AS WindowDays,
        DATEADD(DAY, CAST(COALESCE(i.MedianDays, 30) AS INT), a.LastInvoiceDate) AS ExpectedNextDate
    FROM AccountStats a
    LEFT JOIN IntervalStats i ON i.AccountId = a.AccountId
),
NextRunCalc AS (
    SELECT ap.*,
        CASE 
            WHEN ap.ExpectedNextDate >= CAST(GETDATE() AS DATE) THEN ap.ExpectedNextDate
            ELSE DATEADD(DAY, ((DATEDIFF(DAY, ap.ExpectedNextDate, CAST(GETDATE() AS DATE)) / ap.PeriodDays) + 1) * ap.PeriodDays, ap.ExpectedNextDate)
        END AS NextRunDate
    FROM AccountPeriods ap
),
Combined AS (
    SELECT DISTINCT 
        cab.AccountId AS VMAccountId, 
        cab.CredentialId, 
        cab.ClientId,
        cab.ClientName,
        cab.VendorCode,
        cab.AccountNumber AS VMAccountNumber, 
        cab.InterfaceAccountId,
        nr.PeriodType,
        nr.PeriodDays,
        nr.MedianDays,
        nr.InvoiceCount,
        nr.LastInvoiceDate,
        DATEADD(DAY, -nr.WindowDays, nr.ExpectedNextDate) AS ExpectedRangeStart,
        nr.ExpectedNextDate,
        DATEADD(DAY, nr.WindowDays, nr.ExpectedNextDate) AS ExpectedRangeEnd,
        DATEADD(DAY, -nr.WindowDays, nr.NextRunDate) AS NextRangeStart,
        nr.NextRunDate,
        DATEADD(DAY, nr.WindowDays, nr.NextRunDate) AS NextRangeEnd,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.NextRunDate) AS DaysUntilNextRun,
        -- Calculate HistoricalBillingStatus first (based on ExpectedNextDate)
        CASE 
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.ExpectedNextDate) < -(nr.PeriodDays * 2) THEN 'Missing'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.ExpectedNextDate) < -nr.WindowDays THEN 'Overdue'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.ExpectedNextDate) < 0 THEN 'Due Now'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.ExpectedNextDate) <= nr.WindowDays THEN 'Due Soon'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.ExpectedNextDate) <= 30 THEN 'Upcoming'
            ELSE 'Future'
        END AS HistoricalBillingStatus,
        -- NextRunStatus: If HistoricalBillingStatus is Missing, NextRunStatus should also be Missing
        -- Otherwise calculate based on NextRunDate
        CASE 
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.ExpectedNextDate) < -(nr.PeriodDays * 2) THEN 'Missing'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.NextRunDate) <= 0 THEN 'Run Now'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.NextRunDate) <= nr.WindowDays THEN 'Due Soon'
            WHEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), nr.NextRunDate) <= 30 THEN 'Upcoming'
            ELSE 'Future'
        END AS NextRunStatus
    FROM #tmpCredentialAccountBilling cab
    INNER JOIN NextRunCalc nr ON nr.AccountId = cab.AccountId
),
MaxCred AS (
    SELECT VMAccountId, VMAccountNumber, ExpectedNextDate, MAX(CredentialId) AS MaxCredentialId
    FROM Combined
    GROUP BY VMAccountId, VMAccountNumber, ExpectedNextDate
),
Filtered AS (
    SELECT c.*
    FROM Combined c
    JOIN MaxCred m ON m.VMAccountId = c.VMAccountId
                  AND m.VMAccountNumber = c.VMAccountNumber
                  AND m.ExpectedNextDate = c.ExpectedNextDate
                  AND m.MaxCredentialId = c.CredentialId
)
SELECT DISTINCT
    VMAccountId,
    CredentialId,
    ClientId,
    ClientName,
    VendorCode,
    VMAccountNumber,
    InterfaceAccountId,
    PeriodType,
    PeriodDays,
    MedianDays,
    InvoiceCount,
    LastInvoiceDate,
    ExpectedRangeStart,
    ExpectedNextDate,
    ExpectedRangeEnd,
    NextRangeStart,
    NextRunDate,
    NextRangeEnd,
    DaysUntilNextRun,
    NextRunStatus,
    HistoricalBillingStatus
FROM Filtered
ORDER BY VMAccountId;

DROP TABLE IF EXISTS #tmpCredentialAccountBilling;
";
    }

    /// <summary>
    /// Syncs AdrAccountRule records for each account.
    /// Creates new rules for accounts without them, updates existing rules (respecting override status).
    /// Rules drive the orchestrator's job creation, so they must be kept in sync with account scheduling data.
    /// </summary>
    private async Task SyncAccountRulesAsync(
        List<AdrAccount> accounts,
        List<ExternalAccountData> externalAccounts,
        AdrAccountSyncResult result,
        Action<string, int, int>? subStepCallback,
        CancellationToken cancellationToken)
    {
        var accountsToSync = accounts.Where(a => !a.IsDeleted).ToList();
        _logger.LogInformation("Starting rule sync for {Count} accounts", accountsToSync.Count);
        
        // Report sub-step start
        subStepCallback?.Invoke("Syncing rules", 0, accountsToSync.Count);

        // Build lookup from VMAccountId+VMAccountNumber to external data for scheduling fields
        var externalDataLookup = externalAccounts
            .GroupBy(e => (e.VMAccountId, e.VMAccountNumber))
            .ToDictionary(
                g => g.Key,
                g => g.First());

        // Get all account IDs that need rules
        var accountIds = accountsToSync.Select(a => a.Id).ToList();

        // Load existing rules for these accounts (JobTypeId = 2 for DownloadInvoice)
        var existingRules = await _dbContext.AdrAccountRules
            .Where(r => accountIds.Contains(r.AdrAccountId) && !r.IsDeleted && r.JobTypeId == 2)
            .ToListAsync(cancellationToken);

        var existingRulesByAccountId = existingRules
            .GroupBy(r => r.AdrAccountId)
            .ToDictionary(g => g.Key, g => g.First());

        const int batchSize = 5000;
        int processedSinceLastSave = 0;
        int batchNumber = 1;
        int totalProcessed = 0;

        foreach (var account in accountsToSync)
        {
            try
            {
                // Get external data for this account's scheduling fields
                var accountKey = (account.VMAccountId, account.VMAccountNumber ?? string.Empty);
                if (!externalDataLookup.TryGetValue(accountKey, out var externalData))
                {
                    // No external data found - skip this account
                    continue;
                }

                if (existingRulesByAccountId.TryGetValue(account.Id, out var existingRule))
                {
                    // Rule exists - update it if NOT manually overridden
                    if (existingRule.IsManuallyOverridden)
                    {
                        result.RulesSkippedOverridden++;
                        _logger.LogDebug("Skipping rule update for account {AccountId} - rule is manually overridden", account.Id);
                    }
                    else
                    {
                        // Update rule scheduling fields from external data
                        existingRule.PeriodType = externalData.PeriodType;
                        existingRule.PeriodDays = externalData.PeriodDays;
                        existingRule.NextRunDateTime = externalData.NextRunDateTime;
                        existingRule.NextRangeStartDateTime = externalData.NextRangeStartDateTime;
                        existingRule.NextRangeEndDateTime = externalData.NextRangeEndDateTime;
                        existingRule.ModifiedDateTime = DateTime.UtcNow;
                        existingRule.ModifiedBy = "System Created";
                        result.RulesUpdated++;
                    }
                }
                else
                {
                    // No rule exists - create one
                    var newRule = new AdrAccountRule
                    {
                        AdrAccountId = account.Id,
                        JobTypeId = 2, // DownloadInvoice
                        PeriodType = externalData.PeriodType,
                        PeriodDays = externalData.PeriodDays,
                        NextRunDateTime = externalData.NextRunDateTime,
                        NextRangeStartDateTime = externalData.NextRangeStartDateTime,
                        NextRangeEndDateTime = externalData.NextRangeEndDateTime,
                        IsEnabled = true,
                        IsManuallyOverridden = false,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedBy = "System Created",
                        ModifiedDateTime = DateTime.UtcNow,
                        ModifiedBy = "System Created",
                        IsDeleted = false
                    };
                    await _dbContext.AdrAccountRules.AddAsync(newRule, cancellationToken);
                    result.RulesCreated++;
                }

                processedSinceLastSave++;
                totalProcessed++;

                // Save in batches
                if (processedSinceLastSave >= batchSize)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Rule sync batch {BatchNumber} saved: {Created} created, {Updated} updated, {Skipped} skipped so far",
                        batchNumber, result.RulesCreated, result.RulesUpdated, result.RulesSkippedOverridden);
                    
                    // Report sub-step progress after each batch
                    subStepCallback?.Invoke("Syncing rules", totalProcessed, accountsToSync.Count);
                    
                    processedSinceLastSave = 0;
                    batchNumber++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing rule for account {AccountId}", account.Id);
                result.Errors++;
                result.ErrorMessages.Add($"Rule sync for AccountId {account.Id}: {ex.Message}");
                totalProcessed++;
            }
        }

        // Final save for remaining rules
        if (processedSinceLastSave > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Report final sub-step progress
            subStepCallback?.Invoke("Syncing rules", totalProcessed, accountsToSync.Count);
        }

        _logger.LogInformation("Rule sync completed. Created: {Created}, Updated: {Updated}, Skipped (overridden): {Skipped}",
            result.RulesCreated, result.RulesUpdated, result.RulesSkippedOverridden);
    }

    private void UpdateExistingAccount(AdrAccount existing, ExternalAccountData external, int? internalClientId)
    {
        // Always update identity fields (not affected by manual override)
        existing.VMAccountNumber = external.VMAccountNumber;
        existing.InterfaceAccountId = external.InterfaceAccountId;
        existing.ClientId = internalClientId;
        existing.ClientName = external.ClientName;
        existing.CredentialId = external.CredentialId;
        existing.VendorCode = external.VendorCode;
        
        // Always update historical/calculated fields (these are derived from invoice history, not scheduling config)
        // These fields stay on Account per the data model: Account = identity + historical/calculated data
        existing.MedianDays = external.MedianDays;
        existing.InvoiceCount = external.InvoiceCount;
        existing.LastInvoiceDateTime = external.LastInvoiceDateTime;
        existing.ExpectedNextDateTime = external.ExpectedNextDateTime;
        existing.ExpectedRangeStartDateTime = external.ExpectedRangeStartDateTime;
        existing.ExpectedRangeEndDateTime = external.ExpectedRangeEndDateTime;
        existing.DaysUntilNextRun = external.DaysUntilNextRun;
        existing.NextRunStatus = external.NextRunStatus;
        existing.HistoricalBillingStatus = external.HistoricalBillingStatus;
        
        // NOTE: Scheduling configuration fields (PeriodType, PeriodDays, NextRunDateTime, NextRangeStartDateTime, 
        // NextRangeEndDateTime) are now managed on AdrAccountRule, not AdrAccount.
        // The SyncAccountRulesAsync method handles syncing these fields to rules.
        
        existing.LastSyncedDateTime = DateTime.UtcNow;
        existing.ModifiedDateTime = DateTime.UtcNow;
        existing.ModifiedBy = "System Created";
    }

    private AdrAccount CreateNewAccount(ExternalAccountData external, int? internalClientId)
    {
        // NOTE: Scheduling configuration fields (PeriodType, PeriodDays, NextRunDateTime, NextRangeStartDateTime, 
        // NextRangeEndDateTime) are now managed on AdrAccountRule, not AdrAccount.
        // The SyncAccountRulesAsync method handles creating rules with these fields.
        return new AdrAccount
        {
            // Identity fields
            VMAccountId = external.VMAccountId,
            VMAccountNumber = external.VMAccountNumber,
            InterfaceAccountId = external.InterfaceAccountId,
            ClientId = internalClientId,
            ClientName = external.ClientName,
            CredentialId = external.CredentialId,
            VendorCode = external.VendorCode,
            // Historical/calculated fields (derived from invoice history)
            MedianDays = external.MedianDays,
            InvoiceCount = external.InvoiceCount,
            LastInvoiceDateTime = external.LastInvoiceDateTime,
            ExpectedNextDateTime = external.ExpectedNextDateTime,
            ExpectedRangeStartDateTime = external.ExpectedRangeStartDateTime,
            ExpectedRangeEndDateTime = external.ExpectedRangeEndDateTime,
            DaysUntilNextRun = external.DaysUntilNextRun,
            NextRunStatus = external.NextRunStatus,
            HistoricalBillingStatus = external.HistoricalBillingStatus,
            // Audit fields
            LastSyncedDateTime = DateTime.UtcNow,
            CreatedDateTime = DateTime.UtcNow,
            CreatedBy = "System Created",
            ModifiedDateTime = DateTime.UtcNow,
            ModifiedBy = "System Created",
            IsDeleted = false
        };
    }

    private class ExternalAccountData
    {
        public long VMAccountId { get; set; }
        public string VMAccountNumber { get; set; } = string.Empty;
        public string? InterfaceAccountId { get; set; }
        public int? ClientId { get; set; }
        public string? ClientName { get; set; }
        public int CredentialId { get; set; }
        public string? VendorCode { get; set; }
        public string? PeriodType { get; set; }
        public int? PeriodDays { get; set; }
        public double? MedianDays { get; set; }
        public int InvoiceCount { get; set; }
        public DateTime? LastInvoiceDateTime { get; set; }
        public DateTime? ExpectedNextDateTime { get; set; }
        public DateTime? ExpectedRangeStartDateTime { get; set; }
        public DateTime? ExpectedRangeEndDateTime { get; set; }
        public DateTime? NextRunDateTime { get; set; }
        public DateTime? NextRangeStartDateTime { get; set; }
        public DateTime? NextRangeEndDateTime { get; set; }
        public int? DaysUntilNextRun { get; set; }
        public string? NextRunStatus { get; set; }
        public string? HistoricalBillingStatus { get; set; }
    }
}
