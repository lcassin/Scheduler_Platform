namespace SchedulerPlatform.UI.Models;

public class PermissionTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<UserPermissionDto> Permissions { get; set; } = new();
}
