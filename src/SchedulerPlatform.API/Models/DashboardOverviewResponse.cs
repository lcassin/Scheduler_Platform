namespace SchedulerPlatform.API.Models;

public class DashboardOverviewResponse
{
    public int TotalSchedules { get; set; }
    public int EnabledSchedules { get; set; }
    public int DisabledSchedules { get; set; }
    public int RunningExecutions { get; set; }
    public int CompletedToday { get; set; }
    public int FailedToday { get; set; }
    public int PeakConcurrentExecutions { get; set; }
    public double AverageDurationSeconds { get; set; }
    public int TotalExecutionsInWindow { get; set; }
}
