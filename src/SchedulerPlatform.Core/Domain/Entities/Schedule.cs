using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Domain.Entities;

public class Schedule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public JobType JobType { get; set; }
    public ScheduleFrequency Frequency { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    
    [NotMapped]
    public JobStatus? LastRunStatus { get; set; }
    
    public bool IsEnabled { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 5;
    public int? TimeoutMinutes { get; set; }
    public string? TimeZone { get; set; }
    public string? JobConfiguration { get; set; }
    
    [JsonIgnore]
    public Client? Client { get; set; }
    [JsonIgnore]
    public ICollection<JobExecution> JobExecutions { get; set; } = new List<JobExecution>();
    [JsonIgnore]
    public ICollection<JobParameter> JobParameters { get; set; } = new List<JobParameter>();
    public NotificationSetting? NotificationSetting { get; set; }
}
