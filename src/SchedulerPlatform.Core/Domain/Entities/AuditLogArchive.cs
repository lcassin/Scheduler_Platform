namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Archive table for AuditLog records that have exceeded the retention period.
/// Maintains the same schema as AuditLog for historical reference.
/// </summary>
public class AuditLogArchive
{
    /// <summary>
    /// Primary key for archive table
    /// </summary>
    public int AuditLogArchiveId { get; set; }
    
    /// <summary>
    /// Original AuditLogId before archiving
    /// </summary>
    public int OriginalAuditLogId { get; set; }
    
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime TimestampDateTime { get; set; }
    public string? AdditionalData { get; set; }
    
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
