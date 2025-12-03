namespace SchedulerPlatform.UI.Models;

public class UserDetail
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Username { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSystemAdmin { get; set; }
    public DateTime? LastLoginDateTime { get; set; }
    public int ClientId { get; set; }
    public List<UserPermissionDto> Permissions { get; set; } = new();
}
