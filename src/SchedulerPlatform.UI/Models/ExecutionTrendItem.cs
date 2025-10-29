namespace SchedulerPlatform.UI.Models;

public class ExecutionTrendItem
{
    public DateTime Hour { get; set; }
    public double AverageDurationSeconds { get; set; }
    public int ExecutionCount { get; set; }
    public int ConcurrentCount { get; set; }
}
