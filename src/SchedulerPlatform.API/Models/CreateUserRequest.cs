namespace SchedulerPlatform.API.Models;

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TemplateName { get; set; }
}
