using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.API.Models;

public class RecentExecutionItem
{
    public int Id { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public JobStatus Status { get; set; }
    public int? DurationSeconds { get; set; }
}
