using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace SchedulerPlatform.IdentityServer.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;
    private readonly PasswordHasher<User> _passwordHasher;
    private const int MaxPasswordHistory = 10;

    public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _passwordHasher = new PasswordHasher<User>();
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

    public async Task<(bool IsValid, User? User)> ValidateCredentialsAsync(string emailOrUsername, string password)
    {
        try
        {
            var users = await _unitOfWork.Users.FindAsync(u => 
                (u.Email == emailOrUsername || u.Username == emailOrUsername) && 
                !u.IsDeleted && 
                u.IsActive);
            
            var user = users.FirstOrDefault();
            
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                _logger.LogWarning("Login attempt for non-existent or external user: {EmailOrUsername}", emailOrUsername);
                return (false, null);
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            
            if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await UpdateUserAsync(user);
                
                _logger.LogInformation("Successful login for user: {Email}", user.Email);
                return (true, user);
            }

            _logger.LogWarning("Failed login attempt for user: {Email}", user.Email);
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for {EmailOrUsername}", emailOrUsername);
            return (false, null);
        }
    }

    public async Task<bool> CanReusePasswordAsync(int userId, string newPassword)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null) return true;

            var passwordHistories = await _unitOfWork.PasswordHistories.FindAsync(
                ph => ph.UserId == userId && !ph.IsDeleted);
            
            var recentPasswords = passwordHistories
                .OrderByDescending(ph => ph.ChangedAt)
                .Take(MaxPasswordHistory)
                .ToList();

            foreach (var history in recentPasswords)
            {
                var result = _passwordHasher.VerifyHashedPassword(user, history.PasswordHash, newPassword);
                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    _logger.LogWarning("User {UserId} attempted to reuse a recent password", userId);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking password reuse for user {UserId}", userId);
            return true;
        }
    }

    public async Task AddPasswordToHistoryAsync(int userId, string passwordHash)
    {
        try
        {
            var passwordHistory = new PasswordHistory
            {
                UserId = userId,
                PasswordHash = passwordHash,
                ChangedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false
            };

            await _unitOfWork.PasswordHistories.AddAsync(passwordHistory);
            await _unitOfWork.SaveChangesAsync();

            var allHistories = await _unitOfWork.PasswordHistories.FindAsync(
                ph => ph.UserId == userId && !ph.IsDeleted);
            
            var historiesToDelete = allHistories
                .OrderByDescending(ph => ph.ChangedAt)
                .Skip(MaxPasswordHistory)
                .ToList();

            foreach (var history in historiesToDelete)
            {
                history.IsDeleted = true;
                await _unitOfWork.PasswordHistories.UpdateAsync(history);
            }

            if (historiesToDelete.Any())
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding password to history for user {UserId}", userId);
            throw;
        }
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
