using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class JobExecutionRepository : Repository<JobExecution>, IJobExecutionRepository
{
    public JobExecutionRepository(SchedulerDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<JobExecution>> GetAllAsync()
    {
        var executions = await _dbSet
            .Include(je => je.Schedule)
            .OrderByDescending(je => je.StartTime)
            .ToListAsync();
        
        foreach (var execution in executions)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return executions;
    }

    public async Task<IEnumerable<JobExecution>> GetByScheduleIdAsync(int scheduleId)
    {
        var executions = await _dbSet
            .Include(je => je.Schedule)
            .Where(je => je.ScheduleId == scheduleId)
            .OrderByDescending(je => je.StartTime)
            .ToListAsync();
        
        foreach (var execution in executions)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return executions;
    }

    public async Task<IEnumerable<JobExecution>> GetByStatusAsync(JobStatus status)
    {
        var executions = await _dbSet
            .Include(je => je.Schedule)
            .Where(je => je.Status == status)
            .OrderByDescending(je => je.StartTime)
            .ToListAsync();
        
        foreach (var execution in executions)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return executions;
    }

    public async Task<IEnumerable<JobExecution>> GetFailedExecutionsAsync(int scheduleId)
    {
        var executions = await _dbSet
            .Include(je => je.Schedule)
            .Where(je => je.ScheduleId == scheduleId && je.Status == JobStatus.Failed)
            .OrderByDescending(je => je.StartTime)
            .ToListAsync();
        
        foreach (var execution in executions)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return executions;
    }

    public async Task<JobExecution?> GetLastExecutionAsync(int scheduleId)
    {
        var execution = await _dbSet
            .Include(je => je.Schedule)
            .Where(je => je.ScheduleId == scheduleId)
            .OrderByDescending(je => je.StartTime)
            .FirstOrDefaultAsync();
        
        if (execution != null)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return execution;
    }

    public async Task<IEnumerable<JobExecution>> GetByFiltersAsync(
        int? scheduleId, 
        JobStatus? status, 
        DateTime? startDate, 
        DateTime? endDate)
    {
        var query = _dbSet.Include(je => je.Schedule).AsQueryable();
        
        if (scheduleId.HasValue)
        {
            query = query.Where(je => je.ScheduleId == scheduleId.Value);
        }
        
        if (status.HasValue)
        {
            query = query.Where(je => je.Status == status.Value);
        }
        
        if (startDate.HasValue)
        {
            query = query.Where(je => je.StartTime >= startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            query = query.Where(je => je.EndTime.HasValue 
                ? je.EndTime.Value <= endDate.Value 
                : je.StartTime <= endDate.Value);
        }

        query = query.OrderByDescending(je => je.StartTime);
        
        var executions = await query.ToListAsync();
        
        foreach (var execution in executions)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return executions;
    }

}
