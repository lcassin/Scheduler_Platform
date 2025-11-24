namespace SchedulerPlatform.API.Models;

public class UserListItemResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsActive { get; set; }
    public bool IsSystemAdmin { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int PermissionCount { get; set; }
    public string Role { get; set; } = string.Empty;
}
