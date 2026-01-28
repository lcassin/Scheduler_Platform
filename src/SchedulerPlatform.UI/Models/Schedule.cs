using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.UI.Models;

public class Schedule
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public int ClientId { get; set; }
    
    [Required]
    public JobType JobType { get; set; }
    
    [Required]
    public ScheduleFrequency Frequency { get; set; }
    
    [Required]
    public string CronExpression { get; set; } = string.Empty;
    
    [JsonPropertyName("jobConfiguration")]
    public string? JobDataJson { get; set; }
    
    public bool IsEnabled { get; set; }
    
    public bool IsSystemSchedule { get; set; }
    
    public int MaxRetries { get; set; } = 3;
    
    public int RetryDelayMinutes { get; set; } = 5;
    
    public string? TimeZone { get; set; }
    
    public DateTime? NextRunDateTime { get; set; }
    
    public DateTime? LastRunDateTime { get; set; }
    
    public JobStatus? LastRunStatus { get; set; }
    
    public int? TimeoutMinutes { get; set; }
    
    // Audit fields - ignored during serialization as they are set server-side
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime CreatedDateTime { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? CreatedBy { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime? ModifiedDateTime { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ModifiedBy { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsDeleted { get; set; } = false;
    
    public NotificationSetting? NotificationSetting { get; set; }
}

public enum JobType
{
    Process = 1,
    StoredProcedure = 2,
    ApiCall = 3,
    Maintenance = 4
}

public enum ScheduleFrequency
{
    Manual = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Annually = 5,
    Custom = 6
}
