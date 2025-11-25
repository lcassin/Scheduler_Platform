namespace SchedulerPlatform.API.Models;

public class BulkTriggerRequest
{
    public List<int> ScheduleIds { get; set; } = new();
    public int? DelayBetweenTriggersMs { get; set; } = 200; // Default 200ms between triggers for staggering
}
