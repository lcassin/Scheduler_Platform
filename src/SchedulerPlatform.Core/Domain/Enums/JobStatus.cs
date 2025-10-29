namespace SchedulerPlatform.Core.Domain.Enums;

public enum JobStatus
{
    Scheduled = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Retrying = 5,
    Cancelled = 6
}
