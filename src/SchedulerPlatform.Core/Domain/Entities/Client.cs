using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Entities;

public class Client : BaseEntity
{
    public int ExternalClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    
    [JsonIgnore]
    public ICollection<User> Users { get; set; } = new List<User>();
    [JsonIgnore]
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    [JsonIgnore]
    public ICollection<VendorCredential> VendorCredentials { get; set; } = new List<VendorCredential>();
    [JsonIgnore]
    public ICollection<ScheduleSyncSource> ScheduleSyncSources { get; set; } = new List<ScheduleSyncSource>();
}
