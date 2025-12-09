using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IAdrJobRepository : IRepository<AdrJob>
{
    Task<AdrJob?> GetByAccountAndBillingPeriodAsync(int adrAccountId, DateTime billingPeriodStart, DateTime billingPeriodEnd);
    Task<IEnumerable<AdrJob>> GetByAccountIdAsync(int adrAccountId);
    Task<IEnumerable<AdrJob>> GetByStatusAsync(string status);
    Task<IEnumerable<AdrJob>> GetJobsNeedingCredentialVerificationAsync(DateTime currentDate);
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
            string? vmAccountNumber = null);
    Task<int> GetTotalCountAsync(int? adrAccountId = null);
    Task<int> GetCountByStatusAsync(string status);
    Task<int> GetActiveJobsCountAsync();
    Task<bool> ExistsForBillingPeriodAsync(int adrAccountId, DateTime billingPeriodStart, DateTime billingPeriodEnd);
}
