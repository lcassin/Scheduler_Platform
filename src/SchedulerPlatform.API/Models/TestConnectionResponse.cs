namespace SchedulerPlatform.API.Models;

public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ErrorNumber { get; set; }
}
