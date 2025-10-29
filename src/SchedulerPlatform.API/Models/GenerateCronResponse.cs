namespace SchedulerPlatform.API.Models;

public class GenerateCronResponse
{
    public List<CronExpressionResult> CronExpressions { get; set; } = new();
}

public class CronExpressionResult
{
    public DateTime DateTime { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? NextFireTime { get; set; }
}
