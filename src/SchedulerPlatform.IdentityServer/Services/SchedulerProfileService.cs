using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using System.Security.Claims;
using SchedulerPlatform.IdentityServer.Services;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.IdentityServer.Services;

public class SchedulerProfileService : IProfileService
{
    private readonly IUserService _userService;
    private readonly ILogger<SchedulerProfileService> _logger;
    private readonly IWebHostEnvironment _environment;

    public SchedulerProfileService(
        IUserService userService,
        ILogger<SchedulerProfileService> logger,
        IWebHostEnvironment environment)
    {
        _userService = userService;
        _logger = logger;
        _environment = environment;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subjectId = context.Subject.GetSubjectId();
        
        try
        {
            var testUser = Config.Users.FirstOrDefault(u => u.SubjectId == subjectId);
            if (testUser != null)
            {
                if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Staging") && !_environment.IsEnvironment("UAT"))
                {
                    _logger.LogWarning("Test user {SubjectId} attempted login in {Environment} environment - DENIED", 
                        subjectId, _environment.EnvironmentName);
                    context.IssuedClaims.Clear();
                    return;
                }
                
                _logger.LogInformation("Test user {SubjectId} detected in {Environment}, loading permissions from database", 
                    subjectId, _environment.EnvironmentName);
                
                var email = testUser.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    var dbUser = await _userService.GetUserByUsernameAsync(email);
                    if (dbUser != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim("sub", testUser.SubjectId),
                            new Claim("name", $"{dbUser.FirstName} {dbUser.LastName}"),
                            new Claim("given_name", dbUser.FirstName),
                            new Claim("family_name", dbUser.LastName),
                            new Claim("email", dbUser.Email),
                            new Claim("client_id", dbUser.ClientId.ToString()),
                            new Claim("role", await _userService.GetUserRoleAsync(dbUser.Id)),
                            new Claim("test_user", "true")
                        };

                        var permissions = await _userService.GetUserPermissionsAsync(dbUser.Id);
                        foreach (var perm in permissions)
                        {
                            if (perm.CanRead)
                                claims.Add(new Claim("permission", $"{perm.PermissionName}:read"));
                            if (perm.CanCreate)
                                claims.Add(new Claim("permission", $"{perm.PermissionName}:create"));
                            if (perm.CanUpdate)
                                claims.Add(new Claim("permission", $"{perm.PermissionName}:update"));
                            if (perm.CanDelete)
                                claims.Add(new Claim("permission", $"{perm.PermissionName}:delete"));
                            if (perm.CanExecute)
                                claims.Add(new Claim("permission", $"{perm.PermissionName}:execute"));
                        }

                        if (dbUser.IsSystemAdmin)
                        {
                            claims.Add(new Claim("permission", "users:manage"));
                        }

                        context.IssuedClaims.AddRange(claims.Where(c => context.RequestedClaimTypes.Contains(c.Type)));
                        return;
                    }
                }
                
                _logger.LogWarning("Test user {SubjectId} has no matching database user, using in-memory claims only", subjectId);
                var fallbackClaims = context.Subject.Claims.ToList();
                context.IssuedClaims.AddRange(fallbackClaims);
                return;
            }
            
            if (int.TryParse(subjectId, out var userId))
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim("sub", user.Id.ToString()),
                        new Claim("name", $"{user.FirstName} {user.LastName}"),
                        new Claim("given_name", user.FirstName),
                        new Claim("family_name", user.LastName),
                        new Claim("email", user.Email),
                        new Claim("client_id", user.ClientId.ToString()),
                        new Claim("role", await _userService.GetUserRoleAsync(user.Id))
                    };

                    var permissions = await _userService.GetUserPermissionsAsync(user.Id);
                    foreach (var perm in permissions)
                    {
                        if (perm.CanRead)
                            claims.Add(new Claim("permission", $"{perm.PermissionName}:read"));
                        if (perm.CanCreate)
                            claims.Add(new Claim("permission", $"{perm.PermissionName}:create"));
                        if (perm.CanUpdate)
                            claims.Add(new Claim("permission", $"{perm.PermissionName}:update"));
                        if (perm.CanDelete)
                            claims.Add(new Claim("permission", $"{perm.PermissionName}:delete"));
                        if (perm.CanExecute)
                            claims.Add(new Claim("permission", $"{perm.PermissionName}:execute"));
                    }

                    if (user.IsSystemAdmin)
                    {
                        claims.Add(new Claim("permission", "users:manage"));
                    }

                    context.IssuedClaims.AddRange(claims.Where(c => context.RequestedClaimTypes.Contains(c.Type)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile data for user {SubjectId}", subjectId);
        }
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var subjectId = context.Subject.GetSubjectId();

        try
        {
            if (Config.Users.Any(u => u.SubjectId == subjectId))
            {
                if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Staging") && !_environment.IsEnvironment("UAT"))
                {
                    _logger.LogWarning("Test user {SubjectId} attempted login in {Environment} environment - DENIED", 
                        subjectId, _environment.EnvironmentName);
                    context.IsActive = false;
                    return;
                }
                
                _logger.LogInformation("Test user {SubjectId} detected in {Environment}, marking as active", 
                    subjectId, _environment.EnvironmentName);
                context.IsActive = true;
                return;
            }

            if (int.TryParse(subjectId, out var userId))
            {
                var user = await _userService.GetUserByIdAsync(userId);
                context.IsActive = user != null && user.IsActive && !user.IsDeleted;
            }
            else
            {
                context.IsActive = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {SubjectId} is active", subjectId);
            context.IsActive = false;
        }
    }
}
