using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IAdrJobRepository : IRepository<AdrJob>
{
    Task<AdrJob?> GetByAccountAndBillingPeriodAsync(int adrAccountId, DateTime billingPeriodStart, DateTime billingPeriodEnd);
    Task<IEnumerable<AdrJob>> GetByAccountIdAsync(int adrAccountId);
    Task<IEnumerable<AdrJob>> GetByStatusAsync(string status);
    Task<IEnumerable<AdrJob>> GetJobsNeedingCredentialVerificationAsync(DateTime currentDate, int credentialCheckLeadDays = 7);
    Task<IEnumerable<AdrJob>> GetJobsReadyForScrapingAsync(DateTime currentDate);
    Task<IEnumerable<AdrJob>> GetJobsNeedingStatusCheckAsync(DateTime currentDate, int followUpDelayDays = 5);
    Task<IEnumerable<AdrJob>> GetJobsForRetryAsync(DateTime currentDate, int maxRetries = 5);
            Task<(IEnumerable<AdrJob> items, int totalCount)> GetPagedAsync(
                int pageNumber,
                int pageSize,
                int? adrAccountId = null,
                string? status = null,
                DateTime? billingPeriodStart = null,
                DateTime? billingPeriodEnd = null,
                string? vendorCode = null,
                string? masterVendorCode = null,
                string? vmAccountNumber = null,
                bool latestPerAccount = false,
                long? vmAccountId = null,
                string? interfaceAccountId = null,
                int? credentialId = null,
                bool? isManualRequest = null,
                string? sortColumn = null,
                bool sortDescending = true,
                List<int>? jobIds = null,
                int? adrJobTypeId = null);
    Task<int> GetTotalCountAsync(int? adrAccountId = null);
    Task<int> GetCountByStatusAsync(string status);
    Task<int> GetCountByStatusAndIdsAsync(string status, HashSet<int> jobIds);
    Task<Dictionary<string, int>> GetCountsByStatusAndIdsAsync(HashSet<int> jobIds);
    Task<int> GetActiveJobsCountAsync();
    Task<bool> ExistsForBillingPeriodAsync(int adrAccountId, DateTime billingPeriodStart, DateTime billingPeriodEnd);
    Task<IEnumerable<AdrJob>> GetJobsNeedingDailyStatusCheckAsync(DateTime currentDate, int delayDays = 1);
    Task<IEnumerable<AdrJob>> GetAllJobsForManualStatusCheckAsync();
    /// <summary>
    /// Gets jobs that are stuck in Pending or CredentialCheckInProgress status
    /// but have passed their billing window (NextRangeEndDateTime &lt; today).
    /// These jobs need to be finalized (cancelled) and their rules advanced to the next cycle.
    /// </summary>
    /// <param name="currentDate">The current date for comparison</param>
    /// <param name="maxLookbackDays">Maximum days to look back (to avoid processing very old jobs)</param>
    Task<IEnumerable<AdrJob>> GetStalePendingJobsAsync(DateTime currentDate, int maxLookbackDays = 90);
    
    /// <summary>
    /// Gets the persistent rebill job for an account, or null if none exists.
    /// Rebill jobs (JobTypeId = 3) are persistent per-account and reused for all rebill executions.
    /// </summary>
    /// <param name="adrAccountId">The account ID to find the rebill job for</param>
    Task<AdrJob?> GetRebillJobByAccountAsync(int adrAccountId);
}
