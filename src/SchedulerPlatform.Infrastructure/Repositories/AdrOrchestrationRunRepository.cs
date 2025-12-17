using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class AdrOrchestrationRunRepository : Repository<AdrOrchestrationRun>, IAdrOrchestrationRunRepository
{
    public AdrOrchestrationRunRepository(SchedulerDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<AdrOrchestrationRun>> GetRecentRunsAsync(int count)
    {
        return await _dbSet
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.RequestedDateTime)
            .Take(count)
            .ToListAsync();
    }

    public async Task<AdrOrchestrationRun?> GetByRequestIdAsync(string requestId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.RequestId == requestId && !r.IsDeleted);
    }
}
