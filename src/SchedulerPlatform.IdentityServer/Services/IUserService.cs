using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.IdentityServer.Services;

public interface IUserService
{
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByExternalIdAsync(string externalId);
    Task<string> GetUserRoleAsync(int userId);
    Task<bool> ValidateUserCredentialsAsync(string username, string password);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task AssignDefaultPermissionsAsync(int userId);
    Task<IEnumerable<UserPermission>> GetUserPermissionsAsync(int userId);
}
