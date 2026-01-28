using System.Text.Json.Serialization;

namespace SchedulerPlatform.UI.Models;

public class NotificationSetting
{
    public int Id { get; set; }
    
    public int ScheduleId { get; set; }
    
    [JsonPropertyName("enableSuccessNotifications")]
    public bool EnableSuccessNotifications { get; set; }
    
    [JsonPropertyName("enableFailureNotifications")]
    public bool EnableFailureNotifications { get; set; } = true;
    
    [JsonPropertyName("successEmailRecipients")]
    public string? SuccessEmailRecipients { get; set; }
    
    [JsonPropertyName("successEmailSubject")]
    public string? SuccessEmailSubject { get; set; }
    
    [JsonPropertyName("failureEmailRecipients")]
    public string? FailureEmailRecipients { get; set; }
    
    [JsonPropertyName("failureEmailSubject")]
    public string? FailureEmailSubject { get; set; }
    
    // Audit fields - ignored during serialization as they are set server-side
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime CreatedDateTime { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? CreatedBy { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime? ModifiedDateTime { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ModifiedBy { get; set; }
    
    public bool IncludeExecutionDetails { get; set; } = true;
    public bool IncludeOutput { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
}
