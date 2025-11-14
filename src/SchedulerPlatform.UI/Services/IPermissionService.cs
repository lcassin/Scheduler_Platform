namespace SchedulerPlatform.UI.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string permission);
    Task<bool> CanCreateAsync(string resource);
    Task<bool> CanReadAsync(string resource);
    Task<bool> CanUpdateAsync(string resource);
    Task<bool> CanDeleteAsync(string resource);
    Task<bool> CanExecuteAsync(string resource);
    Task<bool> IsSystemAdminAsync();
}
