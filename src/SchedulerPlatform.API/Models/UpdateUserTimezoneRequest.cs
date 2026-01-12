namespace SchedulerPlatform.API.Models;

/// <summary>
/// Request model for updating a user's preferred timezone.
/// </summary>
public class UpdateUserTimezoneRequest
{
    /// <summary>
    /// The Windows timezone ID (e.g., "Eastern Standard Time", "Central Standard Time").
    /// Set to null to clear the preference and use browser detection.
    /// </summary>
    public string? PreferredTimeZone { get; set; }
}
