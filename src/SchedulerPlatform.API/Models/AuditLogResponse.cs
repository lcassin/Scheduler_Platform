namespace SchedulerPlatform.API.Models;

/// <summary>
/// Response model for audit log entries.
/// </summary>
public class AuditLogResponse
{
    /// <summary>
    /// Unique identifier for the audit log entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Type of event (e.g., "Create", "Update", "Delete").
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Type of entity being audited (e.g., "Schedule", "Client").
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// ID of the entity being audited.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Action performed on the entity.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Username of the user who performed the action.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Client ID associated with the action.
    /// </summary>
    public int? ClientId { get; set; }

    /// <summary>
    /// IP address of the user who performed the action.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string of the client.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime TimestampDateTime { get; set; }

    /// <summary>
    /// JSON string containing the old values before the change.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON string containing the new values after the change.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Additional data associated with the audit log entry.
    /// </summary>
    public string? AdditionalData { get; set; }
}
