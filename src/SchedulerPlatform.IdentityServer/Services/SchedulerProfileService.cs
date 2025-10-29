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
            if (Config.Users.Any(u => u.SubjectId == subjectId))
            {
                _logger.LogInformation("Test user {SubjectId} detected, using in-memory claims", subjectId);
                var claims = context.Subject.Claims.ToList();
                context.IssuedClaims.AddRange(claims);
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
                _logger.LogInformation("Test user {SubjectId} detected, skipping database check", subjectId);
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
