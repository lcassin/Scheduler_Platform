using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;
using System.Data;

namespace SchedulerPlatform.API.Services;

public interface IAdrAccountSyncService
{
    Task<AdrAccountSyncResult> SyncAccountsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
}

public class AdrAccountSyncResult
{
    public int TotalAccountsProcessed { get; set; }
    public int AccountsInserted { get; set; }
    public int AccountsUpdated { get; set; }
    public int AccountsMarkedDeleted { get; set; }
    public int ClientsCreated { get; set; }
    public int ClientsUpdated { get; set; }
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

    public async Task<AdrAccountSyncResult> SyncAccountsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
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

            // Step 1: Fetch external accounts from VendorCred
            var externalAccounts = await FetchExternalAccountsAsync(externalConnectionString, cancellationToken);
            _logger.LogInformation("Fetched {Count} accounts from external database", externalAccounts.Count);
            
            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, externalAccounts.Count);

            // Step 2: Sync Clients - create/update Client records based on ExternalClientId
            var externalClientIdToInternalClientId = await SyncClientsAsync(externalAccounts, result, cancellationToken);
            _logger.LogInformation("Client sync complete. Created: {Created}, Updated: {Updated}", 
                result.ClientsCreated, result.ClientsUpdated);

            // Step 3: Sync AdrAccounts using the ExternalClientId -> internal ClientId mapping
            // Process in batches to avoid large transactions and memory pressure
            const int batchSize = 5000;
            
            // Use VMAccountId + VMAccountNumber as composite key since VMAccountId can have multiple account numbers
            // (historical account number changes in source system)
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
                        // Use most recently modified
                        return g
                            .OrderByDescending(a => a.ModifiedDateTime)
                            .First();
                    });

            // Track processed accounts by composite key (VMAccountId + VMAccountNumber)
            var processedAccountKeys = new HashSet<(long VMAccountId, string VMAccountNumber)>();
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            _logger.LogInformation("Processing {Count} accounts in batches of {BatchSize}", externalAccounts.Count, batchSize);

            foreach (var externalAccount in externalAccounts)
            {
                try
                {
                    var accountKey = (externalAccount.VMAccountId, externalAccount.VMAccountNumber);
                    processedAccountKeys.Add(accountKey);

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
                        existingAccounts[accountKey] = newAccount; // Track for future lookups
                        result.AccountsInserted++;
                    }

                    result.TotalAccountsProcessed++;
                    processedSinceLastSave++;

                    // Save in batches to reduce transaction size and memory pressure
                    if (processedSinceLastSave >= batchSize)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Batch {BatchNumber} saved: {Count} accounts processed so far", 
                            batchNumber, result.TotalAccountsProcessed);
                        
                        // Report progress after each batch
                        progressCallback?.Invoke(result.TotalAccountsProcessed, externalAccounts.Count);
                        
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the sync loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing account VMAccountId={VMAccountId}", externalAccount.VMAccountId);
                    result.Errors++;
                    result.ErrorMessages.Add($"VMAccountId {externalAccount.VMAccountId}: {ex.Message}");
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

                    // Also batch the deletion updates
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
            
            // Report final progress (100%)
            progressCallback?.Invoke(externalAccounts.Count, externalAccounts.Count);

            result.SyncEndDateTime = DateTime.UtcNow;
            _logger.LogInformation(
                "ADR account sync completed. Clients: {ClientsCreated} created/{ClientsUpdated} updated. Accounts: {Processed} processed, {Inserted} inserted, {Updated} updated, {Deleted} deleted, {Errors} errors. Duration: {Duration}",
                result.ClientsCreated, result.ClientsUpdated, result.TotalAccountsProcessed, result.AccountsInserted, 
                result.AccountsUpdated, result.AccountsMarkedDeleted, result.Errors, result.Duration);

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
            accounts.Add(new ExternalAccountData
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
            });
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

    private void UpdateExistingAccount(AdrAccount existing, ExternalAccountData external, int? internalClientId)
    {
        // Always update these fields (not affected by manual override)
        existing.VMAccountNumber = external.VMAccountNumber;
        existing.InterfaceAccountId = external.InterfaceAccountId;
        existing.ClientId = internalClientId;
        existing.ClientName = external.ClientName;
        existing.CredentialId = external.CredentialId;
        existing.VendorCode = external.VendorCode;
        
        // Only update billing-related fields if NOT manually overridden
        // When IsManuallyOverridden = true, preserve the manually set values
        if (!existing.IsManuallyOverridden)
        {
            existing.PeriodType = external.PeriodType;
            existing.PeriodDays = external.PeriodDays;
            existing.MedianDays = external.MedianDays;
            existing.InvoiceCount = external.InvoiceCount;
            existing.LastInvoiceDateTime = external.LastInvoiceDateTime;
            existing.ExpectedNextDateTime = external.ExpectedNextDateTime;
            existing.ExpectedRangeStartDateTime = external.ExpectedRangeStartDateTime;
            existing.ExpectedRangeEndDateTime = external.ExpectedRangeEndDateTime;
            existing.NextRunDateTime = external.NextRunDateTime;
            existing.NextRangeStartDateTime = external.NextRangeStartDateTime;
            existing.NextRangeEndDateTime = external.NextRangeEndDateTime;
            existing.DaysUntilNextRun = external.DaysUntilNextRun;
            existing.NextRunStatus = external.NextRunStatus;
            existing.HistoricalBillingStatus = external.HistoricalBillingStatus;
        }
        
        existing.LastSyncedDateTime = DateTime.UtcNow;
        existing.ModifiedDateTime = DateTime.UtcNow;
        existing.ModifiedBy = "System Created";
    }

    private AdrAccount CreateNewAccount(ExternalAccountData external, int? internalClientId)
    {
        return new AdrAccount
        {
            VMAccountId = external.VMAccountId,
            VMAccountNumber = external.VMAccountNumber,
            InterfaceAccountId = external.InterfaceAccountId,
            ClientId = internalClientId,
            ClientName = external.ClientName,
            CredentialId = external.CredentialId,
            VendorCode = external.VendorCode,
            PeriodType = external.PeriodType,
            PeriodDays = external.PeriodDays,
            MedianDays = external.MedianDays,
            InvoiceCount = external.InvoiceCount,
            LastInvoiceDateTime = external.LastInvoiceDateTime,
            ExpectedNextDateTime = external.ExpectedNextDateTime,
            ExpectedRangeStartDateTime = external.ExpectedRangeStartDateTime,
            ExpectedRangeEndDateTime = external.ExpectedRangeEndDateTime,
            NextRunDateTime = external.NextRunDateTime,
            NextRangeStartDateTime = external.NextRangeStartDateTime,
            NextRangeEndDateTime = external.NextRangeEndDateTime,
            DaysUntilNextRun = external.DaysUntilNextRun,
            NextRunStatus = external.NextRunStatus,
            HistoricalBillingStatus = external.HistoricalBillingStatus,
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
