namespace SchedulerPlatform.API.Models;

public class UserDetailResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSystemAdmin { get; set; }
    public DateTime? LastLoginDateTime { get; set; }
    public int ClientId { get; set; }
    public List<UserPermissionResponse> Permissions { get; set; } = new();
}
