namespace SchedulerPlatform.UI.Models;

public class MissedSchedulesResult
{
    public List<MissedScheduleItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int? WindowDays { get; set; }
    public DateTime AsOfUtc { get; set; }
}

public class MissedScheduleItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public DateTime? LastRunTime { get; set; }
    public double MinutesLate { get; set; }
}

public class BulkTriggerResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int TotalRequested { get; set; }
    public List<BulkTriggerItemResult> Results { get; set; } = new();
}

public class BulkTriggerItemResult
{
    public int ScheduleId { get; set; }
    public bool Success { get; set; }
    public string? ScheduleName { get; set; }
    public string? Error { get; set; }
}
