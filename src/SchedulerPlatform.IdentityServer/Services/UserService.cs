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
}
