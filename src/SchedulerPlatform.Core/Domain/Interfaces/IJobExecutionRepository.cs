using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IJobExecutionRepository : IRepository<JobExecution>
{
    Task<JobExecution?> GetByIdAsync(int id);
    Task<IEnumerable<JobExecution>> GetByScheduleIdAsync(int scheduleId);
    Task<IEnumerable<JobExecution>> GetByStatusAsync(JobStatus status);
    Task<IEnumerable<JobExecution>> GetFailedExecutionsAsync(int scheduleId);
    Task<JobExecution?> GetLastExecutionAsync(int scheduleId);
    Task<IEnumerable<JobExecution>> GetByFiltersAsync(int? scheduleId, JobStatus? status, DateTime? startDate, DateTime? endDate);
    
    Task<int> GetRunningCountAsync(DateTime startDate, int? clientId);
    Task<int> GetCompletedTodayCountAsync(DateTime today, int? clientId);
    Task<int> GetFailedTodayCountAsync(DateTime today, int? clientId);
    Task<double> GetAverageDurationAsync(DateTime startDate, int? clientId);
    Task<int> GetTotalExecutionsCountAsync(DateTime startDate, int? clientId);
    Task<List<JobExecution>> GetExecutionsForPeakCalculationAsync(DateTime startDate, int? clientId);
    
    Task<Dictionary<JobStatus, int>> GetStatusBreakdownAsync(DateTime startDate, int? clientId);
    Task<List<(int Year, int Month, int Day, int Hour, int ExecutionCount, double AvgDuration, int ConcurrentCount)>> GetExecutionTrendsAsync(DateTime startDate, int? clientId, JobStatus[] statuses);
    Task<List<(string ScheduleName, int DurationSeconds, DateTime StartDateTime, DateTime? EndDateTime)>> GetTopLongestAsync(DateTime startDate, int? clientId, JobStatus[] statuses, int limit);
}
