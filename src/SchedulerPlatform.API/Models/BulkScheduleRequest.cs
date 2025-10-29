using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.API.Models;

public class BulkScheduleRequest
{
    public int ClientId { get; set; }
    public JobType JobType { get; set; }
    public List<ScheduleDateTimeRequest> ScheduleDates { get; set; } = new();
    public string? TimeZone { get; set; } = "UTC";
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 5;
    public string? JobConfiguration { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class ScheduleDateTimeRequest
{
    public DateTime DateTime { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string>? JobParameters { get; set; }
}
