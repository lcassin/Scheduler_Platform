using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.Core.Services;

public static class CronExpressionGenerator
{
    public static string GenerateFromFrequency(ScheduleFrequency frequency, DateTime? referenceDate = null)
    {
        var refDate = referenceDate ?? DateTime.Now;
        
        return frequency switch
        {
            ScheduleFrequency.Manual => "0 0 0 1 1 ? 2099", // Never runs automatically
            ScheduleFrequency.Daily => "0 0 9 * * ?", // Daily at 9:00 AM
            ScheduleFrequency.Weekly => "0 0 9 ? * MON", // Weekly on Monday at 9:00 AM
            ScheduleFrequency.Monthly => "0 0 9 1 * ?", // Monthly on 1st at 9:00 AM
            ScheduleFrequency.Quarterly => GenerateQuarterlyCron(refDate),
            ScheduleFrequency.Annually => $"0 0 9 1 {refDate.Month} ?", // Annually on same month/day at 9:00 AM
            ScheduleFrequency.Custom => "0 0 9 * * ?", // Default to daily
            _ => "0 0 9 * * ?" // Default to daily
        };
    }
    
    private static string GenerateQuarterlyCron(DateTime referenceDate)
    {
        int fiscalStartMonth = 1;
        int[] quarterMonths = new int[4];
        for (int i = 0; i < 4; i++)
        {
            quarterMonths[i] = ((fiscalStartMonth - 1 + i * 3) % 12) + 1;
        }
        var monthsField = string.Join(",", quarterMonths);
        return $"0 0 9 1 {monthsField} ?"; // First day of quarter months at 9:00 AM
    }
    
    public static string GetDescription(ScheduleFrequency frequency)
    {
        return frequency switch
        {
            ScheduleFrequency.Manual => "Manual execution only - will not run automatically",
            ScheduleFrequency.Daily => "Runs daily at 9:00 AM",
            ScheduleFrequency.Weekly => "Runs weekly on Monday at 9:00 AM",
            ScheduleFrequency.Monthly => "Runs monthly on the 1st at 9:00 AM",
            ScheduleFrequency.Quarterly => "Runs quarterly (Jan, Apr, Jul, Oct) on the 1st at 9:00 AM",
            ScheduleFrequency.Annually => "Runs annually",
            ScheduleFrequency.Custom => "Custom schedule",
            _ => "Unknown schedule"
        };
    }
}
