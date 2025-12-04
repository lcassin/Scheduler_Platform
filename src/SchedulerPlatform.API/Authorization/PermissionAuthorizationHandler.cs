using Microsoft.AspNetCore.Authorization;

namespace SchedulerPlatform.API.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // System admins and Admin role users bypass all permission checks
        if (context.User.HasClaim("is_system_admin", "True") ||
            context.User.HasClaim("role", "Admin"))
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
