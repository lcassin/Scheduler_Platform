using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IAdrAccountRepository : IRepository<AdrAccount>
{
    Task<AdrAccount?> GetByVMAccountIdAsync(long vmAccountId);
    Task<IEnumerable<AdrAccount>> GetAccountsDueForRunAsync(DateTime currentDate);
    Task<IEnumerable<AdrAccount>> GetAccountsNeedingCredentialCheckAsync(DateTime currentDate, int leadTimeDays = 7);
    Task<(IEnumerable<AdrAccount> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? clientId = null,
        int? credentialId = null,
        string? nextRunStatus = null,
        string? searchTerm = null,
        string? historicalBillingStatus = null,
        bool? isOverridden = null,
        string? sortColumn = null,
        bool sortDescending = false,
        List<int>? accountIdsFilter = null,
        string? primaryVendorCode = null,
        string? masterVendorCode = null);
    Task<int> GetTotalCountAsync(int? clientId = null);
    Task<int> GetCountByNextRunStatusAsync(string status, int? clientId = null);
    Task<int> GetCountByHistoricalStatusAsync(string status, int? clientId = null);
    /// <summary>
    /// Gets accounts with rules that are due for job creation.
    /// Jobs are created when NextRunDateTime is within the credential check window (credentialCheckLeadDays in the future)
    /// or has already arrived/passed. This allows credential verification to happen before NextRunDate.
    /// </summary>
    /// <param name="credentialCheckLeadDays">Number of days before NextRunDate to start creating jobs (default: 7)</param>
    Task<IEnumerable<AdrAccount>> GetDueAccountsWithRulesAsync(int credentialCheckLeadDays = 7);
}
