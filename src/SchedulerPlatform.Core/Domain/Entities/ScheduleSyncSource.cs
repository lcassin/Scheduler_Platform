using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Entities;

public class ScheduleSyncSource : BaseEntity
{
    [Column("SyncId")]
    public override int Id { get; set; }
    
    public long ExternalAccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public long ExternalVendorId { get; set; }
    public int ExternalClientId { get; set; }
    public int? ClientId { get; set; }
    public int CredentialId { get; set; }
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
