using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Services;

namespace SchedulerPlatform.API.Services;

public interface IAdrOrchestratorService
{
    Task<JobCreationResult> CreateJobsForDueAccountsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<CredentialVerificationResult> VerifyCredentialsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<ScrapeResult> ProcessScrapingAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<StatusCheckResult> CheckPendingStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<StatusCheckResult> CheckAllScrapedStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Finalizes stale pending jobs that missed their processing window.
    /// Jobs in Pending or CredentialCheckInProgress status with NextRangeEndDateTime in the past
    /// are marked as Cancelled and their rules are advanced to the next billing cycle.
    /// This does NOT call any ADR APIs (no cost incurred).
    /// </summary>
    Task<StalePendingJobsResult> FinalizeStalePendingJobsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Runs credential verification (AttemptLogin) for ALL active accounts in the system.
    /// This is a one-time bulk operation to check all existing credentials ahead of time.
    /// Unlike VerifyCredentialsAsync which only checks jobs approaching their NextRunDate,
    /// this method checks ALL accounts with valid CredentialIds regardless of scheduling.
    /// Note: This does NOT respect test mode limits as it's intended for one-time bulk operations.
    /// </summary>
    /// <param name="progressCallback">Optional callback to report progress (current, total)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Results of the bulk credential verification operation</returns>
    Task<BulkCredentialVerificationResult> VerifyAllAccountCredentialsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes weekly rebill checks for accounts whose expected billing day of week matches today.
    /// Rebill checks look for updated invoices, partial invoices, and off-cycle invoices.
    /// Unlike regular ADR requests, rebill checks do NOT create Zendesk tickets when no document is found
    /// (only creates tickets for credential failures).
    /// </summary>
    /// <param name="progressCallback">Optional callback to report progress (current, total)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Results of the rebill processing operation</returns>
    Task<RebillResult> ProcessRebillAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Fires a rebill check for a single account.
    /// This is used for manual rebill triggers from the API.
    /// </summary>
    /// <param name="accountId">The AdrAccount ID to fire rebill for</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result of the single account rebill operation</returns>
    Task<SingleRebillResult> FireRebillForAccountAsync(int accountId, CancellationToken cancellationToken = default);
}

#region Result Classes

public class JobCreationResult
{
    public int JobsCreated { get; set; }
    public int JobsSkipped { get; set; }
    public int BlacklistedCount { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class CredentialVerificationResult
{
    public int JobsProcessed { get; set; }
    public int CredentialsVerified { get; set; }
    public int CredentialsFailed { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class ScrapeResult
{
    public int JobsProcessed { get; set; }
    public int ScrapesRequested { get; set; }
    public int ScrapesCompleted { get; set; }
    public int ScrapesFailed { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class StatusCheckResult
{
    public int JobsChecked { get; set; }
    public int JobsCompleted { get; set; }
    public int JobsNeedingReview { get; set; }
    public int JobsStillProcessing { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class StalePendingJobsResult
{
    public int JobsFound { get; set; }
    public int JobsCancelled { get; set; }
    public int RulesAdvanced { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of bulk credential verification for all accounts.
/// </summary>
public class BulkCredentialVerificationResult
{
    /// <summary>
    /// Total number of accounts processed.
    /// </summary>
    public int AccountsProcessed { get; set; }
    
    /// <summary>
    /// Number of accounts where credentials were verified successfully.
    /// </summary>
    public int CredentialsVerified { get; set; }
    
    /// <summary>
    /// Number of accounts where credential verification failed.
    /// </summary>
    public int CredentialsFailed { get; set; }
    
    /// <summary>
    /// Number of errors encountered during processing.
    /// </summary>
    public int Errors { get; set; }
    
    /// <summary>
    /// Detailed error messages for troubleshooting.
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new();
    
    /// <summary>
    /// Total duration of the bulk verification operation.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of weekly rebill processing for accounts.
/// </summary>
public class RebillResult
{
    /// <summary>
    /// Total number of accounts processed for rebill.
    /// </summary>
    public int AccountsProcessed { get; set; }
    
    /// <summary>
    /// Number of rebill requests sent successfully.
    /// </summary>
    public int RebillRequestsSent { get; set; }
    
    /// <summary>
    /// Number of rebill requests that failed.
    /// </summary>
    public int RebillRequestsFailed { get; set; }
    
    /// <summary>
    /// Number of accounts skipped (blacklisted, no credential, etc.).
    /// </summary>
    public int AccountsSkipped { get; set; }
    
    /// <summary>
    /// Number of errors encountered during processing.
    /// </summary>
    public int Errors { get; set; }
    
    /// <summary>
    /// Detailed error messages for troubleshooting.
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new();
    
    /// <summary>
    /// Total duration of the rebill operation.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of firing a rebill for a single account.
/// </summary>
public class SingleRebillResult
{
    /// <summary>
    /// Whether the rebill request was sent successfully.
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// The ADR API IndexId returned for the request.
    /// </summary>
    public long? IndexId { get; set; }
    
    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// HTTP status code from the ADR API.
    /// </summary>
    public int? HttpStatusCode { get; set; }
}

#endregion

public class AdrOrchestratorService : IAdrOrchestratorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdrOrchestratorService> _logger;

    // Default values used when database configuration is not available
    private const int DefaultScrapeRetryDays = 5;
    private const int DefaultFollowUpDelayDays = 5;
    private const int DefaultDailyStatusCheckDelayDays = 1;  // Check status the day after scraping
    private const int DefaultMaxRetries = 5;
    private const int DefaultBatchSize = 1000; // Process and save in batches to avoid large transactions
    private const int DefaultMaxParallelRequests = 8; // Default parallel API requests

    // Cached configuration from database (loaded once per orchestration run)
    private AdrConfiguration? _cachedConfig;

    public AdrOrchestratorService(
        IUnitOfWork unitOfWork,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdrOrchestratorService> logger)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the ADR configuration from the database, falling back to appsettings.json defaults.
    /// Configuration is cached for the lifetime of this service instance.
    /// </summary>
    private async Task<AdrConfiguration> GetConfigurationAsync()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        try
        {
            // Try to load configuration from database
            var configs = await _unitOfWork.AdrConfigurations.FindAsync(c => !c.IsDeleted);
            _cachedConfig = configs.FirstOrDefault();
            
            if (_cachedConfig != null)
            {
                _logger.LogDebug("Loaded ADR configuration from database (ConfigId: {ConfigId})", _cachedConfig.Id);
                return _cachedConfig;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ADR configuration from database, using defaults");
        }

        // Return default configuration if database config not available
        _cachedConfig = new AdrConfiguration
        {
            ScrapeRetryDays = _configuration.GetValue<int>("SchedulerSettings:AdrOrchestration:ScrapeRetryDays", DefaultScrapeRetryDays),
            MaxRetries = _configuration.GetValue<int>("SchedulerSettings:AdrOrchestration:MaxRetries", DefaultMaxRetries),
            DailyStatusCheckDelayDays = _configuration.GetValue<int>("SchedulerSettings:AdrOrchestration:DailyStatusCheckDelayDays", DefaultDailyStatusCheckDelayDays),
            MaxParallelRequests = _configuration.GetValue<int>("SchedulerSettings:AdrOrchestration:MaxParallelRequests", DefaultMaxParallelRequests),
            BatchSize = _configuration.GetValue<int>("SchedulerSettings:AdrOrchestration:BatchSize", DefaultBatchSize),
            IsOrchestrationEnabled = true
        };
        
        _logger.LogDebug("Using default ADR configuration (database config not found)");
        return _cachedConfig;
    }

    /// <summary>
    /// Checks if an account is blacklisted from job creation.
    /// This method queries the database each time - use IsAccountBlacklistedCached for batch operations.
    /// </summary>
    private async Task<bool> IsAccountBlacklistedAsync(AdrAccount account, string exclusionType = "All")
    {
        try
        {
            var blacklistEntries = await LoadBlacklistEntriesAsync(exclusionType);
            return IsAccountBlacklistedCached(account, blacklistEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check blacklist for account {AccountId}, allowing job creation", account.Id);
            return false; // Allow job creation if blacklist check fails
        }
    }

    /// <summary>
    /// Loads all active blacklist entries for the given exclusion type.
    /// Call this once at the start of batch operations to avoid N database queries.
    /// </summary>
    private async Task<List<AdrAccountBlacklist>> LoadBlacklistEntriesAsync(string exclusionType = "All")
    {
        var today = DateTime.UtcNow.Date;
        var entries = await _unitOfWork.AdrAccountBlacklists.FindAsync(b =>
            !b.IsDeleted &&
            b.IsActive &&
            (b.EffectiveStartDate == null || b.EffectiveStartDate <= today) &&
            (b.EffectiveEndDate == null || b.EffectiveEndDate >= today) &&
            (b.ExclusionType == "All" || b.ExclusionType == exclusionType));
        return entries.ToList();
    }

    /// <summary>
    /// Checks if an account matches any of the cached blacklist entries.
    /// Use this for batch operations after loading entries with LoadBlacklistEntriesAsync.
    /// </summary>
    private bool IsAccountBlacklistedCached(AdrAccount account, List<AdrAccountBlacklist> blacklistEntries)
    {
        foreach (var entry in blacklistEntries)
        {
            // Check if this blacklist entry matches the account
            bool matches = false;

            // Match by PrimaryVendorCode (if specified)
            if (!string.IsNullOrEmpty(entry.PrimaryVendorCode) && entry.PrimaryVendorCode == account.PrimaryVendorCode)
                matches = true;

            // Match by MasterVendorCode (if specified)
            if (!string.IsNullOrEmpty(entry.MasterVendorCode) && entry.MasterVendorCode == account.MasterVendorCode)
                matches = true;

            // Match by VMAccountId (if specified)
            if (entry.VMAccountId.HasValue && entry.VMAccountId == account.VMAccountId)
                matches = true;

            // Match by VMAccountNumber (if specified)
            if (!string.IsNullOrEmpty(entry.VMAccountNumber) && entry.VMAccountNumber == account.VMAccountNumber)
                matches = true;

            // Match by CredentialId (if specified)
            if (entry.CredentialId.HasValue && entry.CredentialId == account.CredentialId)
                matches = true;

            if (matches)
            {
                LogDetailedInfo(
                    "Account {AccountId} (VMAccountId: {VMAccountId}, PrimaryVendorCode: {PrimaryVendorCode}) is blacklisted. Reason: {Reason}",
                    account.Id, account.VMAccountId, account.PrimaryVendorCode, entry.Reason);
                return true;
            }
        }

        return false;
    }

    // Configuration helper methods - use cached config with fallback to IConfiguration
    private int GetMaxParallelRequests()
    {
        return _cachedConfig?.MaxParallelRequests ?? 
            _configuration.GetValue<int>("SchedulerSettings:AdrOrchestration:MaxParallelRequests", DefaultMaxParallelRequests);
    }

    private int GetBatchSize()
    {
        return _cachedConfig?.BatchSize ?? DefaultBatchSize;
    }

    private int GetDailyStatusCheckDelayDays()
    {
        return _cachedConfig?.DailyStatusCheckDelayDays ?? DefaultDailyStatusCheckDelayDays;
    }

    private bool IsTestModeEnabled()
    {
        return _cachedConfig?.TestModeEnabled ?? false;
    }

    private int GetTestModeMaxScrapingJobs()
    {
        return _cachedConfig?.TestModeMaxScrapingJobs ?? 50;
    }

    private int GetTestModeMaxRebillJobs()
    {
        return _cachedConfig?.TestModeMaxRebillJobs ?? 50;
    }

    private bool IsDetailedLoggingEnabled()
    {
        return _cachedConfig?.EnableDetailedLogging ?? false;
    }

    /// <summary>
    /// Logs a message at Information level only if detailed logging is enabled.
    /// Use this for per-record logging that would otherwise bloat log files.
    /// </summary>
    private void LogDetailedInfo(string message, params object[] args)
    {
        if (IsDetailedLoggingEnabled())
        {
            _logger.LogInformation(message, args);
        }
    }

    #region Step 2: Job Creation

    public async Task<JobCreationResult> CreateJobsForDueAccountsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new JobCreationResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting job creation for due accounts");

            // Load configuration from database (cached for this orchestration run)
            var config = await GetConfigurationAsync();
            
            // Check if orchestration is enabled
            if (!config.IsOrchestrationEnabled)
            {
                _logger.LogWarning("ADR orchestration is disabled in configuration. Skipping job creation.");
                return result;
            }

            // Use the method that includes AdrAccountRules for rule tracking per BRD requirements
            // Jobs are created when NextRunDate <= today (the day scraping should start)
            var dueAccounts = await _unitOfWork.AdrAccounts.GetDueAccountsWithRulesAsync();

            int processedSinceLastSave = 0;
            int batchNumber = 1;
            int blacklistedCount = 0;
            int totalProcessed = 0;
            var dueAccountsList = dueAccounts.ToList();
            var totalAccounts = dueAccountsList.Count;
            var batchSize = config.BatchSize;
            _logger.LogInformation("Processing {Count} due accounts in batches of {BatchSize}", totalAccounts, batchSize);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, totalAccounts);

            // PERFORMANCE OPTIMIZATION: Load blacklist entries once instead of N database queries
            var blacklistEntries = await LoadBlacklistEntriesAsync("Download");
            _logger.LogDebug("Loaded {Count} active blacklist entries for job creation", blacklistEntries.Count);

            foreach (var account in dueAccountsList)
            {
                try
                {
                    // Check if account is blacklisted before creating job (using cached entries)
                    if (IsAccountBlacklistedCached(account, blacklistEntries))
                    {
                        result.JobsSkipped++;
                        blacklistedCount++;
                        continue;
                    }

                    // Get the active rule for this account (JobTypeId = 2 for DownloadInvoice/ADR Request)
                    // Rules now drive the orchestrator per BRD requirements
                    var accountRule = account.AdrAccountRules?
                        .FirstOrDefault(r => !r.IsDeleted && r.IsEnabled && r.JobTypeId == 2 &&
                            r.NextRunDateTime.HasValue && r.NextRangeStartDateTime.HasValue && r.NextRangeEndDateTime.HasValue);

                    if (accountRule == null)
                    {
                        // No valid rule found - skip this account
                        result.JobsSkipped++;
                        _logger.LogDebug("Skipping account {AccountId} - no valid enabled rule found", account.Id);
                        continue;
                    }

                    // Use scheduling data from the RULE, not the account
                    var existingJob = await _unitOfWork.AdrJobs.ExistsForBillingPeriodAsync(
                        account.Id,
                        accountRule.NextRangeStartDateTime!.Value,
                        accountRule.NextRangeEndDateTime!.Value);

                    if (existingJob)
                    {
                        result.JobsSkipped++;
                        continue;
                    }

                    // Create job using scheduling data from the RULE
                    var job = new AdrJob
                    {
                        AdrAccountId = account.Id,
                        AdrAccountRuleId = accountRule.Id,  // Track which rule created this job
                        VMAccountId = account.VMAccountId,
                        VMAccountNumber = account.VMAccountNumber,
                        PrimaryVendorCode = account.PrimaryVendorCode,
                        MasterVendorCode = account.MasterVendorCode,
                        CredentialId = account.CredentialId,
                        PeriodType = accountRule.PeriodType,  // From rule
                        BillingPeriodStartDateTime = accountRule.NextRangeStartDateTime!.Value,  // From rule
                        BillingPeriodEndDateTime = accountRule.NextRangeEndDateTime!.Value,  // From rule
                        NextRunDateTime = accountRule.NextRunDateTime,  // From rule
                        NextRangeStartDateTime = accountRule.NextRangeStartDateTime,  // From rule
                        NextRangeEndDateTime = accountRule.NextRangeEndDateTime,  // From rule
                        Status = "Pending",
                        IsMissing = account.HistoricalBillingStatus == "Missing",
                        RetryCount = 0,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedBy = "System Created",
                        ModifiedDateTime = DateTime.UtcNow,
                        ModifiedBy = "System Created"
                    };

                    await _unitOfWork.AdrJobs.AddAsync(job);
                    result.JobsCreated++;
                    processedSinceLastSave++;

                    // Save in batches to reduce transaction size
                    if (processedSinceLastSave >= batchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogDebug("Job creation batch {BatchNumber} saved: {Count} jobs created so far", 
                            batchNumber, result.JobsCreated);
                        processedSinceLastSave = 0;
                        batchNumber++;
                        
                        // Report progress after each batch save
                        totalProcessed = result.JobsCreated + result.JobsSkipped;
                        progressCallback?.Invoke(totalProcessed, totalAccounts);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating job for account {AccountId}", account.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Account {account.Id}: {ex.Message}");
                }
                
                // Track total processed for progress (created + skipped)
                totalProcessed = result.JobsCreated + result.JobsSkipped;
                
                // Report progress every 1000 accounts or at the end
                if (totalProcessed % 1000 == 0 || totalProcessed == totalAccounts)
                {
                    progressCallback?.Invoke(totalProcessed, totalAccounts);
                }
            }

            // Final save for remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            result.BlacklistedCount = blacklistedCount;
            
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Job creation completed in {Duration}. Created: {Created}, Skipped: {Skipped} (Blacklisted: {Blacklisted}), Errors: {Errors}",
                result.Duration, result.JobsCreated, result.JobsSkipped, blacklistedCount, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job creation failed");
            result.Errors++;
            result.ErrorMessages.Add($"Job creation failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Step 3: Credential Verification

    public async Task<CredentialVerificationResult> VerifyCredentialsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new CredentialVerificationResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxParallel = GetMaxParallelRequests();
        // Default lead days for credential verification (used by bulk verification endpoint)
        const int credentialCheckLeadDays = 7;

        try
        {
            _logger.LogInformation("Starting credential verification with {MaxParallel} parallel workers, {LeadDays} day lead time", 
                maxParallel, credentialCheckLeadDays);

            var jobsNeedingVerification = (await _unitOfWork.AdrJobs.GetJobsNeedingCredentialVerificationAsync(DateTime.UtcNow, credentialCheckLeadDays)).ToList();
            var totalJobsFound = jobsNeedingVerification.Count;
            _logger.LogInformation("Found {Count} jobs needing credential verification (NextRunDate within {LeadDays} days)", 
                totalJobsFound, credentialCheckLeadDays);

            // Apply test mode limit if enabled
            if (IsTestModeEnabled())
            {
                var maxJobs = GetTestModeMaxRebillJobs();
                if (maxJobs == 0)
                {
                    jobsNeedingVerification.Clear();
                    _logger.LogWarning("TEST MODE ENABLED: Skipping credential verification (max set to 0)");
                }
                else if (jobsNeedingVerification.Count > maxJobs)
                {
                    // Order by JobId for consistency - same jobs will be picked each run
                    jobsNeedingVerification = jobsNeedingVerification.OrderBy(j => j.Id).Take(maxJobs).ToList();
                    _logger.LogWarning("TEST MODE ENABLED: Limiting credential checks from {Total} to {Max} jobs (ordered by JobId for consistency)", 
                        totalJobsFound, maxJobs);
                }
            }

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsNeedingVerification.Count);

            if (!jobsNeedingVerification.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            // This prevents double-billing if the process crashes after the API call
            // Batching reduces database round-trips from 2*N to N/batchSize
            const int setupBatchSize = 500;
            var jobsToProcess = new List<(int JobId, int CredentialId, DateTime? StartDate, DateTime? EndDate, int ExecutionId, long VMAccountId, string? InterfaceAccountId)>();
            int markedCount = 0;
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogDebug("Starting to mark {Count} jobs as in-progress (batch size: {BatchSize})", 
                jobsNeedingVerification.Count, setupBatchSize);
            
            // Store execution objects (not IDs) since IDs aren't assigned until SaveChangesAsync
            var executionsByJobId = new Dictionary<int, AdrJobExecution>();
            
            foreach (var job in jobsNeedingVerification)
            {
                try
                {
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    // Calling UpdateAsync would scan all tracked entities on each iteration = O(N²)
                    job.Status = "CredentialCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.AttemptLogin, saveChanges: false);
                    executionsByJobId[job.Id] = execution;
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        _logger.LogDebug("About to save credential-check setup batch: {Marked}/{Total} jobs", 
                            markedCount, jobsNeedingVerification.Count);
                        
                        var batchSaveStart = DateTime.UtcNow;
                        await _unitOfWork.SaveChangesAsync();
                        var batchSaveDuration = (DateTime.UtcNow - batchSaveStart).TotalSeconds;
                        
                        setupProcessedSinceLastSave = 0;
                        _logger.LogDebug("Saved credential-check setup batch: {Marked}/{Total} jobs in {Duration:F1} seconds", 
                            markedCount, jobsNeedingVerification.Count, batchSaveDuration);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        // UI can show "Preparing: X / Total" instead of "Processing: X / Total"
                        progressCallback?.Invoke(-markedCount, jobsNeedingVerification.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsNeedingVerification.Count);
            }
            
            // Build jobsToProcess AFTER SaveChangesAsync so execution IDs are populated
            foreach (var job in jobsNeedingVerification)
            {
                if (executionsByJobId.TryGetValue(job.Id, out var execution))
                {
                    jobsToProcess.Add((job.Id, job.CredentialId, job.NextRangeStartDateTime, job.NextRangeEndDateTime, execution.Id, job.VMAccountId, job.AdrAccount?.InterfaceAccountId));
                }
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Marked {Count} jobs as in-progress in {Duration:F1} seconds, starting parallel API calls", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Call ADR API in parallel with semaphore to limit concurrency
            var apiResults = new ConcurrentDictionary<int, (AdrApiResult Result, int ExecutionId)>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedApiCalls = 0;
            var totalApiCalls = jobsToProcess.Count;
            
            var tasks = jobsToProcess.Select(async jobInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.AttemptLogin,
                        jobInfo.CredentialId,
                        jobInfo.StartDate,
                        jobInfo.EndDate,
                        jobInfo.JobId,
                        jobInfo.VMAccountId,
                        jobInfo.InterfaceAccountId,
                        cancellationToken);

                    apiResults[jobInfo.JobId] = (apiResult, jobInfo.ExecutionId);
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                    if (count % 500 == 0 || count == totalApiCalls)
                    {
                        _logger.LogDebug(
                            "Credential verification API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalApiCalls, (double)count / totalApiCalls * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the parallel tasks
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling ADR API for job {JobId}", jobInfo.JobId);
                    apiResults[jobInfo.JobId] = (new AdrApiResult 
                    { 
                        IsSuccess = false, 
                        IsError = true, 
                        ErrorMessage = ex.Message 
                    }, jobInfo.ExecutionId);
                    
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogDebug("Completed {Count} parallel API calls, updating job statuses", apiResults.Count);

            // Step 3: Update job statuses sequentially (EF DbContext is not thread-safe)
            // Use the job and execution objects we already have from the setup phase - no need to re-fetch
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsNeedingVerification.ToDictionary(j => j.Id);

            foreach (var jobInfo in jobsToProcess)
            {
                try
                {
                    result.JobsProcessed++;

                    if (!apiResults.TryGetValue(jobInfo.JobId, out var apiResultInfo))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: No API result found");
                        continue;
                    }

                    var (apiResult, executionId) = apiResultInfo;

                    // Update execution record - use the one from our dictionary (already tracked by EF)
                    if (executionsByJobId.TryGetValue(jobInfo.JobId, out var execution))
                    {
                        // Update execution properties directly - no need to call UpdateAsync
                        execution.EndDateTime = DateTime.UtcNow;
                        execution.HttpStatusCode = apiResult.HttpStatusCode;
                        execution.IsSuccess = apiResult.IsSuccess;
                        execution.IsError = apiResult.IsError;
                        execution.IsFinal = apiResult.IsFinal;
                        execution.AdrStatusId = apiResult.StatusId;
                        execution.AdrStatusDescription = apiResult.StatusDescription;
                        execution.ErrorMessage = apiResult.IsSuccess ? null : apiResult.ErrorMessage;
                        execution.AdrIndexId = apiResult.IndexId;
                        execution.ModifiedDateTime = DateTime.UtcNow;
                        execution.ModifiedBy = "System Created";
                    }

                    // Update job record - use the one from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobInfo.JobId, out var job))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: Job not found after API call");
                        continue;
                    }

                    if (apiResult.IsSuccess)
                    {
                        job.Status = "CredentialVerified";
                        job.CredentialVerifiedDateTime = DateTime.UtcNow;
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        job.AdrIndexId = apiResult.IndexId;
                        result.CredentialsVerified++;
                    }
                    else
                    {
                        job.Status = "CredentialFailed";
                        job.ErrorMessage = apiResult.ErrorMessage;
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        if (apiResult.IndexId.HasValue)
                        {
                            job.AdrIndexId = apiResult.IndexId;
                        }
                        job.RetryCount++;
                        result.CredentialsFailed++;
                    }

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    processedSinceLastSave++;

                    if (processedSinceLastSave >= GetBatchSize())
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogDebug("Credential verification batch {BatchNumber} saved: {Count} jobs processed so far", 
                            batchNumber, result.JobsProcessed);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating job {JobId} after API call", jobInfo.JobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobInfo.JobId}: {ex.Message}");
                }
            }

            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Credential verification completed in {Duration}. Processed: {Processed}, Verified: {Verified}, Failed: {Failed}, Errors: {Errors}",
                result.Duration, result.JobsProcessed, result.CredentialsVerified, result.CredentialsFailed, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential verification failed");
            result.Errors++;
            result.ErrorMessages.Add($"Credential verification failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Step 4: Invoice Scraping

    public async Task<ScrapeResult> ProcessScrapingAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new ScrapeResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting invoice scraping with {MaxParallel} parallel workers", maxParallel);

            var jobsReadyForScraping = (await _unitOfWork.AdrJobs.GetJobsReadyForScrapingAsync(DateTime.UtcNow)).ToList();
            var totalJobsFound = jobsReadyForScraping.Count;
            _logger.LogInformation("Found {Count} jobs ready for scraping", totalJobsFound);

            // Apply test mode limit if enabled
            if (IsTestModeEnabled())
            {
                var maxJobs = GetTestModeMaxScrapingJobs();
                if (maxJobs == 0)
                {
                    jobsReadyForScraping.Clear();
                    _logger.LogWarning("TEST MODE ENABLED: Skipping ADR scraping (max set to 0)");
                }
                else if (jobsReadyForScraping.Count > maxJobs)
                {
                    // Order by JobId for consistency - same jobs will be picked each run
                    jobsReadyForScraping = jobsReadyForScraping.OrderBy(j => j.Id).Take(maxJobs).ToList();
                    _logger.LogWarning("TEST MODE ENABLED: Limiting ADR requests from {Total} to {Max} jobs (ordered by JobId for consistency)", 
                        totalJobsFound, maxJobs);
                }
            }

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsReadyForScraping.Count);

            if (!jobsReadyForScraping.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            const int setupBatchSize = 500;
            var jobsToProcess = new List<(int JobId, int CredentialId, DateTime? StartDate, DateTime? EndDate, int ExecutionId, long VMAccountId, string? InterfaceAccountId)>();
            var executionsByJobId = new Dictionary<int, AdrJobExecution>();
            int markedCount = 0;
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogDebug("Starting to mark {Count} jobs as in-progress for scraping (batch size: {BatchSize})", 
                jobsReadyForScraping.Count, setupBatchSize);
            
            foreach (var job in jobsReadyForScraping)
            {
                try
                {
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    // Calling UpdateAsync would scan all tracked entities on each iteration = O(N²)
                    job.Status = "ScrapeInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.DownloadInvoice, saveChanges: false);
                    executionsByJobId[job.Id] = execution;
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        setupProcessedSinceLastSave = 0;
                        _logger.LogDebug("Marked {Marked}/{Total} jobs as in-progress for scraping (batch saved)", 
                            markedCount, jobsReadyForScraping.Count);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        progressCallback?.Invoke(-markedCount, jobsReadyForScraping.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress for scraping", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsReadyForScraping.Count);
            }
            
            // Build jobsToProcess AFTER SaveChangesAsync so execution IDs are populated
            foreach (var job in jobsReadyForScraping)
            {
                if (executionsByJobId.TryGetValue(job.Id, out var execution))
                {
                    jobsToProcess.Add((job.Id, job.CredentialId, job.NextRangeStartDateTime, job.NextRangeEndDateTime, execution.Id, job.VMAccountId, job.AdrAccount?.InterfaceAccountId));
                }
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Marked {Count} jobs as in-progress for scraping in {Duration:F1} seconds, starting parallel API calls", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Call ADR API in parallel with semaphore to limit concurrency
            var apiResults = new ConcurrentDictionary<int, (AdrApiResult Result, int ExecutionId)>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedApiCalls = 0;
            var totalApiCalls = jobsToProcess.Count;
            var today = DateTime.UtcNow.Date;
            
            var tasks = jobsToProcess.Select(async jobInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // IsLastAttempt is true when today is the last day of the billing window
                    // This tells the ADR API to create a Zendesk ticket if the download fails
                    var isLastAttempt = jobInfo.EndDate.HasValue && jobInfo.EndDate.Value.Date <= today;
                    
                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.DownloadInvoice,
                        jobInfo.CredentialId,
                        jobInfo.StartDate,
                        jobInfo.EndDate,
                        jobInfo.JobId,
                        jobInfo.VMAccountId,
                        jobInfo.InterfaceAccountId,
                        cancellationToken,
                        isLastAttempt);

                    apiResults[jobInfo.JobId] = (apiResult, jobInfo.ExecutionId);
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                    if (count % 500 == 0 || count == totalApiCalls)
                    {
                        _logger.LogInformation(
                            "Invoice scraping API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalApiCalls, (double)count / totalApiCalls * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the parallel tasks
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling ADR API for scraping job {JobId}", jobInfo.JobId);
                    apiResults[jobInfo.JobId] = (new AdrApiResult 
                    { 
                        IsSuccess = false, 
                        IsError = true, 
                        ErrorMessage = ex.Message 
                    }, jobInfo.ExecutionId);
                    
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogDebug("Completed {Count} parallel API calls, updating job statuses", apiResults.Count);

            // Step 3: Update job statuses sequentially (EF DbContext is not thread-safe)
            // Use the job and execution objects we already have from the setup phase - no need to re-fetch
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsReadyForScraping.ToDictionary(j => j.Id);

            foreach (var jobInfo in jobsToProcess)
            {
                try
                {
                    result.JobsProcessed++;

                    if (!apiResults.TryGetValue(jobInfo.JobId, out var apiResultInfo))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: No API result found");
                        continue;
                    }

                    var (apiResult, executionId) = apiResultInfo;

                    // Update execution record - use the one from our dictionary (already tracked by EF)
                    if (executionsByJobId.TryGetValue(jobInfo.JobId, out var execution))
                    {
                        // Update execution properties directly - no need to call UpdateAsync
                        execution.EndDateTime = DateTime.UtcNow;
                        execution.HttpStatusCode = apiResult.HttpStatusCode;
                        execution.IsSuccess = apiResult.IsSuccess;
                        execution.IsError = apiResult.IsError;
                        execution.IsFinal = apiResult.IsFinal;
                        execution.AdrStatusId = apiResult.StatusId;
                        execution.AdrStatusDescription = apiResult.StatusDescription;
                        execution.ErrorMessage = apiResult.IsSuccess ? null : apiResult.ErrorMessage;
                        execution.AdrIndexId = apiResult.IndexId;
                        execution.ModifiedDateTime = DateTime.UtcNow;
                        execution.ModifiedBy = "System Created";
                    }

                    // Update job record - use the one from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobInfo.JobId, out var job))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: Job not found after API call");
                        continue;
                    }

                    if (apiResult.IsSuccess)
                    {
                        job.Status = "ScrapeRequested";
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        job.AdrIndexId = apiResult.IndexId;
                        result.ScrapesRequested++;

                        if (apiResult.IsFinal && apiResult.StatusId == (int)AdrStatus.Complete)
                        {
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.ScrapesCompleted++;
                        }
                    }
                    else
                    {
                        job.Status = "ScrapeFailed";
                        job.ErrorMessage = apiResult.ErrorMessage;
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        if (apiResult.IndexId.HasValue)
                        {
                            job.AdrIndexId = apiResult.IndexId;
                        }
                        job.RetryCount++;
                        result.ScrapesFailed++;
                    }

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    processedSinceLastSave++;

                    if (processedSinceLastSave >= GetBatchSize())
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogDebug("Scraping batch {BatchNumber} saved: {Count} jobs processed so far", 
                            batchNumber, result.JobsProcessed);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating job {JobId} after scraping API call", jobInfo.JobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobInfo.JobId}: {ex.Message}");
                }
            }

            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Invoice scraping completed in {Duration}. Processed: {Processed}, Requested: {Requested}, Completed: {Completed}, Failed: {Failed}, Errors: {Errors}",
                result.Duration, result.JobsProcessed, result.ScrapesRequested, result.ScrapesCompleted, result.ScrapesFailed, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice scraping failed");
            result.Errors++;
            result.ErrorMessages.Add($"Invoice scraping failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Status Checking

    public async Task<StatusCheckResult> CheckPendingStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new StatusCheckResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting status check with {MaxParallel} parallel workers", maxParallel);

            // Get jobs needing daily status check (jobs that were scraped at least delayDays ago)
            var now = DateTime.UtcNow;
            var dailyDelayDays = GetDailyStatusCheckDelayDays();
            
            // Log the query parameters for debugging
            _logger.LogInformation(
                "Status check query parameters: Now={Now}, DailyDelayDays={DailyDelay}, DailyThreshold={DailyThreshold}",
                now, dailyDelayDays, now.AddDays(-dailyDelayDays).Date);
            
            var jobsNeedingStatusCheck = (await _unitOfWork.AdrJobs.GetJobsNeedingDailyStatusCheckAsync(now, dailyDelayDays)).ToList();
            
            _logger.LogInformation(
                "Status check selection: {Total} jobs need status check",
                jobsNeedingStatusCheck.Count);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsNeedingStatusCheck.Count);

            if (!jobsNeedingStatusCheck.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            // Store original status before changing it (needed to determine if this is a credential job later)
            const int setupBatchSize = 500;
            var jobsToProcess = new List<int>();
            var originalStatusByJobId = new Dictionary<int, string>();
            int markedCount = 0;
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogDebug("Starting to mark {Count} jobs as in-progress for status check (batch size: {BatchSize})", 
                jobsNeedingStatusCheck.Count, setupBatchSize);
            
            foreach (var job in jobsNeedingStatusCheck)
            {
                try
                {
                    // Store original status before changing it (needed to determine if this is a credential job later)
                    originalStatusByJobId[job.Id] = job.Status;
                    
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    // Calling UpdateAsync would scan all tracked entities on each iteration = O(N²)
                    job.Status = "StatusCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    jobsToProcess.Add(job.Id);
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        setupProcessedSinceLastSave = 0;
                        _logger.LogDebug("Marked {Marked}/{Total} jobs as in-progress for status check (batch saved)", 
                            markedCount, jobsNeedingStatusCheck.Count);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress for status check", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Marked {Count} jobs as in-progress for status check in {Duration:F1} seconds, starting parallel status checks", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Check status in parallel with semaphore to limit concurrency
            var statusResults = new ConcurrentDictionary<int, AdrApiResult?>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedStatusChecks = 0;
            var totalStatusChecks = jobsToProcess.Count;
            
            var tasks = jobsToProcess.Select(async jobId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var statusResult = await CheckJobStatusAsync(jobId, cancellationToken);
                    statusResults[jobId] = statusResult;
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedStatusChecks);
                    if (count % 50 == 0 || count == totalStatusChecks)
                    {
                        progressCallback?.Invoke(count, totalStatusChecks);
                    }
                    if (count % 500 == 0 || count == totalStatusChecks)
                    {
                        _logger.LogInformation(
                            "Status check API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalStatusChecks, (double)count / totalStatusChecks * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the parallel tasks
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking status for job {JobId}", jobId);
                    statusResults[jobId] = null;
                    var count = Interlocked.Increment(ref completedStatusChecks);
                    if (count % 50 == 0 || count == totalStatusChecks)
                    {
                        progressCallback?.Invoke(count, totalStatusChecks);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed {Count} parallel status checks, updating job statuses", statusResults.Count);

            // Step 3: Pre-load all rules that might need advancement (optimization to avoid N+1 queries)
            var ruleIdsToLoad = jobsNeedingStatusCheck
                .Where(j => j.AdrAccountRuleId.HasValue)
                .Select(j => j.AdrAccountRuleId!.Value)
                .Distinct()
                .ToList();
            
            _logger.LogDebug("Pre-loading {Count} rules for potential advancement", ruleIdsToLoad.Count);
            var rulesById = new Dictionary<int, AdrAccountRule>();
            
            // Load rules in batches to avoid huge IN clauses
            const int ruleBatchSize = 1000;
            for (int i = 0; i < ruleIdsToLoad.Count; i += ruleBatchSize)
            {
                var batchIds = ruleIdsToLoad.Skip(i).Take(ruleBatchSize).ToList();
                var batchRules = await _unitOfWork.AdrAccountRules.FindAsync(r => batchIds.Contains(r.Id) && !r.IsDeleted);
                foreach (var rule in batchRules)
                {
                    rulesById[rule.Id] = rule;
                }
            }
            _logger.LogDebug("Pre-loaded {Count} rules for advancement", rulesById.Count);

            // Step 4: Update job statuses sequentially (EF DbContext is not thread-safe)
            // Use the job objects we already have from the setup phase - no need to re-fetch
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsNeedingStatusCheck.ToDictionary(j => j.Id);

            foreach (var jobId in jobsToProcess)
            {
                try
                {
                    result.JobsChecked++;

                    if (!statusResults.TryGetValue(jobId, out var statusResult) || statusResult == null)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: No status result found");
                        continue;
                    }

                    // Use the job from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobId, out var job))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: Job not found after status check");
                        continue;
                    }

                    job.AdrStatusId = statusResult.StatusId;
                    job.AdrStatusDescription = statusResult.StatusDescription;

                    // Determine if this is a credential verification job or a scraping job
                    // Use the original status (before we changed it to StatusCheckInProgress)
                    var originalStatus = originalStatusByJobId.TryGetValue(jobId, out var origStatus) ? origStatus : job.Status;
                    var isCredentialJob = originalStatus == "CredentialCheckInProgress" || originalStatus == "CredentialFailed";
                    
                    if (isCredentialJob)
                    {
                        // Handle credential verification status check
                        if (statusResult.StatusId == (int)AdrStatus.LoginAttemptSucceeded)
                        {
                            // Credential verification succeeded
                            job.Status = "CredentialVerified";
                            job.CredentialVerifiedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                            _logger.LogInformation(
                                "Job {JobId}: Credential verification succeeded (StatusId 12)",
                                job.Id);
                        }
                        else if (statusResult.IsError || statusResult.StatusId == 3 || statusResult.StatusId == 4 || 
                                 statusResult.StatusId == 5 || statusResult.StatusId == 7 || statusResult.StatusId == 8)
                        {
                            // Credential verification failed - mark as CredentialFailed but keep checking daily
                            // (helpdesk may fix the credentials and we should re-check)
                            job.Status = "CredentialFailed";
                            result.JobsNeedingReview++;
                            _logger.LogInformation(
                                "Job {JobId}: Credential verification failed (StatusId {StatusId}: {Description}), will re-check daily",
                                job.Id, statusResult.StatusId, statusResult.StatusDescription);
                        }
                        else
                        {
                            // Still processing - keep as CredentialCheckInProgress
                            job.Status = "CredentialCheckInProgress";
                            result.JobsStillProcessing++;
                        }
                    }
                    else if (statusResult.IsFinal)
                    {
                        if (statusResult.StatusId == (int)AdrStatus.Complete)
                        {
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                            
                            // Update account's LastSuccessfulDownloadDate to help calculate next billing cycle
                            // Uses anti-creep logic: allows earlier dates but prevents late vendors from causing schedule drift
                            if (job.AdrAccount != null)
                            {
                                // Use PeriodType for calendar-based date calculation (prevents date creep)
                                job.AdrAccount.LastSuccessfulDownloadDate = CalculateLastSuccessfulDownloadDate(
                                    job.AdrAccount.LastSuccessfulDownloadDate,
                                    job.NextRunDateTime,
                                    job.PeriodType);
                                job.AdrAccount.ModifiedDateTime = DateTime.UtcNow;
                                job.AdrAccount.ModifiedBy = "System Created";
                            }
                            
                            // Advance the rule to the next billing cycle using pre-loaded rules (no DB round-trip)
                            AdvanceRuleToNextCycleSync(job, rulesById);
                        }
                        else if (statusResult.StatusId == (int)AdrStatus.NeedsHumanReview)
                        {
                            // Don't set ScrapingCompletedDateTime - NeedsReview can be fixed downstream
                            // and the job should be re-checked daily until it's resolved or window expires
                            job.Status = "NeedsReview";
                            result.JobsNeedingReview++;
                        }
                    }
                    else
                    {
                        // Job is still processing - check if billing window has been exhausted
                        var today = DateTime.UtcNow.Date;
                        var windowEnd = job.NextRangeEndDateTime?.Date ?? today;
                        
                        if (today > windowEnd)
                        {
                            // Billing window exhausted without finding a bill
                            // Mark job as NoInvoiceFound and advance rule to next cycle
                            job.Status = "NoInvoiceFound";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsNeedingReview++; // Count as needing review for reporting
                            
                            _logger.LogInformation(
                                "Job {JobId}: Billing window exhausted (ended {WindowEnd}), marking as NoInvoiceFound and advancing rule",
                                job.Id, windowEnd);
                            
                            // Advance the rule to the next billing cycle using pre-loaded rules (no DB round-trip)
                            AdvanceRuleToNextCycleSync(job, rulesById);
                        }
                        else
                        {
                            // Still within billing window - revert to ScrapeRequested so it gets picked up again
                            job.Status = "ScrapeRequested";
                            result.JobsStillProcessing++;
                        }
                    }

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    processedSinceLastSave++;

                    if (processedSinceLastSave >= GetBatchSize())
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogDebug("Status check batch {BatchNumber} saved: {Count} jobs checked so far", 
                            batchNumber, result.JobsChecked);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating job {JobId} after status check", jobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobId}: {ex.Message}");
                }
            }

            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Status check completed in {Duration}. Checked: {Checked}, Completed: {Completed}, NeedsReview: {NeedsReview}, Processing: {Processing}, Errors: {Errors}",
                result.Duration, result.JobsChecked, result.JobsCompleted, result.JobsNeedingReview, result.JobsStillProcessing, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status check failed");
            result.Errors++;
            result.ErrorMessages.Add($"Status check failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Manual status check: Checks status for ALL jobs that have been scraped, regardless of timing criteria.
    /// This is used by the "Check Statuses Only" button to check status for all ScrapeRequested jobs
    /// since there's no cost to check status from the ADR API.
    /// </summary>
    public async Task<StatusCheckResult> CheckAllScrapedStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new StatusCheckResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting MANUAL status check (all scraped jobs) with {MaxParallel} parallel workers", maxParallel);

            // Get ALL jobs that have been scraped, regardless of timing criteria
            var jobsNeedingStatusCheck = (await _unitOfWork.AdrJobs.GetAllJobsForManualStatusCheckAsync()).ToList();
            
            _logger.LogInformation(
                "Manual status check: Found {Count} jobs in ScrapeRequested/StatusCheckInProgress status",
                jobsNeedingStatusCheck.Count);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsNeedingStatusCheck.Count);

            if (!jobsNeedingStatusCheck.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            // Store original status before changing it (needed to determine if this is a credential job later)
            const int setupBatchSize = 500;
            var jobsToProcess = new List<int>();
            var originalStatusByJobId = new Dictionary<int, string>();
            int markedCount = 0;
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogDebug("Starting to mark {Count} jobs as in-progress for manual status check (batch size: {BatchSize})", 
                jobsNeedingStatusCheck.Count, setupBatchSize);
            
            foreach (var job in jobsNeedingStatusCheck)
            {
                try
                {
                    // Store original status before changing it (needed to determine if this is a credential job later)
                    originalStatusByJobId[job.Id] = job.Status;
                    
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.Status = "StatusCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    jobsToProcess.Add(job.Id);
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        setupProcessedSinceLastSave = 0;
                        _logger.LogDebug("Marked {Marked}/{Total} jobs as in-progress for manual status check (batch saved)", 
                            markedCount, jobsNeedingStatusCheck.Count);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress for manual status check", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Marked {Count} jobs as in-progress for manual status check in {Duration:F1} seconds, starting parallel status checks", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Check status in parallel with semaphore to limit concurrency
            var statusResults = new ConcurrentDictionary<int, AdrApiResult?>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedStatusChecks = 0;
            var totalStatusChecks = jobsToProcess.Count;
            
            var tasks = jobsToProcess.Select(async jobId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var statusResult = await CheckJobStatusAsync(jobId, cancellationToken);
                    statusResults[jobId] = statusResult;
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedStatusChecks);
                    if (count % 50 == 0 || count == totalStatusChecks)
                    {
                        progressCallback?.Invoke(count, totalStatusChecks);
                    }
                    if (count % 500 == 0 || count == totalStatusChecks)
                    {
                        _logger.LogInformation(
                            "Manual status check API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalStatusChecks, (double)count / totalStatusChecks * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the parallel tasks
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking status for job {JobId}", jobId);
                    statusResults[jobId] = null;
                    var count = Interlocked.Increment(ref completedStatusChecks);
                    if (count % 50 == 0 || count == totalStatusChecks)
                    {
                        progressCallback?.Invoke(count, totalStatusChecks);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed {Count} parallel manual status checks, updating job statuses", statusResults.Count);

            // Log status distribution summary for debugging
            var statusSummary = statusResults.Values
                .Where(r => r != null)
                .GroupBy(r => new { r!.StatusId, r.IsFinal, r.IsSuccess })
                .Select(g => new { g.Key.StatusId, g.Key.IsFinal, g.Key.IsSuccess, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            
            _logger.LogDebug("=== STATUS CHECK DISTRIBUTION SUMMARY ===");
            _logger.LogInformation("Expected Complete StatusId = {CompleteId}, NeedsReview StatusId = {NeedsReviewId}", 
                (int)AdrStatus.Complete, (int)AdrStatus.NeedsHumanReview);
            foreach (var entry in statusSummary)
            {
                _logger.LogInformation(
                    "StatusId={StatusId}, IsFinal={IsFinal}, IsSuccess={IsSuccess}, Count={Count}",
                    entry.StatusId, entry.IsFinal, entry.IsSuccess, entry.Count);
            }
            
            // Log a few sample raw responses for debugging
            var sampleResponses = statusResults.Values
                .Where(r => r != null && !string.IsNullOrEmpty(r!.RawResponse))
                .Take(3)
                .ToList();
            foreach (var sample in sampleResponses)
            {
                _logger.LogInformation("Sample raw response: {RawResponse}", TruncateResponse(sample!.RawResponse, 500));
            }
            _logger.LogInformation("=== END STATUS CHECK DISTRIBUTION SUMMARY ===");

            // Step 3: Pre-load all rules that might need advancement (optimization to avoid N+1 queries)
            var ruleIdsToLoad = jobsNeedingStatusCheck
                .Where(j => j.AdrAccountRuleId.HasValue)
                .Select(j => j.AdrAccountRuleId!.Value)
                .Distinct()
                .ToList();
            
            _logger.LogDebug("Pre-loading {Count} rules for potential advancement", ruleIdsToLoad.Count);
            var rulesById = new Dictionary<int, AdrAccountRule>();
            
            // Load rules in batches to avoid huge IN clauses
            const int ruleBatchSize = 1000;
            for (int i = 0; i < ruleIdsToLoad.Count; i += ruleBatchSize)
            {
                var batchIds = ruleIdsToLoad.Skip(i).Take(ruleBatchSize).ToList();
                var batchRules = await _unitOfWork.AdrAccountRules.FindAsync(r => batchIds.Contains(r.Id) && !r.IsDeleted);
                foreach (var rule in batchRules)
                {
                    rulesById[rule.Id] = rule;
                }
            }
            _logger.LogDebug("Pre-loaded {Count} rules for advancement", rulesById.Count);

            // Step 4: Update job statuses sequentially (EF DbContext is not thread-safe)
            int processedSinceLastSave = 0;
            int batchNumber = 1;
            int totalJobsToUpdate = jobsToProcess.Count;
            int jobsUpdated = 0;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsNeedingStatusCheck.ToDictionary(j => j.Id);

            foreach (var jobId in jobsToProcess)
            {
                try
                {
                    result.JobsChecked++;

                    if (!statusResults.TryGetValue(jobId, out var statusResult) || statusResult == null)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: No status result found");
                        continue;
                    }

                    // Use the job from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobId, out var job))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: Job not found after status check");
                        continue;
                    }

                    job.AdrStatusId = statusResult.StatusId;
                    job.AdrStatusDescription = statusResult.StatusDescription;
                    
                    // Store raw API response for debugging (truncated to avoid bloating the database)
                    job.LastStatusCheckResponse = TruncateResponse(statusResult.RawResponse, 1000);
                    job.LastStatusCheckDateTime = DateTime.UtcNow;

                    // Determine if this is a credential verification job or a scraping job
                    // Use the original status (before we changed it to StatusCheckInProgress)
                    var originalStatus = originalStatusByJobId.TryGetValue(jobId, out var origStatus) ? origStatus : job.Status;
                    var isCredentialJob = originalStatus == "CredentialCheckInProgress" || originalStatus == "CredentialFailed";
                    
                    if (isCredentialJob)
                    {
                        // Handle credential verification status check
                        if (statusResult.StatusId == (int)AdrStatus.LoginAttemptSucceeded)
                        {
                            // Credential verification succeeded
                            job.Status = "CredentialVerified";
                            job.CredentialVerifiedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                            _logger.LogInformation(
                                "Job {JobId}: Credential verification succeeded (StatusId 12)",
                                job.Id);
                        }
                        else if (statusResult.IsError || statusResult.StatusId == 3 || statusResult.StatusId == 4 || 
                                 statusResult.StatusId == 5 || statusResult.StatusId == 7 || statusResult.StatusId == 8)
                        {
                            // Credential verification failed - mark as CredentialFailed but keep checking daily
                            // (helpdesk may fix the credentials and we should re-check)
                            job.Status = "CredentialFailed";
                            result.JobsNeedingReview++;
                            _logger.LogInformation(
                                "Job {JobId}: Credential verification failed (StatusId {StatusId}: {Description}), will re-check daily",
                                job.Id, statusResult.StatusId, statusResult.StatusDescription);
                        }
                        else
                        {
                            // Still processing - keep as CredentialCheckInProgress
                            job.Status = "CredentialCheckInProgress";
                            result.JobsStillProcessing++;
                        }
                    }
                    else if (statusResult.IsFinal)
                    {
                        if (statusResult.StatusId == (int)AdrStatus.Complete)
                        {
                            // StatusId 11: Document Retrieval Complete
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                            
                            // Update account's LastSuccessfulDownloadDate to help calculate next billing cycle
                            // Uses anti-creep logic: allows earlier dates but prevents late vendors from causing schedule drift
                            if (job.AdrAccount != null)
                            {
                                // Use PeriodType for calendar-based date calculation (prevents date creep)
                                job.AdrAccount.LastSuccessfulDownloadDate = CalculateLastSuccessfulDownloadDate(
                                    job.AdrAccount.LastSuccessfulDownloadDate,
                                    job.NextRunDateTime,
                                    job.PeriodType);
                                job.AdrAccount.ModifiedDateTime = DateTime.UtcNow;
                                job.AdrAccount.ModifiedBy = "System Created";
                            }
                            
                            // Advance the rule to the next billing cycle using pre-loaded rules (no DB round-trip)
                            AdvanceRuleToNextCycleSync(job, rulesById);
                        }
                        else if (statusResult.StatusId == (int)AdrStatus.NeedsHumanReview)
                        {
                            // StatusId 9: Needs Human Review
                            // Don't set ScrapingCompletedDateTime - NeedsReview can be fixed downstream
                            // and the job should be re-checked daily until it's resolved or window expires
                            job.Status = "NeedsReview";
                            result.JobsNeedingReview++;
                        }
                        else if (statusResult.StatusId == 3 || statusResult.StatusId == 4 || statusResult.StatusId == 5 ||
                                 statusResult.StatusId == 7 || statusResult.StatusId == 8 || statusResult.StatusId == 14)
                        {
                            // Error statuses:
                            // StatusId 3: Invalid CredentialID
                            // StatusId 4: Cannot Connect To VCM
                            // StatusId 5: Cannot Insert Into Queue
                            // StatusId 7: Cannot Connect To AI
                            // StatusId 8: Cannot Save Result
                            // StatusId 14: Failed To Process All Documents
                            job.Status = "Failed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.Errors++;
                            result.ErrorMessages.Add($"Job {jobId}: {statusResult.StatusDescription} (StatusId={statusResult.StatusId})");
                        }
                        else
                        {
                            // Unrecognized final status - log for debugging but still mark as completed
                            _logger.LogWarning(
                                "Job {JobId}: Unrecognized final status. StatusId={StatusId}, Description={Description}, Raw={RawResponse}",
                                jobId, statusResult.StatusId, statusResult.StatusDescription, 
                                TruncateResponse(statusResult.RawResponse, 300));
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                        }
                    }
                    else
                    {
                        // Job is still processing - check if billing window has been exhausted
                        var today = DateTime.UtcNow.Date;
                        var windowEnd = job.NextRangeEndDateTime?.Date ?? today;
                        
                        if (today > windowEnd)
                        {
                            // Billing window exhausted without finding a bill
                            // Mark job as NoInvoiceFound and advance rule to next cycle
                            job.Status = "NoInvoiceFound";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsNeedingReview++; // Count as needing review for reporting
                            
                            _logger.LogInformation(
                                "Job {JobId}: Billing window exhausted (ended {WindowEnd}), marking as NoInvoiceFound and advancing rule",
                                job.Id, windowEnd);
                            
                            // Advance the rule to the next billing cycle using pre-loaded rules (no DB round-trip)
                            AdvanceRuleToNextCycleSync(job, rulesById);
                        }
                        else
                        {
                            // Still within billing window - revert to ScrapeRequested so it gets picked up again
                            job.Status = "ScrapeRequested";
                            result.JobsStillProcessing++;
                        }
                    }

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    processedSinceLastSave++;

                    if (processedSinceLastSave >= GetBatchSize())
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogDebug("Manual status check batch {BatchNumber} saved: {Count} jobs checked so far", 
                            batchNumber, result.JobsChecked);
                        
                        // Report progress for database update phase
                        // Use values < -1000000 to indicate "Updating database" phase (distinct from "Preparing" which uses small negative values)
                        progressCallback?.Invoke(-1000000 - result.JobsChecked, totalJobsToUpdate);
                        
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to stop the loop
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating job {JobId} after manual status check", jobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobId}: {ex.Message}");
                }
            }

            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                // Report final progress for database update phase
                progressCallback?.Invoke(-1000000 - result.JobsChecked, totalJobsToUpdate);
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Manual status check completed in {Duration}. Checked: {Checked}, Completed: {Completed}, NeedsReview: {NeedsReview}, Processing: {Processing}, Errors: {Errors}",
                result.Duration, result.JobsChecked, result.JobsCompleted, result.JobsNeedingReview, result.JobsStillProcessing, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual status check failed");
            result.Errors++;
            result.ErrorMessages.Add($"Manual status check failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Stale Job Finalization

    /// <summary>
    /// Finalizes stale pending jobs that missed their processing window.
    /// Jobs in Pending or CredentialCheckInProgress status with NextRangeEndDateTime in the past
    /// are marked as Cancelled and their rules are advanced to the next billing cycle.
    /// This does NOT call any ADR APIs (no cost incurred).
    /// </summary>
    public async Task<StalePendingJobsResult> FinalizeStalePendingJobsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new StalePendingJobsResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting stale pending jobs finalization");

            // Load configuration from database (cached for this orchestration run)
            var config = await GetConfigurationAsync();
            
            // Check if orchestration is enabled
            if (!config.IsOrchestrationEnabled)
            {
                _logger.LogWarning("ADR orchestration is disabled in configuration. Skipping stale job finalization.");
                return result;
            }

            // Get stale pending jobs (default 90 day lookback)
            var staleJobs = (await _unitOfWork.AdrJobs.GetStalePendingJobsAsync(DateTime.UtcNow)).ToList();
            result.JobsFound = staleJobs.Count;
            
            _logger.LogInformation("Found {Count} stale pending jobs to finalize", staleJobs.Count);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, staleJobs.Count);

            if (!staleJobs.Any())
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            int processedCount = 0;
            int processedSinceLastSave = 0;
            var batchSize = config.BatchSize;

            foreach (var job in staleJobs)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Mark job as Cancelled with reason
                    job.Status = "Cancelled";
                    job.ErrorMessage = $"Job missed processing window. Billing period ended {job.NextRangeEndDateTime:yyyy-MM-dd}. Finalized on {DateTime.UtcNow:yyyy-MM-dd}.";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    result.JobsCancelled++;

                    // Advance the rule to the next billing cycle
                    await AdvanceRuleToNextCycleAsync(job);
                    result.RulesAdvanced++;

                    processedCount++;
                    processedSinceLastSave++;

                    // Save in batches
                    if (processedSinceLastSave >= batchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogDebug("Stale job finalization batch saved: {Count}/{Total} jobs processed", 
                            processedCount, staleJobs.Count);
                        processedSinceLastSave = 0;
                        
                        // Report progress
                        progressCallback?.Invoke(processedCount, staleJobs.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error finalizing stale job {JobId}", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    processedCount++;
                }
            }

            // Final save for remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            // Report final progress
            progressCallback?.Invoke(processedCount, staleJobs.Count);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Stale pending jobs finalization completed in {Duration}. Found: {Found}, Cancelled: {Cancelled}, Rules Advanced: {RulesAdvanced}, Errors: {Errors}",
                result.Duration, result.JobsFound, result.JobsCancelled, result.RulesAdvanced, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stale pending jobs finalization failed");
            result.Errors++;
            result.ErrorMessages.Add($"Stale pending jobs finalization failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Bulk Credential Verification

    /// <summary>
    /// Runs credential verification (AttemptLogin) for ALL active accounts in the system.
    /// This is a one-time bulk operation to check all existing credentials ahead of time.
    /// Unlike VerifyCredentialsAsync which only checks jobs approaching their NextRunDate,
    /// this method checks ALL accounts with valid CredentialIds regardless of scheduling.
    /// Note: This does NOT respect test mode limits as it's intended for one-time bulk operations.
    /// </summary>
    public async Task<BulkCredentialVerificationResult> VerifyAllAccountCredentialsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new BulkCredentialVerificationResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting BULK credential verification for ALL accounts with {MaxParallel} parallel workers", maxParallel);

            // Get ALL active accounts with valid credentials (not limited by scheduling or test mode)
            var allAccounts = (await _unitOfWork.AdrAccounts.GetAllActiveAccountsForCredentialCheckAsync()).ToList();
            var totalAccounts = allAccounts.Count;
            
            _logger.LogInformation("Found {Count} active accounts with valid credentials for bulk verification", totalAccounts);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, totalAccounts);

            if (!allAccounts.Any())
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            // Create a single tracking job for this bulk run
            var firstAccount = allAccounts.First();
            var trackingJob = new AdrJob
            {
                AdrAccountId = firstAccount.Id,
                VMAccountId = firstAccount.VMAccountId,
                VMAccountNumber = firstAccount.VMAccountNumber,
                PrimaryVendorCode = firstAccount.PrimaryVendorCode,
                MasterVendorCode = firstAccount.MasterVendorCode,
                CredentialId = firstAccount.CredentialId,
                BillingPeriodStartDateTime = DateTime.UtcNow,
                BillingPeriodEndDateTime = DateTime.UtcNow,
                Status = "BulkCredentialVerification",
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = "System - Bulk Credential Verification",
                ModifiedDateTime = DateTime.UtcNow,
                ModifiedBy = "System - Bulk Credential Verification"
            };
            await _unitOfWork.AdrJobs.AddAsync(trackingJob);
            await _unitOfWork.SaveChangesAsync();
            var trackingJobId = trackingJob.Id;
            _logger.LogInformation("Created tracking job {JobId} for bulk credential verification run", trackingJobId);

            // Build list of accounts to process with their credential info
            var accountsToProcess = allAccounts
                .Select(a => (AccountId: a.Id, CredentialId: a.CredentialId, VMAccountId: a.VMAccountId, InterfaceAccountId: a.InterfaceAccountId))
                .ToList();

            _logger.LogInformation("Starting parallel API calls for {Count} accounts", accountsToProcess.Count);

            // Call ADR API in parallel with semaphore to limit concurrency
            var apiResults = new ConcurrentDictionary<int, AdrApiResult>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedApiCalls = 0;
            var totalApiCalls = accountsToProcess.Count;

            var tasks = accountsToProcess.Select(async accountInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.AttemptLogin,
                        accountInfo.CredentialId,
                        DateTime.UtcNow.Date.AddDays(-1),
                        DateTime.UtcNow.Date.AddDays(1),
                        trackingJobId,
                        accountInfo.VMAccountId,
                        accountInfo.InterfaceAccountId,
                        cancellationToken);

                    apiResults[accountInfo.AccountId] = apiResult;

                    // Log and report progress every 100 completions or at the end
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 100 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                    if (count % 1000 == 0 || count == totalApiCalls)
                    {
                        _logger.LogInformation(
                            "Bulk credential verification API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalApiCalls, (double)count / totalApiCalls * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling ADR API for account {AccountId}", accountInfo.AccountId);
                    apiResults[accountInfo.AccountId] = new AdrApiResult
                    {
                        IsSuccess = false,
                        IsError = true,
                        ErrorMessage = ex.Message
                    };

                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 100 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed {Count} parallel API calls, tallying results", apiResults.Count);

            // Tally results (no database updates needed - this is just a verification check)
            foreach (var accountInfo in accountsToProcess)
            {
                result.AccountsProcessed++;

                if (!apiResults.TryGetValue(accountInfo.AccountId, out var apiResult))
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"Account {accountInfo.AccountId}: No API result found");
                    continue;
                }

                if (apiResult.IsSuccess)
                {
                    result.CredentialsVerified++;
                }
                else if (apiResult.IsError)
                {
                    result.Errors++;
                    if (result.ErrorMessages.Count < 100)
                    {
                        var requestInfo = !string.IsNullOrEmpty(apiResult.RequestPayload) 
                            ? $" Request: {apiResult.RequestPayload}" 
                            : "";
                        result.ErrorMessages.Add($"Account {accountInfo.AccountId} (CredentialId: {accountInfo.CredentialId}): {apiResult.ErrorMessage}{requestInfo}");
                    }
                }
                else
                {
                    result.CredentialsFailed++;
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Update tracking job with final status
            trackingJob.Status = result.Errors > 0 ? "CompletedWithErrors" : "Complete";
            trackingJob.ErrorMessage = $"Processed: {result.AccountsProcessed}, Verified: {result.CredentialsVerified}, Failed: {result.CredentialsFailed}, Errors: {result.Errors}";
            trackingJob.ModifiedDateTime = DateTime.UtcNow;
            await _unitOfWork.AdrJobs.UpdateAsync(trackingJob);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "BULK credential verification completed in {Duration}. Processed: {Processed}, Verified: {Verified}, Failed: {Failed}, Errors: {Errors}, TrackingJobId: {JobId}",
                result.Duration, result.AccountsProcessed, result.CredentialsVerified, result.CredentialsFailed, result.Errors, trackingJobId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk credential verification failed");
            result.Errors++;
            result.ErrorMessages.Add($"Bulk credential verification failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Processes weekly rebill checks for accounts whose expected billing day of week matches today.
    /// Rebill checks look for updated invoices, partial invoices, and off-cycle invoices.
    /// </summary>
    public async Task<RebillResult> ProcessRebillAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new RebillResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxParallel = GetMaxParallelRequests();
        var today = DateTime.UtcNow.Date;
        var todayDayOfWeek = today.DayOfWeek;

        try
        {
            _logger.LogInformation("Starting weekly rebill processing for day of week: {DayOfWeek}", todayDayOfWeek);

            // Load blacklist entries for filtering
            var blacklistEntries = await LoadBlacklistEntriesAsync("All");

            // Get all active accounts where the day of week of ExpectedNextDateTime matches today
            // Use OverriddenDateTime if set, otherwise use ExpectedNextDateTime
            var allAccounts = await _unitOfWork.AdrAccounts.GetAllAsync();
            var accountsForRebill = allAccounts
                .Where(a => !a.IsDeleted && a.CredentialId > 0)
                .Where(a => 
                {
                    // Use the overridden date if manually set, otherwise use expected date
                    var dateToCheck = a.IsManuallyOverridden && a.OverriddenDateTime.HasValue 
                        ? a.OverriddenDateTime.Value 
                        : a.ExpectedNextDateTime;
                    
                    return dateToCheck.HasValue && dateToCheck.Value.DayOfWeek == todayDayOfWeek;
                })
                .ToList();

            var totalAccountsFound = accountsForRebill.Count;
            _logger.LogInformation("Found {Count} accounts for rebill on {DayOfWeek}", totalAccountsFound, todayDayOfWeek);

            // Apply test mode limit if enabled
            if (IsTestModeEnabled())
            {
                var maxJobs = GetTestModeMaxRebillJobs();
                if (maxJobs == 0)
                {
                    accountsForRebill.Clear();
                    _logger.LogWarning("TEST MODE ENABLED: Skipping rebill processing (max set to 0)");
                }
                else if (accountsForRebill.Count > maxJobs)
                {
                    // Order by AccountId for consistency - same accounts will be picked each run
                    accountsForRebill = accountsForRebill.OrderBy(a => a.Id).Take(maxJobs).ToList();
                    _logger.LogWarning("TEST MODE ENABLED: Limiting rebill accounts from {Total} to {Max} (ordered by AccountId for consistency)", 
                        totalAccountsFound, maxJobs);
                }
            }

            // Report initial progress
            progressCallback?.Invoke(0, accountsForRebill.Count);

            if (!accountsForRebill.Any())
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            // Filter out blacklisted accounts
            var accountsToProcess = new List<AdrAccount>();
            foreach (var account in accountsForRebill)
            {
                if (IsAccountBlacklistedCached(account, blacklistEntries))
                {
                    result.AccountsSkipped++;
                    LogDetailedInfo("Skipping blacklisted account {VMAccountId} for rebill", account.VMAccountId);
                }
                else
                {
                    accountsToProcess.Add(account);
                }
            }

            _logger.LogInformation("Processing {Count} accounts for rebill (skipped {Skipped} blacklisted)", 
                accountsToProcess.Count, result.AccountsSkipped);

            // Process rebill requests in parallel
            var apiResults = new ConcurrentDictionary<int, AdrApiResult>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedCalls = 0;
            var totalCalls = accountsToProcess.Count;

            var tasks = accountsToProcess.Select(async account =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // For rebill, we use the current billing window dates from the account
                    var startDate = account.NextRangeStartDateTime ?? account.ExpectedRangeStartDateTime;
                    var endDate = account.NextRangeEndDateTime ?? account.ExpectedRangeEndDateTime;

                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.Rebill,
                        account.CredentialId,
                        startDate,
                        endDate,
                        0, // No job ID for rebill - it's account-level
                        account.VMAccountId,
                        account.InterfaceAccountId,
                        cancellationToken,
                        false); // Rebill is never the "last attempt" - it's a weekly check

                    apiResults[account.Id] = apiResult;

                    var count = Interlocked.Increment(ref completedCalls);
                    if (count % 50 == 0 || count == totalCalls)
                    {
                        progressCallback?.Invoke(count, totalCalls);
                    }
                    if (count % 500 == 0 || count == totalCalls)
                    {
                        _logger.LogInformation(
                            "Rebill API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalCalls, (double)count / totalCalls * 100);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling ADR API for rebill account {AccountId}", account.Id);
                    apiResults[account.Id] = new AdrApiResult
                    {
                        IsSuccess = false,
                        IsError = true,
                        ErrorMessage = ex.Message
                    };

                    var count = Interlocked.Increment(ref completedCalls);
                    if (count % 50 == 0 || count == totalCalls)
                    {
                        progressCallback?.Invoke(count, totalCalls);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Process results
            foreach (var account in accountsToProcess)
            {
                result.AccountsProcessed++;

                if (apiResults.TryGetValue(account.Id, out var apiResult))
                {
                    if (apiResult.IsSuccess)
                    {
                        result.RebillRequestsSent++;
                    }
                    else if (apiResult.IsError)
                    {
                        result.RebillRequestsFailed++;
                        result.Errors++;
                        if (result.ErrorMessages.Count < 100)
                        {
                            result.ErrorMessages.Add($"Account {account.Id} (VMAccountId: {account.VMAccountId}): {apiResult.ErrorMessage}");
                        }
                    }
                    else
                    {
                        result.RebillRequestsFailed++;
                    }
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Weekly rebill processing completed in {Duration}. Processed: {Processed}, Sent: {Sent}, Failed: {Failed}, Skipped: {Skipped}, Errors: {Errors}",
                result.Duration, result.AccountsProcessed, result.RebillRequestsSent, result.RebillRequestsFailed, result.AccountsSkipped, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weekly rebill processing failed");
            result.Errors++;
            result.ErrorMessages.Add($"Rebill processing failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Fires a rebill check for a single account.
    /// </summary>
    public async Task<SingleRebillResult> FireRebillForAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var result = new SingleRebillResult();

        try
        {
            var account = await _unitOfWork.AdrAccounts.GetByIdAsync(accountId);
            if (account == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Account with ID {accountId} not found";
                return result;
            }

            if (account.IsDeleted)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Account {accountId} is deleted";
                return result;
            }

            if (account.CredentialId <= 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Account {accountId} has no valid credential";
                return result;
            }

            _logger.LogInformation("Firing rebill for account {AccountId} (VMAccountId: {VMAccountId})", accountId, account.VMAccountId);

            var startDate = account.NextRangeStartDateTime ?? account.ExpectedRangeStartDateTime;
            var endDate = account.NextRangeEndDateTime ?? account.ExpectedRangeEndDateTime;

            var apiResult = await CallAdrApiAsync(
                AdrRequestType.Rebill,
                account.CredentialId,
                startDate,
                endDate,
                0, // No job ID for manual rebill
                account.VMAccountId,
                account.InterfaceAccountId,
                cancellationToken,
                false);

            result.IsSuccess = apiResult.IsSuccess;
            result.IndexId = apiResult.IndexId;
            result.HttpStatusCode = apiResult.HttpStatusCode;
            result.ErrorMessage = apiResult.ErrorMessage;

            if (apiResult.IsSuccess)
            {
                _logger.LogInformation("Rebill request sent successfully for account {AccountId}, IndexId: {IndexId}", accountId, apiResult.IndexId);
            }
            else
            {
                _logger.LogWarning("Rebill request failed for account {AccountId}: {Error}", accountId, apiResult.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing rebill for account {AccountId}", accountId);
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<AdrJobExecution> CreateExecutionAsync(int adrJobId, int adrRequestTypeId, bool saveChanges = true)
    {
        var execution = new AdrJobExecution
        {
            AdrJobId = adrJobId,
            AdrRequestTypeId = adrRequestTypeId,
            StartDateTime = DateTime.UtcNow,
            CreatedDateTime = DateTime.UtcNow,
            CreatedBy = "System Created",
            ModifiedDateTime = DateTime.UtcNow,
            ModifiedBy = "System Created"
        };

        await _unitOfWork.AdrJobExecutions.AddAsync(execution);
        
        if (saveChanges)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return execution;
    }

    private async Task CompleteExecutionAsync(AdrJobExecution execution, AdrApiResult apiResult)
    {
        execution.EndDateTime = DateTime.UtcNow;
        execution.AdrStatusId = apiResult.StatusId;
        execution.AdrStatusDescription = apiResult.StatusDescription;
        execution.AdrIndexId = apiResult.IndexId;
        execution.HttpStatusCode = apiResult.HttpStatusCode;
        execution.IsSuccess = apiResult.IsSuccess;
        execution.IsError = apiResult.IsError;
        execution.IsFinal = apiResult.IsFinal;
        execution.ErrorMessage = apiResult.ErrorMessage;
        execution.ApiResponse = apiResult.RawResponse;
        execution.RequestPayload = apiResult.RequestPayload;
        execution.ModifiedDateTime = DateTime.UtcNow;
        execution.ModifiedBy = "System Created";

        await _unitOfWork.AdrJobExecutions.UpdateAsync(execution);
    }

    private async Task<AdrApiResult> CallAdrApiAsync(
        AdrRequestType requestType,
        int credentialId,
        DateTime? startDate,
        DateTime? endDate,
        int jobId,
        long vmAccountId,
        string? interfaceAccountId,
        CancellationToken cancellationToken,
        bool isLastAttempt = false)
    {
        var result = new AdrApiResult();

        try
        {
            var baseUrl = _configuration["SchedulerSettings:AdrApi:BaseUrl"] ?? "https://nuscetsadrdevfn01.azurewebsites.net/api/";
            var sourceApplicationName = _configuration["SchedulerSettings:AdrApi:SourceApplicationName"] ?? "ADRScheduler";
            var recipientEmail = _configuration["SchedulerSettings:AdrApi:RecipientEmail"] ?? "lcassin@cassinfo.com";

            var client = _httpClientFactory.CreateClient("AdrApi");

            var request = new
            {
                ADRRequestTypeId = (int)requestType,
                CredentialId = credentialId,
                StartDate = startDate?.ToString("yyyy-MM-dd") ?? "",
                EndDate = endDate?.ToString("yyyy-MM-dd") ?? "",
                SourceApplicationName = sourceApplicationName,
                RecipientEmail = recipientEmail,
                JobId = jobId,
                AccountId = vmAccountId,
                InterfaceAccountId = interfaceAccountId,
                IsLastAttempt = isLastAttempt
            };

            // Store the request payload for debugging/diagnostics
            result.RequestPayload = JsonSerializer.Serialize(request);

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}IngestAdrRequest",
                request,
                cancellationToken);

            result.HttpStatusCode = (int)response.StatusCode;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = responseContent;

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogWarning("ADR API returned empty response for job {JobId}", jobId);
                    result.IsSuccess = true;
                    result.IsError = false;
                    result.StatusDescription = "ADR API returned no content.";
                }
                else
                {
                    try
                    {
                        var trimmed = responseContent.TrimStart();
                        if (trimmed.StartsWith("{"))
                        {
                            var apiResponse = JsonSerializer.Deserialize<AdrApiResponse>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (apiResponse != null)
                            {
                                result.StatusId = apiResponse.StatusId;
                                result.StatusDescription = apiResponse.StatusDescription;
                                result.IndexId = apiResponse.IndexId;
                                result.IsSuccess = true;
                                result.IsError = apiResponse.IsError;
                                result.IsFinal = apiResponse.IsFinal;
                            }
                            else
                            {
                                result.IsSuccess = false;
                                result.IsError = true;
                                result.ErrorMessage = "ADR API returned an empty JSON object.";
                            }
                        }
                        else if (trimmed.StartsWith("["))
                        {
                            var list = JsonSerializer.Deserialize<List<AdrApiResponse>>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            var apiResponse = list?.FirstOrDefault();
                            if (apiResponse != null)
                            {
                                result.StatusId = apiResponse.StatusId;
                                result.StatusDescription = apiResponse.StatusDescription;
                                result.IndexId = apiResponse.IndexId;
                                result.IsSuccess = true;
                                result.IsError = apiResponse.IsError;
                                result.IsFinal = apiResponse.IsFinal;
                                LogDetailedInfo("ADR API returned array response for job {JobId}, using first element", jobId);
                            }
                            else
                            {
                                result.IsSuccess = false;
                                result.IsError = true;
                                result.ErrorMessage = "ADR API returned an empty JSON array.";
                            }
                        }
                        else if (long.TryParse(trimmed, out var indexId))
                        {
                            // API returned just a number - this is the IndexId
                            result.IndexId = indexId;
                            result.IsSuccess = true;
                            result.IsError = false;
                            result.StatusDescription = "Request submitted successfully";
                            LogDetailedInfo("ADR API returned IndexId {IndexId} for job {JobId}", indexId, jobId);
                        }
                        else
                        {
                            _logger.LogWarning("ADR API returned unexpected content for job {JobId}: {Response}", 
                                jobId, TruncateResponse(responseContent));
                            result.IsSuccess = false;
                            result.IsError = true;
                            result.ErrorMessage = $"ADR API returned unexpected content: {TruncateResponse(responseContent)}";
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Error deserializing ADR API response for job {JobId}: {Response}", 
                            jobId, TruncateResponse(responseContent));
                        result.IsSuccess = false;
                        result.IsError = true;
                        result.ErrorMessage = $"Error deserializing ADR API response: {jsonEx.Message}. Raw: {TruncateResponse(responseContent)}";
                    }
                }
            }
            else
            {
                // 4XX errors may still contain an IndexId if the record was created
                // but credential verification or enqueuing failed
                result.IsSuccess = false;
                result.IsError = true;
                result.ErrorMessage = $"API returned {response.StatusCode}: {TruncateResponse(responseContent)}";
                
                // Try to extract IndexId from error response if present
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        // Try to parse as JSON object with IndexId
                        var trimmed = responseContent.TrimStart();
                        if (trimmed.StartsWith("{"))
                        {
                            using var doc = JsonDocument.Parse(responseContent);
                            if (doc.RootElement.TryGetProperty("indexId", out var indexIdProp) ||
                                doc.RootElement.TryGetProperty("IndexId", out indexIdProp))
                            {
                                if (indexIdProp.TryGetInt64(out var indexId))
                                {
                                    result.IndexId = indexId;
                                    _logger.LogWarning("ADR API returned error with IndexId {IndexId} for job {JobId}: {Error}", 
                                        indexId, jobId, result.ErrorMessage);
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore JSON parsing errors for error responses
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ADR API for job {JobId}", jobId);
            result.IsSuccess = false;
            result.IsError = true;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<AdrApiResult?> CheckJobStatusAsync(int jobId, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = _configuration["SchedulerSettings:AdrApi:BaseUrl"] ?? "https://nuscetsadrdevfn01.azurewebsites.net/api/";

            var client = _httpClientFactory.CreateClient("AdrApi");

            var response = await client.GetAsync(
                $"{baseUrl}GetRequestStatusByJobId/{jobId}",
                cancellationToken);

            var result = new AdrApiResult
            {
                HttpStatusCode = (int)response.StatusCode
            };

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = responseContent;

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<AdrApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse != null)
                    {
                        result.StatusId = apiResponse.StatusId;
                        // Use Status field if StatusDescription is not provided
                        result.StatusDescription = apiResponse.StatusDescription ?? apiResponse.Status;
                        result.IndexId = apiResponse.IndexId;
                        result.IsSuccess = true;
                        result.IsError = apiResponse.IsError;
                        
                        // Determine IsFinal based on StatusId since the API may not return IsFinal field
                        // Final statuses: Complete (11), NeedsHumanReview (9), and error states like "Cannot Insert Into Queue" (5)
                        result.IsFinal = apiResponse.IsFinal || IsFinalStatus(apiResponse.StatusId);
                    }
                    else
                    {
                        // Log when deserialization returns null - may indicate API format change
                        _logger.LogWarning(
                            "Status check for job {JobId}: Deserialization returned null. Raw response: {RawResponse}",
                            jobId, TruncateResponse(responseContent, 500));
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Error deserializing status check response for job {JobId}: {Response}", 
                        jobId, TruncateResponse(responseContent, 500));
                    result.IsSuccess = false;
                    result.IsError = true;
                    result.ErrorMessage = $"Failed to parse status check response: {TruncateResponse(responseContent, 200)}";
                }
            }
            else
            {
                // Log non-success HTTP responses
                _logger.LogWarning(
                    "Status check for job {JobId}: HTTP {StatusCode}. Raw response: {RawResponse}",
                    jobId, (int)response.StatusCode, TruncateResponse(responseContent, 500));
                result.IsSuccess = false;
                result.IsError = true;
                result.ErrorMessage = $"Status check returned HTTP {(int)response.StatusCode}: {TruncateResponse(responseContent, 200)}";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking job status for job {JobId}", jobId);
            return null;
        }
    }

    private static string TruncateResponse(string? response, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(response))
            return string.Empty;
        return response.Length <= maxLength ? response : response.Substring(0, maxLength) + "...";
    }
    
    /// <summary>
    /// Determines if a StatusId represents a final state (job is done processing).
    /// Based on ADR API status codes:
    /// - Final success: 11 (Complete/Document Retrieval Complete)
    /// - Final needs review: 9 (Needs Human Review)
    /// - Final errors: 3 (Invalid CredentialID), 4 (Cannot Connect To VCM), 5 (Cannot Insert Into Queue),
    ///                 7 (Cannot Connect To AI), 8 (Cannot Save Result), 14 (Failed To Process All Documents)
    /// - Still processing: 1 (Inserted), 2 (Inserted With Priority), 6 (Sent To AI), 10 (Received From AI),
    ///                     12 (Login Attempt Succeeded), 13 (No Documents Found), 15 (No Documents Processed - TBD)
    /// </summary>
    private static bool IsFinalStatus(int statusId)
    {
        return statusId switch
        {
            11 => true,  // Complete (Document Retrieval Complete)
            9 => true,   // Needs Human Review
            3 => true,   // Invalid CredentialID (error - final)
            4 => true,   // Cannot Connect To VCM (error - final)
            5 => true,   // Cannot Insert Into Queue (error - final)
            7 => true,   // Cannot Connect To AI (error - final)
            8 => true,   // Cannot Save Result (error - final)
            14 => true,  // Failed To Process All Documents (error - final)
            _ => false   // 1 (Inserted), 2 (Inserted With Priority), 6 (Sent To AI), 10 (Received From AI),
                         // 12 (Login Attempt Succeeded), 13 (No Documents Found - retry next day), 15 (No Documents Processed - TBD)
        };
    }

    /// <summary>
    /// Calculates the LastSuccessfulDownloadDate with anti-creep logic.
    /// - If no previous date exists, use the job's scheduled date (establish baseline)
    /// - If job date is earlier or equal to expected, use job date (allow earlier)
    /// - If job date is later than expected (vendor posted late), use expected date (prevent creep)
    /// Uses BillingPeriodCalculator for calendar-based date arithmetic to prevent drift.
    /// </summary>
    private static DateTime CalculateLastSuccessfulDownloadDate(
        DateTime? currentLastSuccessfulDownloadDate,
        DateTime? jobNextRunDateTime,
        string? periodType)
    {
        var jobDate = jobNextRunDateTime?.Date ?? DateTime.UtcNow.Date;
        
        // First successful download - establish baseline
        if (!currentLastSuccessfulDownloadDate.HasValue)
        {
            return jobDate;
        }
        
        // Calculate expected date based on previous anchor + period using calendar arithmetic
        var previousAnchor = currentLastSuccessfulDownloadDate.Value;
        var anchorDayOfMonth = BillingPeriodCalculator.GetAnchorDayOfMonth(previousAnchor);
        var expectedDate = BillingPeriodCalculator.CalculateNextRunDate(periodType, previousAnchor, anchorDayOfMonth);
        
        // Allow earlier or same, but don't let late vendors cause creep
        if (jobDate <= expectedDate)
        {
            return jobDate; // OK to move earlier or keep same
        }
        else
        {
            return expectedDate; // Vendor late - use expected date to prevent creep
        }
    }

    /// <summary>
    /// Advances the AdrAccountRule to the next billing cycle after a successful job completion.
    /// Uses the job's NextRunDateTime (not status check date) to avoid scheduling creep.
    /// Uses BillingPeriodCalculator for calendar-based date arithmetic (AddMonths/AddYears)
    /// to prevent date drift over time.
    /// Preserves the current window offsets (days before/after NextRunDateTime) to maintain
    /// any manual adjustments to the search window size.
    /// </summary>
    private async Task AdvanceRuleToNextCycleAsync(AdrJob job) => 
        AdvanceRuleToNextCycleSync(job, null);

    /// <summary>
    /// Synchronous version of AdvanceRuleToNextCycleAsync that uses pre-loaded rules dictionary.
    /// This avoids N+1 database queries when processing many jobs.
    /// </summary>
    private void AdvanceRuleToNextCycleSync(AdrJob job, Dictionary<int, AdrAccountRule>? preloadedRules)
    {
        if (!job.AdrAccountRuleId.HasValue)
        {
            _logger.LogWarning("Job {JobId} has no AdrAccountRuleId - cannot advance rule", job.Id);
            return;
        }

        try
        {
            // Use pre-loaded rule if available, otherwise skip (rule will be updated on next full orchestration)
            AdrAccountRule? rule = null;
            if (preloadedRules != null)
            {
                preloadedRules.TryGetValue(job.AdrAccountRuleId.Value, out rule);
            }
            
            if (rule == null || rule.IsDeleted)
            {
                if (preloadedRules != null)
                {
                    _logger.LogWarning("Rule {RuleId} not found in preloaded rules or deleted for job {JobId}", job.AdrAccountRuleId, job.Id);
                }
                return;
            }

            // Use job's NextRunDateTime as the anchor (not status check date) to avoid creep
            var anchorDate = job.NextRunDateTime?.Date ?? DateTime.UtcNow.Date;
            
            // Get the anchor day of month to preserve across billing cycles
            // This prevents "sticky" drift after short months (e.g., Jan 31 -> Feb 28 -> Mar 28)
            var anchorDayOfMonth = BillingPeriodCalculator.GetAnchorDayOfMonth(anchorDate);

            // Calculate next billing cycle run date using calendar-based arithmetic
            var nextRunDate = BillingPeriodCalculator.CalculateNextRunDate(
                rule.PeriodType, 
                anchorDate, 
                anchorDayOfMonth);

            // Preserve the current window offsets (days before/after NextRunDateTime)
            // This maintains any manual adjustments to narrow or widen the search window
            int windowBefore;
            int windowAfter;
            
            if (rule.NextRunDateTime.HasValue && rule.NextRangeStartDateTime.HasValue && rule.NextRangeEndDateTime.HasValue)
            {
                // Calculate offsets from the current rule's dates to preserve manual adjustments
                windowBefore = (int)(rule.NextRunDateTime.Value.Date - rule.NextRangeStartDateTime.Value.Date).TotalDays;
                windowAfter = (int)(rule.NextRangeEndDateTime.Value.Date - rule.NextRunDateTime.Value.Date).TotalDays;
                
                // Sanity check - if offsets are negative or unreasonable, fall back to stored/default values
                if (windowBefore < 0 || windowAfter < 0 || windowBefore > 365 || windowAfter > 365)
                {
                    var defaultWindow = BillingPeriodCalculator.GetDefaultWindowDays(rule.PeriodType);
                    windowBefore = rule.WindowDaysBefore ?? defaultWindow.Before;
                    windowAfter = rule.WindowDaysAfter ?? defaultWindow.After;
                    _logger.LogWarning(
                        "Rule {RuleId} had invalid window offsets, falling back to WindowDaysBefore={Before}/WindowDaysAfter={After}",
                        rule.Id, windowBefore, windowAfter);
                }
            }
            else
            {
                // No existing dates - use stored window values or defaults based on period type
                var defaultWindow = BillingPeriodCalculator.GetDefaultWindowDays(rule.PeriodType);
                windowBefore = rule.WindowDaysBefore ?? defaultWindow.Before;
                windowAfter = rule.WindowDaysAfter ?? defaultWindow.After;
            }

            var (nextRangeStart, nextRangeEnd) = BillingPeriodCalculator.CalculateBillingWindow(
                nextRunDate, windowBefore, windowAfter);

            // Update rule with next cycle dates (rule is already tracked by EF, will be saved in batch)
            rule.NextRunDateTime = nextRunDate;
            rule.NextRangeStartDateTime = nextRangeStart;
            rule.NextRangeEndDateTime = nextRangeEnd;

            // Note: We do NOT clear IsManuallyOverridden here because:
            // 1. We can't distinguish between date-only overrides and PeriodType overrides
            // 2. Per BRD, PeriodType overrides should persist
            // 3. The window offsets we just preserved may have been manually set
            // The override flag will remain set, preserving any manual adjustments

            rule.ModifiedDateTime = DateTime.UtcNow;
            rule.ModifiedBy = "System Created";

            // IMPORTANT: Also update the AdrAccount's scheduling fields to keep them in sync with the rule
            // This ensures the UI displays the correct NextRunDateTime and NextRunStatus
            // The Account fields were previously only updated during sync from VendorCred, causing stale data
            if (job.AdrAccount != null)
            {
                var today = DateTime.UtcNow.Date;
                var daysUntilNextRun = (int)(nextRunDate - today).TotalDays;
                
                // Update scheduling fields on the account
                job.AdrAccount.NextRunDateTime = nextRunDate;
                job.AdrAccount.NextRangeStartDateTime = nextRangeStart;
                job.AdrAccount.NextRangeEndDateTime = nextRangeEnd;
                job.AdrAccount.DaysUntilNextRun = daysUntilNextRun;
                
                // Recalculate NextRunStatus based on the new dates
                // Use the same logic as in AdrAccountSyncService
                if (job.AdrAccount.HistoricalBillingStatus == "Missing")
                {
                    job.AdrAccount.NextRunStatus = "Missing";
                }
                else
                {
                    if (daysUntilNextRun <= 0)
                        job.AdrAccount.NextRunStatus = "Run Now";
                    else if (daysUntilNextRun <= windowBefore)
                        job.AdrAccount.NextRunStatus = "Due Soon";
                    else if (daysUntilNextRun <= 30)
                        job.AdrAccount.NextRunStatus = "Upcoming";
                    else
                        job.AdrAccount.NextRunStatus = "Future";
                }
                
                job.AdrAccount.ModifiedDateTime = DateTime.UtcNow;
                job.AdrAccount.ModifiedBy = "System Created";
                
                _logger.LogDebug(
                    "Updated account {AccountId} scheduling fields: NextRunDateTime={NextRun}, NextRunStatus={Status}, DaysUntilNextRun={Days}",
                    job.AdrAccount.Id, nextRunDate, job.AdrAccount.NextRunStatus, daysUntilNextRun);
            }

            // Note: No UpdateAsync call here - the rule and account are already tracked by EF and will be saved
            // in the batch SaveChangesAsync call in the calling method

            _logger.LogInformation(
                "Advanced rule {RuleId} to next cycle using calendar arithmetic: PeriodType={PeriodType}, NextRunDateTime={NextRun}, NextRangeStart={RangeStart}, NextRangeEnd={RangeEnd} (window: -{Before}/+{After} days)",
                rule.Id, rule.PeriodType, nextRunDate, nextRangeStart, nextRangeEnd, windowBefore, windowAfter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing rule {RuleId} for job {JobId}", job.AdrAccountRuleId, job.Id);
            // Don't throw - rule advancement failure shouldn't fail the status check
        }
    }

    #endregion

    #region Private Classes

    private class AdrApiResult
    {
        public int? StatusId { get; set; }
        public string? StatusDescription { get; set; }
        public long? IndexId { get; set; }
        public int? HttpStatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsError { get; set; }
        public bool IsFinal { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RawResponse { get; set; }
        public string? RequestPayload { get; set; }
    }

    private class AdrApiResponse
    {
        public int StatusId { get; set; }
        public string? StatusDescription { get; set; }
        public string? Status { get; set; }  // API returns "Status" field, not "StatusDescription"
        public long? IndexId { get; set; }
        public bool IsError { get; set; }
        public bool IsFinal { get; set; }
    }

    #endregion
}
