using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IAdrJobExecutionRepository : IRepository<AdrJobExecution>
{
    Task<IEnumerable<AdrJobExecution>> GetByJobIdAsync(int adrJobId);
    Task<(IEnumerable<AdrJobExecution> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? adrJobId = null,
        int? adrRequestTypeId = null,
        bool? isSuccess = null);
    Task<int> DeleteByJobIdAsync(int adrJobId);
    Task<IEnumerable<int>> GetJobIdsModifiedSinceAsync(DateTime sinceDateTime);
}
