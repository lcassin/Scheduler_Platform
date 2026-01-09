using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service for checking user permissions.
/// Uses cached permissions from UserPermissionCacheService when available,
/// falls back to claims-based checks when cache is not populated.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly UserPermissionCacheService _permissionCache;

    public PermissionService(
        AuthenticationStateProvider authenticationStateProvider,
        UserPermissionCacheService permissionCache)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _permissionCache = permissionCache;
    }

    public async Task<bool> HasPermissionAsync(string permission)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
            return false;

        // Use cached permissions if available (preferred - includes enriched permissions from API)
        if (_permissionCache.CachedPermissions != null)
        {
            return _permissionCache.HasPermission(permission);
        }

        // Fall back to claims-based check (may not have enriched permissions)
        var isSystemAdmin = user.Claims.Any(c =>
            c.Type == "is_system_admin" &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
        
        if (isSystemAdmin)
            return true;

        return user.HasClaim("permission", permission);
    }

    public async Task<bool> CanCreateAsync(string resource)
    {
        return await HasPermissionAsync($"{resource}:create");
    }

    public async Task<bool> CanReadAsync(string resource)
    {
        return await HasPermissionAsync($"{resource}:read");
    }

    public async Task<bool> CanUpdateAsync(string resource)
    {
        return await HasPermissionAsync($"{resource}:update");
    }

    public async Task<bool> CanDeleteAsync(string resource)
    {
        return await HasPermissionAsync($"{resource}:delete");
    }

    public async Task<bool> CanExecuteAsync(string resource)
    {
        return await HasPermissionAsync($"{resource}:execute");
    }

    public async Task<bool> IsSystemAdminAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
            return false;

        // Use cached permissions if available
        if (_permissionCache.CachedPermissions != null)
        {
            return _permissionCache.IsSystemAdmin();
        }

        // Fall back to claims-based check
        return user.Claims.Any(c =>
            c.Type == "is_system_admin" &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsAdminOrAboveAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
            return false;

        // Use cached permissions if available
        if (_permissionCache.CachedPermissions != null)
        {
            return _permissionCache.IsAdminOrAbove();
        }

        // Fall back to claims-based check
        var isSystemAdmin = user.Claims.Any(c =>
            c.Type == "is_system_admin" &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
        
        if (isSystemAdmin)
            return true;

        var role = user.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        return role == "Admin";
    }
}
