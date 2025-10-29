using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.API.Models;

public class StatusBreakdownItem
{
    public JobStatus Status { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}
