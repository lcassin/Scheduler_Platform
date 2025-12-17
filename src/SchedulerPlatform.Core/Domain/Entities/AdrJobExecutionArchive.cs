namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Archive table for AdrJobExecution records that have exceeded the retention period.
/// Maintains the same schema as AdrJobExecution for historical reference.
/// </summary>
public class AdrJobExecutionArchive
{
    /// <summary>
    /// Primary key for archive table
    /// </summary>
    public int AdrJobExecutionArchiveId { get; set; }
    
    /// <summary>
    /// Original AdrJobExecutionId before archiving
    /// </summary>
    public int OriginalAdrJobExecutionId { get; set; }
    
    /// <summary>
    /// FK to AdrJob (or archived job)
    /// </summary>
    public int AdrJobId { get; set; }
    
    /// <summary>
    /// Type of ADR request (1 = Attempt Login, 2 = Download Invoice)
    /// </summary>
    public int AdrRequestTypeId { get; set; }
    
    /// <summary>
    /// Execution start time
    /// </summary>
    public DateTime StartDateTime { get; set; }
    
    /// <summary>
    /// Execution end time
    /// </summary>
    public DateTime? EndDateTime { get; set; }
    
    /// <summary>
    /// ADR status ID
    /// </summary>
    public int? AdrStatusId { get; set; }
    
    /// <summary>
    /// ADR status description
    /// </summary>
    public string? AdrStatusDescription { get; set; }
    
    /// <summary>
    /// Whether this status indicates an error
    /// </summary>
    public bool IsError { get; set; }
    
    /// <summary>
    /// Whether this status is final
    /// </summary>
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// Index ID from ADR API
    /// </summary>
    public long? AdrIndexId { get; set; }
    
    /// <summary>
    /// HTTP status code
    /// </summary>
    public int? HttpStatusCode { get; set; }
    
    /// <summary>
    /// Success or failure
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Error message
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Full API response (JSON)
    /// </summary>
    public string? ApiResponse { get; set; }
    
    /// <summary>
    /// Request payload (JSON)
    /// </summary>
    public string? RequestPayload { get; set; }
    
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
