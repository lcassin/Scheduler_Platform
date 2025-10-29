namespace SchedulerPlatform.API.Models;

public class TopLongestExecutionItem
{
    public string ScheduleName { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
