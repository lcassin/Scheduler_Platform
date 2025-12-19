using Microsoft.AspNetCore.Authorization;

namespace SchedulerPlatform.API.Authorization;

/// <summary>
/// Authorization requirement that only allows Super Admin users.
/// </summary>
public class SuperAdminRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Authorization handler that checks if the user has the is_system_admin claim set to true.
/// </summary>
public class SuperAdminAuthorizationHandler : AuthorizationHandler<SuperAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SuperAdminRequirement requirement)
    {
        var isSystemAdmin = context.User.Claims.Any(c =>
            c.Type == "is_system_admin" &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));

        if (isSystemAdmin)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
