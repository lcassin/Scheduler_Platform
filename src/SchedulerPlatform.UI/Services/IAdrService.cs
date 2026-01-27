using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public interface IAdrService
{
    // Account operations
    Task<PagedResult<AdrAccount>> GetAccountsPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? clientId = null,
        string? searchTerm = null,
        string? nextRunStatus = null,
        string? historicalBillingStatus = null,
        bool? isOverridden = null,
        string? jobStatus = null,
        string? blacklistStatus = null,
        string? primaryVendorCode = null,
        string? masterVendorCode = null,
        string? sortColumn = null,
        bool sortDescending = false);
    
    Task<List<string>> GetPrimaryVendorCodesAsync(string? searchTerm = null, int limit = 50);
    Task<List<string>> GetMasterVendorCodesAsync(string? searchTerm = null, int limit = 50);
    Task<AdrAccount?> GetAccountAsync(int id);
    Task<AdrAccount?> GetAccountByVMAccountIdAsync(long vmAccountId);
    Task<AdrAccountStats> GetAccountStatsAsync();
    Task<AdrAccount> UpdateAccountBillingAsync(int accountId, DateTime? expectedBillingDate, string? periodType, string? historicalBillingStatus);
        Task<AdrAccount> ClearAccountOverrideAsync(int accountId);
        Task<ManualScrapeResult> ManualScrapeRequestAsync(int accountId, DateTime targetDate, DateTime? rangeStartDate = null, DateTime? rangeEndDate = null, string? reason = null, bool isHighPriority = false, int requestType = 2);
        Task<byte[]> DownloadAccountsExportAsync(
        int? clientId = null,
        string? searchTerm = null,
        string? nextRunStatus = null,
        string? historicalBillingStatus = null,
        string format = "excel");
    
        // Job operations
        Task<PagedResult<AdrJob>> GetJobsPagedAsync(
            int pageNumber = 1,
            int pageSize = 20,
            int? adrAccountId = null,
            string? status = null,
            string? vendorCode = null,
            string? masterVendorCode = null,
            string? vmAccountNumber = null,
            bool latestPerAccount = false,
            long? vmAccountId = null,
            string? interfaceAccountId = null,
            int? credentialId = null,
            bool? isManualRequest = null,
            string? blacklistStatus = null,
            string? sortColumn = null,
            bool sortDescending = true);
    Task<AdrJob?> GetJobAsync(int id);
    Task<List<AdrJob>> GetJobsByAccountAsync(int adrAccountId);
    Task<AdrJobStats> GetJobStatsAsync(int? lastOrchestrationRuns = null);
    Task<byte[]> DownloadJobsExportAsync(
        string? status = null,
        string? vendorCode = null,
        string? vmAccountNumber = null,
        bool latestPerAccount = false,
        string format = "excel");
    
    // Execution operations
    Task<PagedResult<AdrJobExecution>> GetExecutionsPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? adrJobId = null);
    Task<List<AdrJobExecution>> GetExecutionsByJobAsync(int adrJobId);
    
        // Job refire operations
        Task<RefireJobResult> RefireJobAsync(int jobId, bool forceRefire = false);
        Task<RefireJobsBulkResult> RefireJobsBulkAsync(List<int> jobIds, bool forceRefire = false);
    
        // Job status check (for manual jobs)
        Task<CheckJobStatusResult> CheckJobStatusAsync(int jobId);
    
    // Orchestration operations
    Task<AdrAccountSyncResult> SyncAccountsAsync();
    Task<JobCreationResult> CreateJobsAsync();
    Task<CredentialVerificationResult> VerifyCredentialsAsync();
    Task<ScrapeResult> ProcessScrapingAsync();
    Task<StatusCheckResult> CheckStatusesAsync();
    Task<FullCycleResult> RunFullCycleAsync();
    
    // Background orchestration monitoring
    Task<BackgroundOrchestrationResponse> StartBackgroundOrchestrationAsync(BackgroundOrchestrationRequest? request = null);
    Task<OrchestrationCurrentResponse> GetCurrentOrchestrationAsync();
    Task<AdrOrchestrationStatus?> GetOrchestrationStatusAsync(string requestId);
    Task<List<AdrOrchestrationStatus>> GetOrchestrationHistoryAsync(int? count = 10);
    Task<OrchestrationHistoryPagedResponse> GetOrchestrationHistoryPagedAsync(int pageNumber = 1, int pageSize = 20);
    
    // Rule operations
    Task<AccountRuleDto?> GetRuleAsync(int ruleId);
    Task<List<AccountRuleDto>> GetRulesByAccountAsync(int accountId);
    Task<AccountRuleDto> UpdateRuleAsync(int ruleId, UpdateRuleRequest request);
    Task<AccountRuleDto> ClearRuleOverrideAsync(int ruleId);
    
    /// <summary>
    /// Cancels a running or queued orchestration request.
    /// </summary>
    Task<CancelOrchestrationResult> CancelOrchestrationAsync(string requestId);
    
    /// <summary>
    /// Gets the count of current and future blacklist entries.
    /// </summary>
    Task<BlacklistCountsResult> GetBlacklistCountsAsync();
    
    Task<byte[]> DownloadRulesExportAsync(
        string? vendorCode = null,
        string? accountNumber = null,
        bool? isEnabled = null,
        bool? isOverridden = null,
        string format = "excel");
    
    /// <summary>
    /// Gets the current test mode status from the ADR configuration.
    /// </summary>
    Task<TestModeStatus> GetTestModeStatusAsync();
    
    /// <summary>
    /// Starts a background export operation for large datasets.
    /// </summary>
    /// <param name="exportType">Type of export: accounts, jobs, rules, blacklist</param>
    /// <param name="format">Export format: excel or csv</param>
    /// <param name="filters">Optional filters to apply</param>
    /// <returns>The request ID for tracking the export</returns>
    Task<BackgroundExportStartResult> StartBackgroundExportAsync(string exportType, string format = "excel", Dictionary<string, string?>? filters = null);
    
    /// <summary>
    /// Gets the status of a background export operation.
    /// </summary>
    /// <param name="requestId">The export request ID</param>
    /// <returns>The current status of the export</returns>
    Task<BackgroundExportStatus?> GetBackgroundExportStatusAsync(string requestId);
    
    /// <summary>
    /// Downloads a completed background export.
    /// </summary>
    /// <param name="requestId">The export request ID</param>
    /// <returns>The exported file bytes</returns>
    Task<byte[]?> DownloadBackgroundExportAsync(string requestId);
}
