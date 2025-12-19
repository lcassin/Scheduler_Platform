using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace SchedulerPlatform.UI.Services;

public class PermissionService : IPermissionService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public PermissionService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<bool> HasPermissionAsync(string permission)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
            return false;

        // Check is_system_admin case-insensitively to handle both "true" and "True"
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

        // Check is_system_admin case-insensitively to handle both "true" and "True"
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

        // Check is_system_admin (Super Admin) case-insensitively
        var isSystemAdmin = user.Claims.Any(c =>
            c.Type == "is_system_admin" &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
        
        if (isSystemAdmin)
            return true;

        // Check for Admin role
        var role = user.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        return role == "Admin";
    }
}
