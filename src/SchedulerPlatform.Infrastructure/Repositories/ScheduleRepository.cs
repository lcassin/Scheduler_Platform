using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class ScheduleRepository : Repository<Schedule>, IScheduleRepository
{
    public ScheduleRepository(SchedulerDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<Schedule>> GetAllAsync()
    {
        return await _dbSet
            .Where(s => !s.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetByClientIdAsync(int clientId)
    {
        return await _dbSet
            .Include(s => s.Client)
            .Include(s => s.JobParameters)
            .Where(s => s.ClientId == clientId && !s.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetEnabledSchedulesAsync()
    {
        return await _dbSet
            .Include(s => s.Client)
            .Include(s => s.JobParameters)
            .Where(s => s.IsEnabled && !s.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesDueForExecutionAsync(DateTime currentTime)
    {
        return await _dbSet
            .Include(s => s.Client)
            .Include(s => s.JobParameters)
            .Where(s => s.IsEnabled 
                && !s.IsDeleted 
                && (s.NextRunTime == null || s.NextRunTime <= currentTime))
            .ToListAsync();
    }

    public async Task UpdateNextRunTimeAsync(int scheduleId, DateTime nextRunTime)
    {
        var schedule = await _dbSet.FindAsync(scheduleId);
        if (schedule != null)
        {
            schedule.NextRunTime = nextRunTime;
            schedule.LastRunTime = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<Schedule?> GetByIdWithNotificationSettingsAsync(int id)
    {
        return await _dbSet
            .Include(s => s.NotificationSetting)
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public async Task<(IEnumerable<Schedule> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? clientId = null,
        string? searchTerm = null)
    {
        var query = _dbSet
            .Include(s => s.Client)
            .Include(s => s.JobParameters)
            .Where(s => !s.IsDeleted);
        
        if (clientId.HasValue)
        {
            query = query.Where(s => s.ClientId == clientId.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(s => s.Name.Contains(searchTerm));
        }
        
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                Schedule = s,
                LastExecution = s.JobExecutions
                    .OrderByDescending(e => e.StartTime)
                    .FirstOrDefault()
            })
            .ToListAsync();
        
        var schedules = items.Select(i =>
        {
            i.Schedule.LastRunStatus = i.LastExecution?.Status;
            return i.Schedule;
        }).ToList();
        
        return (schedules, totalCount);
    }

    public async Task<IEnumerable<Schedule>> GetAllWithNotificationSettingsAsync()
    {
        return await _dbSet
            .Include(s => s.NotificationSetting)
            .Where(s => !s.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetByClientIdWithNotificationSettingsAsync(int clientId)
    {
        return await _dbSet
            .Where(s => s.ClientId == clientId && !s.IsDeleted)
            .Include(s => s.NotificationSetting)
            .ToListAsync();
    }
}
