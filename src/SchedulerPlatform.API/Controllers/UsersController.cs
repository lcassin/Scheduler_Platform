using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.API.Models;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUnitOfWork unitOfWork, ILogger<UsersController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetUsers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (!HasUsersManagePermission())
            {
                return Forbid();
            }

            var query = _unitOfWork.Users.GetAllAsync().Result.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u =>
                    u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.FirstName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.LastName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = query.Count();
            var users = query
                .OrderBy(u => u.Email)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userResponses = new List<UserListItemResponse>();
            foreach (var user in users)
            {
                var permissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(user.Id);
                userResponses.Add(new UserListItemResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    IsSystemAdmin = user.IsSystemAdmin,
                    LastLoginAt = user.LastLoginAt,
                    PermissionCount = permissions.Count()
                });
            }

            return Ok(new
            {
                items = userResponses,
                totalCount = totalCount,
                pageNumber = pageNumber,
                pageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, "An error occurred while retrieving users");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailResponse>> GetUser(int id)
    {
        try
        {
            if (!HasUsersManagePermission())
            {
                return Forbid();
            }

            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var permissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(id);
            var permissionResponses = permissions.Select(p => new UserPermissionResponse
            {
                Id = p.Id,
                PermissionName = p.PermissionName,
                ResourceType = p.ResourceType,
                ResourceId = p.ResourceId,
                CanCreate = p.CanCreate,
                CanRead = p.CanRead,
                CanUpdate = p.CanUpdate,
                CanDelete = p.CanDelete,
                CanExecute = p.CanExecute
            }).ToList();

            var response = new UserDetailResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                IsActive = user.IsActive,
                IsSystemAdmin = user.IsSystemAdmin,
                LastLoginAt = user.LastLoginAt,
                ClientId = user.ClientId,
                Permissions = permissionResponses
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, "An error occurred while retrieving the user");
        }
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdateUserPermissions(int id, [FromBody] UpdateUserPermissionsRequest request)
    {
        try
        {
            if (!HasUsersManagePermission())
            {
                return Forbid();
            }

            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.IsSystemAdmin)
            {
                return BadRequest(new { message = "Cannot modify permissions for system administrators" });
            }

            var existingPermissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(id);

            foreach (var permission in existingPermissions)
            {
                await _unitOfWork.UserPermissions.DeleteAsync(permission);
            }

            foreach (var permissionRequest in request.Permissions)
            {
                var permission = new UserPermission
                {
                    UserId = id,
                    PermissionName = permissionRequest.PermissionName,
                    ResourceType = permissionRequest.ResourceType,
                    ResourceId = permissionRequest.ResourceId,
                    CanCreate = permissionRequest.CanCreate,
                    CanRead = permissionRequest.CanRead,
                    CanUpdate = permissionRequest.CanUpdate,
                    CanDelete = permissionRequest.CanDelete,
                    CanExecute = permissionRequest.CanExecute,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "System"
                };

                await _unitOfWork.UserPermissions.AddAsync(permission);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated permissions for user {UserId} by {UpdatedBy}", 
                id, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for user {UserId}", id);
            return StatusCode(500, "An error occurred while updating user permissions");
        }
    }

    [HttpPost("{id}/templates/{templateName}")]
    public async Task<IActionResult> ApplyPermissionTemplate(int id, string templateName)
    {
        try
        {
            if (!HasUsersManagePermission())
            {
                return Forbid();
            }

            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.IsSystemAdmin)
            {
                return BadRequest(new { message = "Cannot modify permissions for system administrators" });
            }

            var template = GetPermissionTemplate(templateName);
            if (template == null)
            {
                return BadRequest(new { message = $"Unknown template: {templateName}" });
            }

            var existingPermissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(id);

            foreach (var permission in existingPermissions)
            {
                await _unitOfWork.UserPermissions.DeleteAsync(permission);
            }

            foreach (var permissionRequest in template.Permissions)
            {
                var permission = new UserPermission
                {
                    UserId = id,
                    PermissionName = permissionRequest.PermissionName,
                    ResourceType = permissionRequest.ResourceType,
                    ResourceId = permissionRequest.ResourceId,
                    CanCreate = permissionRequest.CanCreate,
                    CanRead = permissionRequest.CanRead,
                    CanUpdate = permissionRequest.CanUpdate,
                    CanDelete = permissionRequest.CanDelete,
                    CanExecute = permissionRequest.CanExecute,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "System"
                };

                await _unitOfWork.UserPermissions.AddAsync(permission);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Applied template {TemplateName} to user {UserId} by {UpdatedBy}",
                templateName, id, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying template {TemplateName} to user {UserId}", templateName, id);
            return StatusCode(500, "An error occurred while applying the permission template");
        }
    }

    [HttpGet("templates")]
    public ActionResult<List<PermissionTemplateResponse>> GetPermissionTemplates()
    {
        try
        {
            if (!HasUsersManagePermission())
            {
                return Forbid();
            }

            var templates = new List<PermissionTemplateResponse>
            {
                GetPermissionTemplate("Viewer")!,
                GetPermissionTemplate("Editor")!,
                GetPermissionTemplate("Admin")!
            };

            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission templates");
            return StatusCode(500, "An error occurred while retrieving permission templates");
        }
    }

    private bool HasUsersManagePermission()
    {
        var isSystemAdminClaim = User.FindFirst("is_system_admin")?.Value;
        if (string.Equals(isSystemAdminClaim, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var permissionClaims = User.FindAll("permission");
        return permissionClaims.Any(c => c.Value == "users:manage");
    }

    private PermissionTemplateResponse? GetPermissionTemplate(string templateName)
    {
        return templateName.ToLower() switch
        {
            "viewer" => new PermissionTemplateResponse
            {
                Name = "Viewer",
                Description = "Read-only access to all resources",
                Permissions = new List<UserPermissionRequest>
                {
                    new() { PermissionName = "scheduler", CanRead = true },
                    new() { PermissionName = "schedules", CanRead = true },
                    new() { PermissionName = "jobs", CanRead = true }
                }
            },
            "editor" => new PermissionTemplateResponse
            {
                Name = "Editor",
                Description = "Full access to schedules and jobs",
                Permissions = new List<UserPermissionRequest>
                {
                    new() { PermissionName = "scheduler", CanRead = true },
                    new() { PermissionName = "schedules", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true, CanExecute = true },
                    new() { PermissionName = "jobs", CanRead = true }
                }
            },
            "admin" => new PermissionTemplateResponse
            {
                Name = "Admin",
                Description = "Full access to all resources including user management",
                Permissions = new List<UserPermissionRequest>
                {
                    new() { PermissionName = "scheduler", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
                    new() { PermissionName = "schedules", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true, CanExecute = true },
                    new() { PermissionName = "jobs", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true, CanExecute = true },
                    new() { PermissionName = "users", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
                    new() { PermissionName = "users:manage", CanRead = true }
                }
            },
            _ => null
        };
    }
}
