namespace SchedulerPlatform.API.Models;

public class InvalidScheduleInfo
{
    public int ScheduleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public DateTime? LastFailureDateTime { get; set; }
    public string? LastErrorMessage { get; set; }
}
