namespace SchedulerPlatform.API.Models;

/// <summary>
/// Request model for updating a user's Super Admin status.
/// </summary>
public class UpdateSuperAdminStatusRequest
{
    /// <summary>
    /// Whether the user should be a Super Admin.
    /// </summary>
    public bool IsSuperAdmin { get; set; }
}
