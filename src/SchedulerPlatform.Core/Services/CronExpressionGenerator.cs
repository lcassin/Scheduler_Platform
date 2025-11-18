using SchedulerPlatform.Core.Domain.Enums;
using System.Text;

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
    
    public static string GenerateFromFrequency(ScheduleFrequency frequency, string stableKey, int startHour = 4, int endHour = 24, DateTime? referenceDate = null)
    {
        if (string.IsNullOrEmpty(stableKey))
        {
            return GenerateFromFrequency(frequency, referenceDate);
        }
        
        if (startHour < 0 || startHour > 23 || endHour < 1 || endHour > 24 || startHour >= endHour)
        {
            throw new ArgumentException($"Invalid hour range: startHour={startHour}, endHour={endHour}. Must be 0 <= startHour < endHour <= 24");
        }
        
        var refDate = referenceDate ?? DateTime.Now;
        
        var (hour, minute, second) = CalculateStaggeredTime(stableKey, startHour, endHour);
        
        return frequency switch
        {
            ScheduleFrequency.Manual => "0 0 0 1 1 ? 2099", // Never runs automatically
            ScheduleFrequency.Daily => $"{second} {minute} {hour} * * ?",
            ScheduleFrequency.Weekly => $"{second} {minute} {hour} ? * MON",
            ScheduleFrequency.Monthly => $"{second} {minute} {hour} 1 * ?",
            ScheduleFrequency.Quarterly => GenerateQuarterlyCronStaggered(refDate, hour, minute, second),
            ScheduleFrequency.Annually => $"{second} {minute} {hour} 1 {refDate.Month} ?",
            ScheduleFrequency.Custom => $"{second} {minute} {hour} * * ?",
            _ => $"{second} {minute} {hour} * * ?"
        };
    }
    
    private static (int hour, int minute, int second) CalculateStaggeredTime(string stableKey, int startHour, int endHour)
    {
        int totalMinutes = (endHour - startHour) * 60;
        
        uint hash = StableHash(stableKey);
        int minuteOffset = (int)(hash % (uint)totalMinutes);
        
        int hour = startHour + (minuteOffset / 60);
        int minute = minuteOffset % 60;
        
        uint secondHash = StableHash(stableKey + "|sec");
        int second = (int)(secondHash % 60);
        
        return (hour, minute, second);
    }
    
    private static uint StableHash(string input)
    {
        const uint FnvPrime = 16777619;
        const uint FnvOffsetBasis = 2166136261;
        
        uint hash = FnvOffsetBasis;
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime;
        }
        
        return hash;
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
    
    private static string GenerateQuarterlyCronStaggered(DateTime referenceDate, int hour, int minute, int second)
    {
        int fiscalStartMonth = 1;
        int[] quarterMonths = new int[4];
        for (int i = 0; i < 4; i++)
        {
            quarterMonths[i] = ((fiscalStartMonth - 1 + i * 3) % 12) + 1;
        }
        var monthsField = string.Join(",", quarterMonths);
        return $"{second} {minute} {hour} 1 {monthsField} ?";
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
