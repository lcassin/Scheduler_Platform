namespace SchedulerPlatform.UI.Models;

public class UserPermissionDto
{
    public int Id { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public int? ResourceId { get; set; }
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanExecute { get; set; }
}
