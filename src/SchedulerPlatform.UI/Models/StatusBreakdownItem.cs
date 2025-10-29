using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.UI.Models;

public class StatusBreakdownItem
{
    public JobStatus Status { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}
