using Microsoft.AspNetCore.Authentication;
using SchedulerPlatform.Core.Domain.Interfaces;
using System.Security.Claims;

namespace SchedulerPlatform.API.Authorization;

/// <summary>
/// Transforms claims by enriching the principal with user permissions from the database.
/// This is necessary when using an external identity provider (like corporate Duende) that
/// doesn't have access to the application's user table.
/// </summary>
public class UserClaimsTransformation : IClaimsTransformation
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserClaimsTransformation> _logger;

    public UserClaimsTransformation(
        IServiceProvider serviceProvider,
        ILogger<UserClaimsTransformation> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var existingIsSystemAdmin = principal.Claims.FirstOrDefault(c => c.Type == "is_system_admin");
        if (existingIsSystemAdmin != null)
        {
            return principal;
        }

        var email = principal.FindFirst("email")?.Value
                   ?? principal.FindFirst("preferred_username")?.Value
                   ?? principal.FindFirst("upn")?.Value;

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("No email claim found for authenticated user. Cannot enrich claims.");
            return principal;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var users = await unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                _logger.LogDebug("User with email {Email} not found in database. Using default claims.", email);
                return principal;
            }

            var identity = principal.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return principal;
            }

            if (user.IsSystemAdmin)
            {
                identity.AddClaim(new Claim("is_system_admin", "true"));
                _logger.LogDebug("Added is_system_admin claim for user {Email}", email);
            }

            var permissions = await unitOfWork.UserPermissions.GetAllAsync();
            var userPermissions = permissions.Where(p => p.UserId == user.Id && !p.IsDeleted).ToList();

            foreach (var perm in userPermissions)
            {
                if (perm.CanRead)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:read"));
                if (perm.CanCreate)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:create"));
                if (perm.CanUpdate)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:update"));
                if (perm.CanDelete)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:delete"));
                if (perm.CanExecute)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:execute"));
            }

            var role = user.IsSystemAdmin ? "Super Admin" : 
                       userPermissions.Any(p => p.CanCreate || p.CanUpdate || p.CanDelete) ? "Editor" : "Viewer";
            identity.AddClaim(new Claim("role", role));
            identity.AddClaim(new Claim("user_client_id", user.ClientId.ToString()));

            _logger.LogDebug("Enriched claims for user {Email}: IsSystemAdmin={IsSystemAdmin}, Role={Role}, Permissions={PermissionCount}",
                email, user.IsSystemAdmin, role, userPermissions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching claims for user {Email}", email);
        }

        return principal;
    }
}
