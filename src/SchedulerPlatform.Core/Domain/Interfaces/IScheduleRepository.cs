using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IScheduleRepository : IRepository<Schedule>
{
    Task<IEnumerable<Schedule>> GetByClientIdAsync(int clientId);
    Task<IEnumerable<Schedule>> GetEnabledSchedulesAsync();
    Task<IEnumerable<Schedule>> GetSchedulesDueForExecutionAsync(DateTime currentTime);
    Task UpdateNextRunTimeAsync(int scheduleId, DateTime nextRunTime);
    Task<Schedule?> GetByIdWithNotificationSettingsAsync(int id);
    Task<(IEnumerable<Schedule> items, int totalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        int? clientId = null, 
        string? searchTerm = null);
    Task<IEnumerable<Schedule>> GetAllWithNotificationSettingsAsync();
    Task<IEnumerable<Schedule>> GetByClientIdWithNotificationSettingsAsync(int clientId);
}
