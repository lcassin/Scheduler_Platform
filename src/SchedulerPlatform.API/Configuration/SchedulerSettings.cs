namespace SchedulerPlatform.API.Configuration;

public class SchedulerSettings
{
    public MissedScheduleHandlingSettings MissedScheduleHandling { get; set; } = new();
}

public class MissedScheduleHandlingSettings
{
    public bool EnableAutoFire { get; set; } = true;
    public int MissedScheduleWindowDays { get; set; } = 2;
    public bool EnableInDevelopment { get; set; } = false;
    public int ThrottlePerSecond { get; set; } = 50;
}
