using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Entities;

public class ScheduleSyncSource : BaseEntity
{
    [Column("SyncId")]
    public override int Id { get; set; }
    
    public long AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public long VendorId { get; set; }
    public long ClientId { get; set; }
    public int ScheduleFrequency { get; set; }
    public DateTime LastInvoiceDate { get; set; }
    public string? AccountName { get; set; }
    public string? VendorName { get; set; }
    public string? ClientName { get; set; }
    public string? TandemAccountId { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    
    [JsonIgnore]
    public Client? Client { get; set; }
}
