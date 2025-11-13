using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.IdentityServer.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        try
        {
            return await _unitOfWork.Users.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by ID {UserId}", id);
            return null;
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        try
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.Username == username && !u.IsDeleted);
            return users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by username {Username}", username);
            return null;
        }
    }

    public async Task<User?> GetUserByExternalIdAsync(string externalId)
    {
        try
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.ExternalUserId == externalId && !u.IsDeleted);
            return users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by external ID {ExternalId}", externalId);
            return null;
        }
    }

    public async Task<string> GetUserRoleAsync(int userId)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return "Guest";

            var userPermissions = await _unitOfWork.UserPermissions.FindAsync(
                p => p.UserId == userId && 
                p.PermissionName == "Admin" && 
                !p.IsDeleted);

            if (userPermissions.Any())
                return "Admin";

            return "Client";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role for user {UserId}", userId);
            return "Guest";
        }
    }

    public Task<bool> ValidateUserCredentialsAsync(string username, string password)
    {
        return Task.FromResult(false);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        try
        {
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", user.Username);
            throw;
        }
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        try
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", user.Id);
            throw;
        }
    }

    public async Task AssignDefaultPermissionsAsync(int userId)
    {
        try
        {
            var defaultPermission = new UserPermission
            {
                UserId = userId,
                PermissionName = "scheduler",
                CanRead = true,
                CanCreate = false,
                CanUpdate = false,
                CanDelete = false,
                CanExecute = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false
            };

            await _unitOfWork.UserPermissions.AddAsync(defaultPermission);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning default permissions to user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<UserPermission>> GetUserPermissionsAsync(int userId)
    {
        try
        {
            return await _unitOfWork.UserPermissions.FindAsync(
                p => p.UserId == userId && !p.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for user {UserId}", userId);
            return Enumerable.Empty<UserPermission>();
        }
    }
}
