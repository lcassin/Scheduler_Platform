namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Archive table for JobExecution records (Schedule Job Executions) that have exceeded the retention period.
/// Maintains the same schema as JobExecution for historical reference.
/// </summary>
public class JobExecutionArchive
{
    /// <summary>
    /// Primary key for archive table
    /// </summary>
    public int JobExecutionArchiveId { get; set; }
    
    /// <summary>
    /// Original JobExecutionId before archiving
    /// </summary>
    public int OriginalJobExecutionId { get; set; }
    
    /// <summary>
    /// FK to Schedule
    /// </summary>
    public int ScheduleId { get; set; }
    
    /// <summary>
    /// Execution start time
    /// </summary>
    public DateTime StartDateTime { get; set; }
    
    /// <summary>
    /// Execution end time
    /// </summary>
    public DateTime? EndDateTime { get; set; }
    
    /// <summary>
    /// Job status (Running, Completed, Failed, etc.)
    /// </summary>
    public int Status { get; set; }
    
    /// <summary>
    /// Output from the job execution
    /// </summary>
    public string? Output { get; set; }
    
    /// <summary>
    /// Error message if the job failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Stack trace if the job failed
    /// </summary>
    public string? StackTrace { get; set; }
    
    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Duration of the job execution in seconds
    /// </summary>
    public int? DurationSeconds { get; set; }
    
    /// <summary>
    /// User or process that triggered the job
    /// </summary>
    public string? TriggeredBy { get; set; }
    
    /// <summary>
    /// User who cancelled the job (if cancelled)
    /// </summary>
    public string? CancelledBy { get; set; }
    
    // Original audit fields
    public DateTime CreatedDateTime { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedDateTime { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when this record was archived
    /// </summary>
    public DateTime ArchivedDateTime { get; set; }
    
    /// <summary>
    /// User or process that performed the archival
    /// </summary>
    public string ArchivedBy { get; set; } = string.Empty;
}
