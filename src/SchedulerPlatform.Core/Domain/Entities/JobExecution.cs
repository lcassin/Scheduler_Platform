using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Entities;

public class JobExecution : BaseEntity
{
    public int ScheduleId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public JobStatus Status { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public int RetryCount { get; set; }
    public int? DurationSeconds { get; set; }
    public string? TriggeredBy { get; set; }
    public string? CancelledBy { get; set; }
    
    [NotMapped]
    public string? ScheduleName { get; set; }
    
    [JsonIgnore]
    public Schedule Schedule { get; set; } = null!;
}
