using Microsoft.AspNetCore.Authorization;

namespace SchedulerPlatform.API.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // System admins and Admin role users bypass all permission checks
        // Check is_system_admin case-insensitively to handle both "true" and "True"
        var isSystemAdmin = context.User.Claims.Any(c =>
            c.Type == "is_system_admin" &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
        
        if (isSystemAdmin || context.User.HasClaim("role", "Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Otherwise require the specific permission claim
        if (context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
