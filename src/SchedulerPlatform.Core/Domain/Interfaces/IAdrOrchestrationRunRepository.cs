using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IAdrOrchestrationRunRepository : IRepository<AdrOrchestrationRun>
{
    Task<IEnumerable<AdrOrchestrationRun>> GetRecentRunsAsync(int count);
    Task<AdrOrchestrationRun?> GetByRequestIdAsync(string requestId);
}
