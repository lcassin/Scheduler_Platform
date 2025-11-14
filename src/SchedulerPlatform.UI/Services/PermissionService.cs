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

        if (user.HasClaim("is_system_admin", "true"))
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

        return user.HasClaim("is_system_admin", "true");
    }
}
