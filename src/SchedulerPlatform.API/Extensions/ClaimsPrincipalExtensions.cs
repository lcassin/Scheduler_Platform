using System.Security.Claims;

namespace SchedulerPlatform.API.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to simplify authorization checks.
/// Consolidates common authorization patterns used across controllers.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Checks if the user is a System Admin (Super Admin).
    /// System Admins have full access to all features.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>True if the user is a System Admin.</returns>
    public static bool IsSystemAdmin(this ClaimsPrincipal user)
    {
        var isSystemAdminValue = user.FindFirst("is_system_admin")?.Value;
        return string.Equals(isSystemAdminValue, "True", StringComparison.OrdinalIgnoreCase) 
            || isSystemAdminValue == "1";
    }

    /// <summary>
    /// Checks if the user has Admin role.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>True if the user has Admin role.</returns>
    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        var userRole = user.FindFirst("role")?.Value;
        return userRole == "Admin" || userRole == "Super Admin";
    }

    /// <summary>
    /// Checks if the user is Admin or above (Admin, Super Admin, or System Admin).
    /// This is the most common authorization check for administrative features.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>True if the user is Admin or above.</returns>
    public static bool IsAdminOrAbove(this ClaimsPrincipal user)
    {
        return user.IsSystemAdmin() || user.IsAdmin();
    }

    /// <summary>
    /// Gets the user's client ID from claims.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>The client ID as a string, or null if not found.</returns>
    public static string? GetClientId(this ClaimsPrincipal user)
    {
        return user.FindFirst("user_client_id")?.Value;
    }

    /// <summary>
    /// Gets the user's client ID as an integer.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>The client ID as an integer, or null if not found or invalid.</returns>
    public static int? GetClientIdAsInt(this ClaimsPrincipal user)
    {
        var clientIdStr = user.GetClientId();
        if (int.TryParse(clientIdStr, out var clientId))
        {
            return clientId;
        }
        return null;
    }

    /// <summary>
    /// Gets the user's role from claims.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>The role as a string, or null if not found.</returns>
    public static string? GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirst("role")?.Value;
    }

    /// <summary>
    /// Gets the user's email from claims.
    /// Checks multiple claim types that may contain the email.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>The email as a string, or null if not found.</returns>
    public static string? GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst("email")?.Value 
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets the user's display name from claims.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <returns>The display name, or the email if name is not available.</returns>
    public static string GetDisplayName(this ClaimsPrincipal user)
    {
        return user.Identity?.Name 
            ?? user.GetEmail() 
            ?? "Unknown";
    }

    /// <summary>
    /// Checks if the user can access a resource belonging to a specific client.
    /// Admins and System Admins can access any client's resources.
    /// Regular users can only access their own client's resources.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <param name="resourceClientId">The client ID of the resource being accessed.</param>
    /// <returns>True if the user can access the resource.</returns>
    public static bool CanAccessClient(this ClaimsPrincipal user, int resourceClientId)
    {
        if (user.IsAdminOrAbove())
        {
            return true;
        }

        var userClientId = user.GetClientIdAsInt();
        return userClientId.HasValue && userClientId.Value == resourceClientId;
    }

    /// <summary>
    /// Checks if the user can access a system schedule.
    /// Only Admins and System Admins can access system schedules.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal to check.</param>
    /// <param name="isSystemSchedule">Whether the schedule is a system schedule.</param>
    /// <param name="scheduleClientId">The client ID of the schedule.</param>
    /// <returns>True if the user can access the schedule.</returns>
    public static bool CanAccessSchedule(this ClaimsPrincipal user, bool isSystemSchedule, int scheduleClientId)
    {
        if (isSystemSchedule)
        {
            return user.IsAdminOrAbove();
        }

        return user.CanAccessClient(scheduleClientId);
    }
}
