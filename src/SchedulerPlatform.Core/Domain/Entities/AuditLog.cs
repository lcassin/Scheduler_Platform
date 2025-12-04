namespace SchedulerPlatform.Core.Domain.Entities;

public class AuditLog : BaseEntity
{
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
    public DateTime TimestampDateTime { get; set; } = DateTime.UtcNow;
    public string? AdditionalData { get; set; }
}
