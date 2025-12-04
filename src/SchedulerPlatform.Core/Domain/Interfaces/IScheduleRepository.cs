using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IScheduleRepository : IRepository<Schedule>
{
    Task<IEnumerable<Schedule>> GetByClientIdAsync(int clientId);
    Task<IEnumerable<Schedule>> GetEnabledSchedulesAsync();
    Task<IEnumerable<Schedule>> GetSchedulesDueForExecutionAsync(DateTime currentTime);
    Task UpdateNextRunDateTimeAsync(int scheduleId, DateTime nextRunDateTime);
    Task<Schedule?> GetByIdWithNotificationSettingsAsync(int id);
    Task<(IEnumerable<Schedule> items, int totalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        int? clientId = null, 
        string? searchTerm = null,
        bool? isEnabled = null);
    Task<IEnumerable<Schedule>> GetAllWithNotificationSettingsAsync();
    Task<IEnumerable<Schedule>> GetByClientIdWithNotificationSettingsAsync(int clientId);
    
    Task<int> GetTotalSchedulesCountAsync(int? clientId);
    Task<int> GetEnabledSchedulesCountAsync(int? clientId);
    Task<int> GetDisabledSchedulesCountAsync(int? clientId);
    Task<IEnumerable<Schedule>> GetSchedulesForCalendarAsync(DateTime startUtc, DateTime endUtc, int? clientId, int maxPerDay = 10);
}
