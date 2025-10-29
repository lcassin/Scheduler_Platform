using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public interface IJobExecutionService
{
    Task<PagedResult<JobExecution>> GetJobExecutionsAsync(
        int? scheduleId = null, 
        JobStatus? status = null, 
        int pageNumber = 1, 
        int pageSize = 20);
    Task<PagedResult<JobExecution>> GetJobExecutionsPagedAsync(
        int? scheduleId = null,
        JobStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 100);
    Task RetryJobExecutionAsync(int id);
    Task<byte[]> DownloadJobExecutionsExportAsync(int? scheduleId, string? status, DateTime? startDate, DateTime? endDate, string format);
    Task<JobExecution?> GetJobExecutionAsync(int id);
    Task CancelJobExecutionAsync(int executionId);
}
