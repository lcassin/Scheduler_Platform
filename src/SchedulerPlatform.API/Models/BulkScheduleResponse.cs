namespace SchedulerPlatform.API.Models;

public class BulkScheduleResponse
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ScheduleResult> Results { get; set; } = new();
}

public class ScheduleResult
{
    public bool Success { get; set; }
    public int? ScheduleId { get; set; }
    public string? CronExpression { get; set; }
    public DateTime DateTime { get; set; }
    public string? ErrorMessage { get; set; }
}
