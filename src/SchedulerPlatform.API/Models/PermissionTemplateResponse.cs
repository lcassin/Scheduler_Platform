namespace SchedulerPlatform.API.Models;

public class PermissionTemplateResponse
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<UserPermissionRequest> Permissions { get; set; } = new();
}
