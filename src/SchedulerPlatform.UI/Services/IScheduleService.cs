using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public interface IScheduleService
{
    Task<List<Schedule>> GetSchedulesAsync(DateTime? startDate = null, DateTime? endDate = null, int? clientId = null);
    Task<PagedResult<Schedule>> GetSchedulesPagedAsync(
        int pageNumber = 1, 
        int pageSize = 20, 
        int? clientId = null, 
        string? searchTerm = null,
        bool? isEnabled = null);
    Task<Schedule?> GetScheduleAsync(int id);
    Task<Schedule> CreateScheduleAsync(Schedule schedule);
    Task<Schedule> UpdateScheduleAsync(int id, Schedule schedule);
    Task DeleteScheduleAsync(int id);
    Task TriggerScheduleAsync(int id);
    Task PauseScheduleAsync(int id);
    Task ResumeScheduleAsync(int id);
    Task<byte[]> DownloadSchedulesExportAsync(int? clientId, string? searchTerm, DateTime? startDate, DateTime? endDate, string format);
    Task<(bool Success, string Message)> TestConnectionAsync(string connectionString);
    Task<MissedSchedulesResult> GetMissedSchedulesAsync(int? windowDays = 1, int pageNumber = 1, int pageSize = 100);
    Task<BulkTriggerResult> BulkTriggerMissedSchedulesAsync(List<int> scheduleIds, int? delayBetweenTriggersMs = 200);
}
