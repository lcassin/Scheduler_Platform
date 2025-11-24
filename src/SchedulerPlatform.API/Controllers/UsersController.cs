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
    [Authorize(Policy = "Users.Manage.Read")]
    public async Task<ActionResult<object>> GetUsers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {

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
                var permissionsList = permissions.ToList();
                userResponses.Add(new UserListItemResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    IsSystemAdmin = user.IsSystemAdmin,
                    LastLoginAt = user.LastLoginAt,
                    PermissionCount = permissionsList.Count,
                    Role = DetermineUserRole(user, permissionsList)
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
    [Authorize(Policy = "Users.Manage.Read")]
    public async Task<ActionResult<UserDetailResponse>> GetUser(int id)
    {
        try
        {

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
    [Authorize(Policy = "Users.Manage.Update")]
    public async Task<IActionResult> UpdateUserPermissions(int id, [FromBody] UpdateUserPermissionsRequest request)
    {
        try
        {

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
    [Authorize(Policy = "Users.Manage.Update")]
    public async Task<IActionResult> ApplyPermissionTemplate(int id, string templateName)
    {
        try
        {

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

    [HttpPost]
    [Authorize(Policy = "Users.Manage.Create")]
    public async Task<ActionResult<UserDetailResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var existingUsers = await _unitOfWork.Users.GetAllAsync();
            if (existingUsers.Any(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { message = "A user with this email already exists" });
            }

            var user = new User
            {
                Email = request.Email,
                Username = request.Username ?? request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = request.IsActive,
                IsSystemAdmin = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",
                IsDeleted = false
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created user {Email} with ID {UserId} by {CreatedBy}", 
                request.Email, user.Id, User.Identity?.Name ?? "System");

            if (!string.IsNullOrEmpty(request.TemplateName))
            {
                var template = GetPermissionTemplate(request.TemplateName);
                if (template != null)
                {
                    foreach (var permissionRequest in template.Permissions)
                    {
                        var permission = new UserPermission
                        {
                            UserId = user.Id,
                            PermissionName = permissionRequest.PermissionName,
                            ResourceType = permissionRequest.ResourceType,
                            ResourceId = permissionRequest.ResourceId,
                            CanCreate = permissionRequest.CanCreate,
                            CanRead = permissionRequest.CanRead,
                            CanUpdate = permissionRequest.CanUpdate,
                            CanDelete = permissionRequest.CanDelete,
                            CanExecute = permissionRequest.CanExecute,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = User.Identity?.Name ?? "System",
                            IsDeleted = false
                        };

                        await _unitOfWork.UserPermissions.AddAsync(permission);
                    }

                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Applied template {TemplateName} to new user {UserId}", 
                        request.TemplateName, user.Id);
                }
            }

            var permissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(user.Id);
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

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "An error occurred while creating the user");
        }
    }

    [HttpGet("templates")]
    [Authorize(Policy = "Users.Manage.Read")]
    public ActionResult<List<PermissionTemplateResponse>> GetPermissionTemplates()
    {
        try
        {

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
                Description = "Full access to all resources including permission management",
                Permissions = new List<UserPermissionRequest>
                {
                    new() { PermissionName = "scheduler", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
                    new() { PermissionName = "schedules", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true, CanExecute = true },
                    new() { PermissionName = "jobs", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true, CanExecute = true },
                    new() { PermissionName = "users:manage", CanRead = true, CanUpdate = true }
                }
            },
            _ => null
        };
    }

    private string DetermineUserRole(User user, List<UserPermission> permissions)
    {
        if (user.IsSystemAdmin)
            return "Super Admin";

        if (permissions.Count == 0)
            return "No Access";

        var adminTemplate = GetPermissionTemplate("Admin");
        if (adminTemplate != null && MatchesTemplate(permissions, adminTemplate.Permissions))
            return "Admin";

        var editorTemplate = GetPermissionTemplate("Editor");
        if (editorTemplate != null && MatchesTemplate(permissions, editorTemplate.Permissions))
            return "Editor";

        var viewerTemplate = GetPermissionTemplate("Viewer");
        if (viewerTemplate != null && MatchesTemplate(permissions, viewerTemplate.Permissions))
            return "Viewer";

        return "Custom";
    }

    private bool MatchesTemplate(List<UserPermission> userPermissions, List<UserPermissionRequest> templatePermissions)
    {
        if (userPermissions.Count != templatePermissions.Count)
            return false;

        foreach (var templatePerm in templatePermissions)
        {
            var userPerm = userPermissions.FirstOrDefault(p => 
                string.Equals(p.PermissionName, templatePerm.PermissionName, StringComparison.OrdinalIgnoreCase));
            
            if (userPerm == null)
                return false;

            if (userPerm.CanCreate != templatePerm.CanCreate ||
                userPerm.CanRead != templatePerm.CanRead ||
                userPerm.CanUpdate != templatePerm.CanUpdate ||
                userPerm.CanDelete != templatePerm.CanDelete ||
                userPerm.CanExecute != templatePerm.CanExecute)
                return false;
        }

        return true;
    }
}
