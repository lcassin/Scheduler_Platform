using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

public class NotificationSetting : BaseEntity
{
    public int ScheduleId { get; set; }
    [JsonIgnore]
    public Schedule? Schedule { get; set; }
    
    public bool EnableSuccessNotifications { get; set; } = false;
    public bool EnableFailureNotifications { get; set; } = true;
    
    public string? SuccessEmailRecipients { get; set; }
    public string? FailureEmailRecipients { get; set; }
    
    public string? SuccessEmailSubject { get; set; }
    public string? FailureEmailSubject { get; set; }
    
    public bool IncludeExecutionDetails { get; set; } = true;
    public bool IncludeOutput { get; set; } = false;
}
