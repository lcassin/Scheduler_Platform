using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

public class JobParameter : BaseEntity
{
    public int ScheduleId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string ParameterType { get; set; } = string.Empty;
    public string? ParameterValue { get; set; }
    public string? SourceQuery { get; set; }
    public string? SourceConnectionString { get; set; }
    public bool IsDynamic { get; set; }
    public int DisplayOrder { get; set; }
    
    [JsonIgnore]
    public Schedule Schedule { get; set; } = null!;
}
