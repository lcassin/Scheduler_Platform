using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IAdrAccountRepository : IRepository<AdrAccount>
{
    Task<AdrAccount?> GetByVMAccountIdAsync(long vmAccountId);
    Task<IEnumerable<AdrAccount>> GetByCredentialIdAsync(int credentialId);
    Task<IEnumerable<AdrAccount>> GetByClientIdAsync(int clientId);
    Task<IEnumerable<AdrAccount>> GetAccountsDueForRunAsync(DateTime currentDate);
    Task<IEnumerable<AdrAccount>> GetAccountsNeedingCredentialCheckAsync(DateTime currentDate, int leadTimeDays = 7);
    Task<(IEnumerable<AdrAccount> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? clientId = null,
        int? credentialId = null,
        string? nextRunStatus = null,
        string? searchTerm = null);
    Task<int> GetTotalCountAsync(int? clientId = null);
    Task<int> GetCountByStatusAsync(string status, int? clientId = null);
    Task BulkUpsertAsync(IEnumerable<AdrAccount> accounts);
}
