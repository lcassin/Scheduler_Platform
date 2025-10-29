using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Jobs.Services;

public interface ISchedulerService
{
    Task ScheduleJob(Schedule schedule);
    Task UnscheduleJob(int scheduleId, int clientId);
    Task PauseJob(int scheduleId, int clientId);
    Task ResumeJob(int scheduleId, int clientId);
    Task TriggerJobNow(int scheduleId, int clientId, string triggeredBy);
    Task UpdateNextRunTimeAsync(int scheduleId, int clientId);
}
