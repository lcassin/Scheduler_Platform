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

    public SchedulerProfileService(
        IUserService userService,
        ILogger<SchedulerProfileService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subjectId = context.Subject.GetSubjectId();
        
        try
        {
            User? user = null;

            if (int.TryParse(subjectId, out var userId))
            {
                // Local user — look up by integer ID
                user = await _userService.GetUserByIdAsync(userId);
            }
            else
            {
                // External user (e.g. AAD) — subjectId is a hash, not an integer.
                // Try to find the user by email from subject claims, then by external ID.
                var email = context.Subject.FindFirst("email")?.Value
                         ?? context.Subject.FindFirst(ClaimTypes.Email)?.Value
                         ?? context.Subject.FindFirst("preferred_username")?.Value
                         ?? context.Subject.FindFirst("name")?.Value;

                if (!string.IsNullOrEmpty(email) && email.Contains('@'))
                {
                    user = await _userService.GetUserByEmailAsync(email);
                    if (user != null)
                    {
                        _logger.LogDebug("Resolved external user by email {Email} for subject {SubjectId}", email, subjectId);
                    }
                }

                // Fallback: try external ID patterns
                if (user == null)
                {
                    // Try looking up by the hash subject ID as external user ID
                    user = await _userService.GetUserByExternalIdAsync(subjectId);
                    if (user != null)
                    {
                        _logger.LogDebug("Resolved external user by external ID {ExternalId}", subjectId);
                    }
                }

                if (user == null)
                {
                    _logger.LogWarning("Could not resolve user for external subject {SubjectId}. No email or external ID match found.", subjectId);
                }
            }

            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim("sub", user.Id.ToString()),
                    new Claim("name", $"{user.FirstName} {user.LastName}"),
                    new Claim("given_name", user.FirstName),
                    new Claim("family_name", user.LastName),
                    new Claim("email", user.Email),
                    new Claim("user_client_id", user.ClientId.ToString()),
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
                    claims.Add(new Claim("is_system_admin", "True"));
                    claims.Add(new Claim("permission", "users:manage"));
                }

                context.IssuedClaims.AddRange(claims.Where(c => context.RequestedClaimTypes.Contains(c.Type)));
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
            User? user = null;

            if (int.TryParse(subjectId, out var userId))
            {
                user = await _userService.GetUserByIdAsync(userId);
            }
            else
            {
                // External user — try email then external ID (same as GetProfileDataAsync)
                var email = context.Subject.FindFirst("email")?.Value
                         ?? context.Subject.FindFirst(ClaimTypes.Email)?.Value
                         ?? context.Subject.FindFirst("preferred_username")?.Value
                         ?? context.Subject.FindFirst("name")?.Value;

                if (!string.IsNullOrEmpty(email) && email.Contains('@'))
                {
                    user = await _userService.GetUserByEmailAsync(email);
                }

                if (user == null)
                {
                    user = await _userService.GetUserByExternalIdAsync(subjectId);
                }
            }

            context.IsActive = user != null && user.IsActive && !user.IsDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {SubjectId} is active", subjectId);
            context.IsActive = false;
        }
    }
}
