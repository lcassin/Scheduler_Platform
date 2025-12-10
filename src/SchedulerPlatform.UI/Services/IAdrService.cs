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
        string? historicalBillingStatus = null);
    Task<AdrAccount?> GetAccountAsync(int id);
    Task<AdrAccount?> GetAccountByVMAccountIdAsync(long vmAccountId);
    Task<AdrAccountStats> GetAccountStatsAsync();
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
            bool latestPerAccount = false);
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
    
    // Orchestration operations
    Task<AdrAccountSyncResult> SyncAccountsAsync();
    Task<JobCreationResult> CreateJobsAsync();
    Task<CredentialVerificationResult> VerifyCredentialsAsync();
    Task<ScrapeResult> ProcessScrapingAsync();
    Task<StatusCheckResult> CheckStatusesAsync();
    Task<FullCycleResult> RunFullCycleAsync();
}
