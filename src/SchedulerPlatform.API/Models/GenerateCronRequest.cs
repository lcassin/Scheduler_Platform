namespace SchedulerPlatform.API.Models;

public class GenerateCronRequest
{
    public List<DateTime> DateTimes { get; set; } = new();
    public string? TimeZone { get; set; } = "UTC";
    public bool IncludeYear { get; set; } = true;
}
