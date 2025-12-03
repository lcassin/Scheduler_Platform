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

    public async Task<JobExecution?> GetByIdAsync(int id)
    {
        var execution = await _dbSet
            .Include(je => je.Schedule)
            .FirstOrDefaultAsync(je => je.Id == id);
        
        if (execution != null)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return execution;
    }

    public override async Task<IEnumerable<JobExecution>> GetAllAsync()
    {
        var executions = await _dbSet
            .Include(je => je.Schedule)
            .OrderByDescending(je => je.StartDateTime)
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
            .OrderByDescending(je => je.StartDateTime)
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
            .OrderByDescending(je => je.StartDateTime)
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
            .OrderByDescending(je => je.StartDateTime)
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
            .OrderByDescending(je => je.StartDateTime)
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
            query = query.Where(je => je.StartDateTime >= startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            query = query.Where(je => je.EndDateTime.HasValue 
                ? je.EndDateTime.Value <= endDate.Value 
                : je.StartDateTime <= endDate.Value);
        }

        query = query.OrderByDescending(je => je.StartDateTime);
        
        var executions = await query.ToListAsync();
        
        foreach (var execution in executions)
        {
            execution.ScheduleName = execution.Schedule?.Name;
        }
        
        return executions;
    }

    public async Task<int> GetRunningCountAsync(DateTime startDate, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => e.Status == JobStatus.Running || e.Status == JobStatus.Retrying)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetCompletedTodayCountAsync(DateTime today, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= today)
            .Where(e => e.Status == JobStatus.Completed)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetFailedTodayCountAsync(DateTime today, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= today)
            .Where(e => e.Status == JobStatus.Failed)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<double> GetAverageDurationAsync(DateTime startDate, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => e.DurationSeconds.HasValue)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        var avg = await query.AverageAsync(e => (double?)e.DurationSeconds);
        return avg ?? 0;
    }

    public async Task<int> GetTotalExecutionsCountAsync(DateTime startDate, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<List<JobExecution>> GetExecutionsForPeakCalculationAsync(DateTime startDate, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => !e.Schedule.IsDeleted)
            .Select(e => new JobExecution
            {
                Id = e.Id,
                StartDateTime = e.StartDateTime,
                EndDateTime = e.EndDateTime,
                Status = e.Status
            });

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Dictionary<JobStatus, int>> GetStatusBreakdownAsync(DateTime startDate, int? clientId)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        var grouped = await query
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return grouped.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task<List<(int Year, int Month, int Day, int Hour, int ExecutionCount, double AvgDuration, int ConcurrentCount)>> GetExecutionTrendsAsync(
        DateTime startDate, int? clientId, JobStatus[] statuses)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        if (statuses != null && statuses.Length > 0)
        {
            query = query.Where(e => statuses.Contains(e.Status));
        }

        var trends = await query
            .GroupBy(e => new
            {
                Year = e.StartDateTime.Year,
                Month = e.StartDateTime.Month,
                Day = e.StartDateTime.Day,
                Hour = e.StartDateTime.Hour
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                g.Key.Hour,
                ExecutionCount = g.Count(),
                AvgDuration = g.Where(e => e.DurationSeconds.HasValue).Average(e => (double?)e.DurationSeconds) ?? 0,
                ConcurrentCount = g.Count(e => e.Status == JobStatus.Running || e.Status == JobStatus.Retrying)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ThenBy(x => x.Hour)
            .ToListAsync();

        return trends.Select(t => (t.Year, t.Month, t.Day, t.Hour, t.ExecutionCount, t.AvgDuration, t.ConcurrentCount)).ToList();
    }

    public async Task<List<(string ScheduleName, int DurationSeconds, DateTime StartTime, DateTime? EndTime)>> GetTopLongestAsync(
        DateTime startDate, int? clientId, JobStatus[] statuses, int limit)
    {
        var query = _dbSet.AsNoTracking()
            .Where(e => e.StartDateTime >= startDate)
            .Where(e => e.DurationSeconds.HasValue)
            .Where(e => !e.Schedule.IsDeleted);

        if (clientId.HasValue)
        {
            query = query.Where(e => e.Schedule.ClientId == clientId.Value);
        }

        if (statuses != null && statuses.Length > 0)
        {
            query = query.Where(e => statuses.Contains(e.Status));
        }

        var topLongest = await query
            .OrderByDescending(e => e.DurationSeconds)
            .Take(limit)
            .Select(e => new
            {
                ScheduleName = e.Schedule.Name ?? "Unknown",
                DurationSeconds = e.DurationSeconds!.Value,
                StartDateTime = e.StartDateTime,
                EndDateTime = e.EndDateTime
            })
            .ToListAsync();

        return topLongest.Select(x => (x.ScheduleName, x.DurationSeconds, x.StartDateTime, x.EndDateTime)).ToList();
    }

}
