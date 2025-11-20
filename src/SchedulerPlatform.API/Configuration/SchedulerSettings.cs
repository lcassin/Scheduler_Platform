namespace SchedulerPlatform.API.Configuration;

public class SchedulerSettings
{
    public ScheduleHydrationSettings Hydration { get; set; } = new();
    public MissedScheduleHandlingSettings MissedScheduleHandling { get; set; } = new();
    public StartupRecoverySettings StartupRecovery { get; set; } = new();
}

public class ScheduleHydrationSettings
{
    public bool Enabled { get; set; } = true;
    public int HorizonHours { get; set; } = 24;
    public int BatchSize { get; set; } = 10000;
    public int DelaySeconds { get; set; } = 7;
}

public class MissedScheduleHandlingSettings
{
    public bool EnableAutoFire { get; set; } = true;
    public int MissedScheduleWindowDays { get; set; } = 2;
    public bool EnableInDevelopment { get; set; } = false;
    public int ThrottlePerSecond { get; set; } = 50;
}

public class StartupRecoverySettings
{
    public bool Enabled { get; set; } = true;
    public int GracePeriodMinutes { get; set; } = 10;
    public int DelaySeconds { get; set; } = 30;
}
