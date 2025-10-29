using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Entities;

public class ScheduleSyncSource : BaseEntity
{
    public int ClientId { get; set; }
    public string Vendor { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public ScheduleFrequency ScheduleFrequency { get; set; }
    public DateTime ScheduleDate { get; set; }
    
    [JsonIgnore]
    public Client? Client { get; set; }
}
