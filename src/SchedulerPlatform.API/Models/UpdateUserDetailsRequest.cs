namespace SchedulerPlatform.API.Models;

/// <summary>
/// Request model for updating user details (email, name, timezone).
/// </summary>
public class UpdateUserDetailsRequest
{
    /// <summary>
    /// The user's email address. If changed, must be unique across all users.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The user's first name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// The user's last name.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// The Windows timezone ID (e.g., "Eastern Standard Time", "Central Standard Time").
    /// Set to null to use browser detection.
    /// </summary>
    public string? PreferredTimeZone { get; set; }

    /// <summary>
    /// Set to true to explicitly clear the timezone preference (set to null).
    /// This is needed because null in PreferredTimeZone could mean "don't change" or "clear".
    /// </summary>
    public bool ClearTimezone { get; set; }
}
