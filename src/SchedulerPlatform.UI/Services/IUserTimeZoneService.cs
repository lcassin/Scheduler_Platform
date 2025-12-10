namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service for handling user timezone preferences and date/time display conversions.
/// All date/time displays in the application should use this service to ensure consistency.
/// </summary>
public interface IUserTimeZoneService
{
    /// <summary>
    /// Gets the user's preferred timezone ID (Windows timezone ID format).
    /// </summary>
    Task<string> GetUserTimeZoneIdAsync();
    
    /// <summary>
    /// Converts a UTC DateTime to the user's preferred timezone.
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to convert.</param>
    /// <returns>The DateTime in the user's timezone.</returns>
    Task<DateTime> ToUserTimeAsync(DateTime utcDateTime);
    
    /// <summary>
    /// Gets the abbreviation for the user's timezone (e.g., "ET", "CT", "PT").
    /// </summary>
    Task<string> GetUserTimeZoneAbbreviationAsync();
    
    /// <summary>
    /// Formats a UTC DateTime for display in the user's timezone.
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to format.</param>
    /// <param name="format">The format string (default: "g" for short date/time).</param>
    /// <returns>Formatted string with timezone abbreviation.</returns>
    Task<string> FormatDateTimeAsync(DateTime utcDateTime, string format = "g");
    
    /// <summary>
    /// Formats a nullable UTC DateTime for display in the user's timezone.
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to format, or null.</param>
    /// <param name="format">The format string (default: "g" for short date/time).</param>
    /// <param name="nullDisplay">Text to display when the value is null (default: "N/A").</param>
    /// <returns>Formatted string with timezone abbreviation, or nullDisplay if null.</returns>
    Task<string> FormatDateTimeAsync(DateTime? utcDateTime, string format = "g", string nullDisplay = "N/A");
}
