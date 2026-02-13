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
    /// Jobs are created when NextRunDateTime <= today.
    /// </summary>
    Task<IEnumerable<AdrAccount>> GetDueAccountsWithRulesAsync();
    
    /// <summary>
    /// Gets all active accounts with valid credential IDs for bulk credential verification.
    /// This is used for one-time bulk operations to check all existing credentials.
    /// </summary>
    /// <returns>All non-deleted accounts with CredentialId > 0</returns>
    Task<IEnumerable<AdrAccount>> GetAllActiveAccountsForCredentialCheckAsync(int? testrun=null);
    
    /// <summary>
    /// Gets accounts for weekly rebill processing where the expected billing day of week matches the specified day.
    /// Uses OverriddenDateTime if manually set, otherwise uses ExpectedNextDateTime.
    /// This is optimized to filter at the database level rather than loading all accounts into memory.
    /// </summary>
    /// <param name="dayOfWeek">The day of week to filter by (0 = Sunday, 6 = Saturday)</param>
    /// <returns>Active accounts with valid credentials whose billing day matches the specified day</returns>
    Task<IEnumerable<AdrAccount>> GetAccountsForRebillByDayOfWeekAsync(DayOfWeek dayOfWeek);
}
