using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IJobExecutionRepository : IRepository<JobExecution>
{
    Task<IEnumerable<JobExecution>> GetByScheduleIdAsync(int scheduleId);
    Task<IEnumerable<JobExecution>> GetByStatusAsync(JobStatus status);
    Task<IEnumerable<JobExecution>> GetFailedExecutionsAsync(int scheduleId);
    Task<JobExecution?> GetLastExecutionAsync(int scheduleId);
    Task<IEnumerable<JobExecution>> GetByFiltersAsync(int? scheduleId, JobStatus? status, DateTime? startDate, DateTime? endDate);
}
