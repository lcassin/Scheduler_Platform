namespace SchedulerPlatform.API.Models;

/// <summary>
/// Response model for the current authenticated user's information.
/// Used for claims enrichment when using external identity providers.
/// </summary>
public class CurrentUserResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsSystemAdmin { get; set; }
    public string? Role { get; set; }
    public List<string>? Permissions { get; set; }
    public int ClientId { get; set; }
}
