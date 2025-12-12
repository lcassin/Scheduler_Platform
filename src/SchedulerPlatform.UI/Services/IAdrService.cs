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
        string? sortColumn = null,
        bool sortDescending = false);
    Task<AdrAccount?> GetAccountAsync(int id);
    Task<AdrAccount?> GetAccountByVMAccountIdAsync(long vmAccountId);
    Task<AdrAccountStats> GetAccountStatsAsync();
    Task<AdrAccount> UpdateAccountBillingAsync(int accountId, DateTime? expectedBillingDate, string? periodType, string? historicalBillingStatus);
        Task<AdrAccount> ClearAccountOverrideAsync(int accountId);
        Task<ManualScrapeResult> ManualScrapeRequestAsync(int accountId, DateTime targetDate, DateTime? rangeStartDate = null, DateTime? rangeEndDate = null, string? reason = null);
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
            string? vmAccountNumber = null,
            bool latestPerAccount = false,
            long? vmAccountId = null,
            string? interfaceAccountId = null,
            int? credentialId = null,
            bool? isManualRequest = null,
            string? sortColumn = null,
            bool sortDescending = true);
    Task<AdrJob?> GetJobAsync(int id);
    Task<List<AdrJob>> GetJobsByAccountAsync(int adrAccountId);
    Task<AdrJobStats> GetJobStatsAsync();
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
    Task<BackgroundOrchestrationResponse> StartBackgroundOrchestrationAsync();
    Task<OrchestrationCurrentResponse> GetCurrentOrchestrationAsync();
    Task<AdrOrchestrationStatus?> GetOrchestrationStatusAsync(string requestId);
    Task<List<AdrOrchestrationStatus>> GetOrchestrationHistoryAsync(int? count = 10);
    Task<OrchestrationHistoryPagedResponse> GetOrchestrationHistoryPagedAsync(int pageNumber = 1, int pageSize = 20);
}
