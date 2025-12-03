using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Middleware;

public class AutoUserCreationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AutoUserCreationMiddleware> _logger;

    public AutoUserCreationMiddleware(RequestDelegate next, ILogger<AutoUserCreationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                var email = context.User.FindFirst("email")?.Value 
                           ?? context.User.FindFirst("preferred_username")?.Value
                           ?? context.User.FindFirst("upn")?.Value;
                
                var firstName = context.User.FindFirst("given_name")?.Value ?? "";
                var lastName = context.User.FindFirst("family_name")?.Value ?? "";
                var username = context.User.FindFirst("name")?.Value 
                              ?? context.User.FindFirst("preferred_username")?.Value 
                              ?? email;

                if (!string.IsNullOrEmpty(email))
                {
                    var existingUser = await unitOfWork.Users.GetAllAsync();
                    var user = existingUser.FirstOrDefault(u => 
                        string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

                    if (user == null)
                    {
                        user = new User
                        {
                            Email = email,
                            Username = username ?? email,
                            FirstName = firstName,
                            LastName = lastName,
                            IsActive = true,
                            IsSystemAdmin = false,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedBy = "System",
                            IsDeleted = false
                        };

                        await unitOfWork.Users.AddAsync(user);
                        await unitOfWork.SaveChangesAsync();

                        _logger.LogInformation("Auto-created user {Email} with ID {UserId}", email, user.Id);

                        await AssignViewerPermissions(user.Id, unitOfWork);
                        
                        _logger.LogInformation("Assigned Viewer permissions to new user {Email}", email);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-user creation middleware");
            }
        }

        await _next(context);
    }

    private async Task AssignViewerPermissions(int userId, IUnitOfWork unitOfWork)
    {
        var viewerPermissions = new[]
        {
            new UserPermission
            {
                UserId = userId,
                PermissionName = "scheduler",
                CanRead = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false
            },
            new UserPermission
            {
                UserId = userId,
                PermissionName = "schedules",
                CanRead = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false
            },
            new UserPermission
            {
                UserId = userId,
                PermissionName = "jobs",
                CanRead = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = "System",
                IsDeleted = false
            }
        };

        foreach (var permission in viewerPermissions)
        {
            await unitOfWork.UserPermissions.AddAsync(permission);
        }

        await unitOfWork.SaveChangesAsync();
    }
}
