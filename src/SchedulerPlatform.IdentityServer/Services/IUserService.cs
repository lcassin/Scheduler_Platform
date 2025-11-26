using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.IdentityServer.Services;

public interface IUserService
{
    Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByExternalIdAsync(string externalId);
    Task<string> GetUserRoleAsync(int userId);
    Task<(bool IsValid, User? User)> ValidateCredentialsAsync(string emailOrUsername, string password);
    Task<bool> CanReusePasswordAsync(int userId, string newPassword);
    Task AddPasswordToHistoryAsync(int userId, string passwordHash);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task AssignDefaultPermissionsAsync(int userId);
    Task<IEnumerable<UserPermission>> GetUserPermissionsAsync(int userId);
    Task SetMustChangePasswordAsync(int userId, bool mustChange);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}
