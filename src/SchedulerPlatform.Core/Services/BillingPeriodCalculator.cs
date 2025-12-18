namespace SchedulerPlatform.Core.Services;

/// <summary>
/// Provides calendar-based billing period calculations to prevent date creep.
/// Uses proper calendar arithmetic (AddMonths/AddYears) for month-based periods
/// instead of fixed day counts which cause drift over time.
/// </summary>
public static class BillingPeriodCalculator
{
    /// <summary>
    /// Calculates the next run date based on the period type and anchor date.
    /// Uses calendar arithmetic for month-based periods to prevent date creep.
    /// </summary>
    /// <param name="periodType">The billing period type (Monthly, Bi-Monthly, Quarterly, etc.)</param>
    /// <param name="anchorDate">The anchor date to calculate from (typically LastSuccessfulDownloadDate or previous NextRunDateTime)</param>
    /// <param name="anchorDayOfMonth">Optional anchor day of month to preserve (prevents drift after short months like February)</param>
    /// <returns>The calculated next run date</returns>
    public static DateTime CalculateNextRunDate(string? periodType, DateTime anchorDate, int? anchorDayOfMonth = null)
    {
        if (string.IsNullOrEmpty(periodType))
        {
            // Default to monthly if not specified
            return AddMonthsWithAnchor(anchorDate, 1, anchorDayOfMonth);
        }

        return periodType.ToLowerInvariant() switch
        {
            "bi-weekly" => CalculateBiWeeklyNextDate(anchorDate),
            "monthly" => AddMonthsWithAnchor(anchorDate, 1, anchorDayOfMonth),
            "bi-monthly" => AddMonthsWithAnchor(anchorDate, 2, anchorDayOfMonth),
            "quarterly" => AddMonthsWithAnchor(anchorDate, 3, anchorDayOfMonth),
            "semi-annually" => AddMonthsWithAnchor(anchorDate, 6, anchorDayOfMonth),
            "annually" => AddYearsWithAnchor(anchorDate, 1, anchorDayOfMonth),
            _ => AddMonthsWithAnchor(anchorDate, 1, anchorDayOfMonth) // Default to monthly
        };
    }

    /// <summary>
    /// Calculates the next run date for bi-weekly billing (every 14 days).
    /// Includes drift detection - if the calculated date drifts too far from expected,
    /// it will be corrected based on the anchor.
    /// </summary>
    private static DateTime CalculateBiWeeklyNextDate(DateTime anchorDate)
    {
        // For bi-weekly, use 14 days
        // Drift correction will be handled by the sync process using historical median
        return anchorDate.AddDays(14);
    }

    /// <summary>
    /// Adds months to a date while preserving the anchor day of month.
    /// This prevents "sticky" drift after short months (e.g., Jan 31 -> Feb 28 -> Mar 28).
    /// Instead, it restores the original anchor day when possible (Jan 31 -> Feb 28 -> Mar 31).
    /// </summary>
    /// <param name="date">The base date to add months to</param>
    /// <param name="months">Number of months to add</param>
    /// <param name="anchorDayOfMonth">Optional anchor day to preserve. If null, uses the day from the input date.</param>
    /// <returns>The calculated date with anchor day preserved when possible</returns>
    public static DateTime AddMonthsWithAnchor(DateTime date, int months, int? anchorDayOfMonth = null)
    {
        // Use the provided anchor day, or the day from the input date
        var anchor = anchorDayOfMonth ?? date.Day;
        
        // Calculate the target month
        var targetDate = date.AddMonths(months);
        var targetYear = targetDate.Year;
        var targetMonth = targetDate.Month;
        
        // Get the last day of the target month
        var daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        
        // Use the anchor day, but clamp to the last day of the month if needed
        var targetDay = Math.Min(anchor, daysInTargetMonth);
        
        return new DateTime(targetYear, targetMonth, targetDay, date.Hour, date.Minute, date.Second, date.Kind);
    }

    /// <summary>
    /// Adds years to a date while preserving the anchor day of month.
    /// Handles leap year edge cases (Feb 29 -> Feb 28 in non-leap years).
    /// </summary>
    /// <param name="date">The base date to add years to</param>
    /// <param name="years">Number of years to add</param>
    /// <param name="anchorDayOfMonth">Optional anchor day to preserve. If null, uses the day from the input date.</param>
    /// <returns>The calculated date with anchor day preserved when possible</returns>
    public static DateTime AddYearsWithAnchor(DateTime date, int years, int? anchorDayOfMonth = null)
    {
        var anchor = anchorDayOfMonth ?? date.Day;
        
        var targetDate = date.AddYears(years);
        var targetYear = targetDate.Year;
        var targetMonth = targetDate.Month;
        
        var daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var targetDay = Math.Min(anchor, daysInTargetMonth);
        
        return new DateTime(targetYear, targetMonth, targetDay, date.Hour, date.Minute, date.Second, date.Kind);
    }

    /// <summary>
    /// Calculates the next run date that is on or after today.
    /// If the calculated next date is in the past, advances by additional periods until it's in the future.
    /// </summary>
    /// <param name="periodType">The billing period type</param>
    /// <param name="anchorDate">The anchor date to calculate from</param>
    /// <param name="today">The current date (for testing, defaults to DateTime.UtcNow.Date)</param>
    /// <param name="anchorDayOfMonth">Optional anchor day to preserve</param>
    /// <returns>The next run date that is on or after today</returns>
    public static DateTime CalculateNextRunDateOnOrAfterToday(
        string? periodType, 
        DateTime anchorDate, 
        DateTime? today = null,
        int? anchorDayOfMonth = null)
    {
        var currentDate = today ?? DateTime.UtcNow.Date;
        var nextDate = CalculateNextRunDate(periodType, anchorDate, anchorDayOfMonth);
        
        // Keep advancing until we get a date on or after today
        var maxIterations = 100; // Safety limit to prevent infinite loops
        var iterations = 0;
        
        while (nextDate.Date < currentDate && iterations < maxIterations)
        {
            nextDate = CalculateNextRunDate(periodType, nextDate, anchorDayOfMonth);
            iterations++;
        }
        
        return nextDate;
    }

    /// <summary>
    /// Calculates the billing window (range start and end) based on the next run date.
    /// </summary>
    /// <param name="nextRunDate">The next run date (center of the window)</param>
    /// <param name="windowDaysBefore">Days before the next run date for the window start</param>
    /// <param name="windowDaysAfter">Days after the next run date for the window end</param>
    /// <returns>A tuple of (RangeStartDate, RangeEndDate)</returns>
    public static (DateTime RangeStart, DateTime RangeEnd) CalculateBillingWindow(
        DateTime nextRunDate,
        int windowDaysBefore,
        int windowDaysAfter)
    {
        var rangeStart = nextRunDate.AddDays(-windowDaysBefore);
        var rangeEnd = nextRunDate.AddDays(windowDaysAfter);
        return (rangeStart, rangeEnd);
    }

    /// <summary>
    /// Gets the default window days for a given period type.
    /// Returns the number of days before and after the next run date to use for the search window.
    /// </summary>
    /// <param name="periodType">The billing period type (Monthly, Bi-Monthly, Quarterly, etc.)</param>
    /// <returns>A tuple of (DaysBefore, DaysAfter) for the search window</returns>
    public static (int Before, int After) GetDefaultWindowDays(string? periodType)
    {
        return periodType?.ToLowerInvariant() switch
        {
            "bi-weekly" => (3, 3),
            "monthly" => (5, 5),
            "bi-monthly" => (7, 7),
            "quarterly" => (10, 10),
            "semi-annually" => (14, 14),
            "annually" => (21, 21),
            _ => (5, 5) // Default to monthly window
        };
    }

    /// <summary>
    /// Gets the approximate period days for a given period type.
    /// This is kept for backward compatibility and display purposes,
    /// but should NOT be used for date calculations (use CalculateNextRunDate instead).
    /// </summary>
    /// <param name="periodType">The billing period type (Monthly, Bi-Monthly, Quarterly, etc.)</param>
    /// <returns>The approximate number of days in the billing period</returns>
    public static int GetApproximatePeriodDays(string? periodType)
    {
        return periodType?.ToLowerInvariant() switch
        {
            "bi-weekly" => 14,
            "monthly" => 30,
            "bi-monthly" => 60,
            "quarterly" => 90,
            "semi-annually" => 180,
            "annually" => 365,
            _ => 30 // Default to monthly
        };
    }

    /// <summary>
    /// Detects if a bi-weekly schedule has drifted from its expected pattern.
    /// Returns true if the drift exceeds the threshold.
    /// </summary>
    /// <param name="calculatedNextDate">The calculated next run date</param>
    /// <param name="lastInvoiceDate">The last invoice date from historical data</param>
    /// <param name="medianDays">The median days between invoices from historical data</param>
    /// <param name="driftThresholdDays">The maximum allowed drift in days (default 3)</param>
    /// <returns>True if drift is detected and correction is needed</returns>
    public static bool DetectBiWeeklyDrift(
        DateTime calculatedNextDate,
        DateTime lastInvoiceDate,
        double medianDays,
        int driftThresholdDays = 3)
    {
        // Calculate what the expected date should be based on historical median
        var expectedDate = lastInvoiceDate.AddDays(medianDays);
        
        // Check if the calculated date has drifted too far from expected
        var driftDays = Math.Abs((calculatedNextDate - expectedDate).TotalDays);
        
        return driftDays > driftThresholdDays;
    }

    /// <summary>
    /// Corrects a bi-weekly schedule that has drifted by recalculating from the last invoice date.
    /// </summary>
    /// <param name="lastInvoiceDate">The last invoice date from historical data</param>
    /// <param name="medianDays">The median days between invoices</param>
    /// <param name="today">The current date</param>
    /// <returns>The corrected next run date</returns>
    public static DateTime CorrectBiWeeklyDrift(
        DateTime lastInvoiceDate,
        double medianDays,
        DateTime? today = null)
    {
        var currentDate = today ?? DateTime.UtcNow.Date;
        
        // Start from last invoice and add median days until we get a future date
        var nextDate = lastInvoiceDate.AddDays(medianDays);
        
        while (nextDate.Date < currentDate)
        {
            nextDate = nextDate.AddDays(medianDays);
        }
        
        return nextDate;
    }

    /// <summary>
    /// Extracts the anchor day of month from a date.
    /// For end-of-month dates, returns the actual day (28, 29, 30, or 31).
    /// </summary>
    /// <param name="date">The date to extract the anchor day from</param>
    /// <returns>The day of month (1-31)</returns>
    public static int GetAnchorDayOfMonth(DateTime date)
    {
        return date.Day;
    }

    /// <summary>
    /// Determines if a date is at the end of its month.
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>True if the date is the last day of its month, false otherwise</returns>
    public static bool IsEndOfMonth(DateTime date)
    {
        return date.Day == DateTime.DaysInMonth(date.Year, date.Month);
    }
}
