namespace SchedulerPlatform.UI.Models;

public class TopLongestExecutionItem
{
    public string ScheduleName { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
}
