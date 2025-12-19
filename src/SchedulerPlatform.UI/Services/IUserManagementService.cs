using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public interface IUserManagementService
{
    Task<PagedResult<UserListItem>> GetUsersAsync(string? searchTerm, int pageNumber, int pageSize, bool showInactive = false);
    Task<UserDetail?> GetUserAsync(int id);
    Task UpdateUserPermissionsAsync(int id, List<UserPermissionDto> permissions);
    Task ApplyPermissionTemplateAsync(int id, string templateName);
    Task<List<PermissionTemplate>> GetPermissionTemplatesAsync();
    Task<UserDetail> CreateUserAsync(CreateUserRequest request);
    Task UpdateUserStatusAsync(int id, bool isActive);
    Task UpdateSuperAdminStatusAsync(int id, bool isSuperAdmin);
}
