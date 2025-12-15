using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class AdrJobExecutionRepository : Repository<AdrJobExecution>, IAdrJobExecutionRepository
{
    public AdrJobExecutionRepository(SchedulerDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<AdrJobExecution>> GetByJobIdAsync(int adrJobId)
    {
        return await _dbSet
            .Where(e => e.AdrJobId == adrJobId && !e.IsDeleted)
            .OrderByDescending(e => e.StartDateTime)
            .ToListAsync();
    }

    public async Task<(IEnumerable<AdrJobExecution> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? adrJobId = null,
        int? adrRequestTypeId = null,
        bool? isSuccess = null)
    {
        var query = _dbSet.Where(e => !e.IsDeleted);

        if (adrJobId.HasValue)
        {
            query = query.Where(e => e.AdrJobId == adrJobId.Value);
        }

        if (adrRequestTypeId.HasValue)
        {
            query = query.Where(e => e.AdrRequestTypeId == adrRequestTypeId.Value);
        }

        if (isSuccess.HasValue)
        {
            query = query.Where(e => e.IsSuccess == isSuccess.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.StartDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(e => e.AdrJob)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> DeleteByJobIdAsync(int adrJobId)
    {
        // Soft delete all executions for this job to allow force refire
        var executions = await _dbSet
            .Where(e => e.AdrJobId == adrJobId && !e.IsDeleted)
            .ToListAsync();

        foreach (var execution in executions)
        {
            execution.IsDeleted = true;
            execution.ModifiedDateTime = DateTime.UtcNow;
            execution.ModifiedBy = "System Created";
        }

        return executions.Count;
    }

    public async Task<IEnumerable<int>> GetJobIdsModifiedSinceAsync(DateTime sinceDateTime)
    {
        // Get distinct job IDs that have executions created since the given date
        // This is used to scope job stats to jobs touched by recent orchestration runs
        return await _dbSet
            .Where(e => !e.IsDeleted && e.StartDateTime >= sinceDateTime)
            .Select(e => e.AdrJobId)
            .Distinct()
            .ToListAsync();
    }
}
