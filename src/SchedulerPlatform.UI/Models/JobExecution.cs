using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.UI.Models;

public class JobExecution
{
    public int Id { get; set; }
    
    public int ScheduleId { get; set; }
    
    public string? ScheduleName { get; set; }
    
    public DateTime StartTime { get; set; }
    
    public DateTime? EndTime { get; set; }
    
    public JobStatus Status { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string? OutputLog { get; set; }
    
    public string? Output { get; set; }
    
    public string? TriggeredBy { get; set; }
    
    public string? CancelledBy { get; set; }
    
    public int RetryCount { get; set; }
}
