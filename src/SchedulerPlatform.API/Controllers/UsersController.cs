using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.API.Models;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Security;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing users and their permissions.
/// Provides endpoints for user CRUD operations, permission management, and password resets.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsersController> _logger;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
	private readonly IWebHostEnvironment _env;

	public UsersController(
        IUnitOfWork unitOfWork, 
        ILogger<UsersController> logger, 
        IEmailService emailService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
		IWebHostEnvironment env	)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _emailService = emailService;
		_env = env;
		_passwordHasher = new PasswordHasher<User>();
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Retrieves a paginated list of users with optional filtering and sorting. Requires Users.Manage.Read policy.
    /// </summary>
    /// <param name="searchTerm">Optional search term to filter by email, first name, last name, or username.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 50).</param>
    /// <param name="showInactive">Whether to include inactive users (default: false).</param>
    /// <param name="sortColumn">Column to sort by: Email, FirstName, LastName, IsActive, LastLoginDateTime, PreferredTimeZone (default: LastName).</param>
    /// <param name="sortDescending">Whether to sort in descending order (default: false).</param>
    /// <returns>A paginated list of users with their permission counts and roles.</returns>
    /// <response code="200">Returns the paginated list of users.</response>
    /// <response code="500">An error occurred while retrieving users.</response>
    [HttpGet]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetUsers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool showInactive = false,
        [FromQuery] string sortColumn = "LastName",
        [FromQuery] bool sortDescending = false)
    {
        try
        {

            var query = _unitOfWork.Users.GetAllAsync().Result.AsQueryable();

            if (!showInactive)
            {
                query = query.Where(u => u.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u =>
                    u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.FirstName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.LastName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = query.Count();
            
            // Apply sorting based on sortColumn parameter
            query = sortColumn.ToLowerInvariant() switch
            {
                "email" => sortDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                "firstname" => sortDescending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
                "lastname" => sortDescending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
                "isactive" => sortDescending ? query.OrderByDescending(u => u.IsActive) : query.OrderBy(u => u.IsActive),
                "lastlogindatetime" => sortDescending ? query.OrderByDescending(u => u.LastLoginDateTime) : query.OrderBy(u => u.LastLoginDateTime),
                "preferredtimezone" => sortDescending ? query.OrderByDescending(u => u.PreferredTimeZone) : query.OrderBy(u => u.PreferredTimeZone),
                _ => sortDescending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName) // Default to LastName
            };
            
            var users = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

                        var userResponses = new List<UserListItemResponse>();
                        foreach (var user in users)
                        {
                            var permissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(user.Id);
                            var permissionsList = permissions.ToList();
                
                            // Count individual enabled permissions (each checkbox) instead of permission rows
                            var individualPermissionCount = permissionsList.Sum(p =>
                                (p.CanCreate ? 1 : 0) +
                                (p.CanRead ? 1 : 0) +
                                (p.CanUpdate ? 1 : 0) +
                                (p.CanDelete ? 1 : 0) +
                                (p.CanExecute ? 1 : 0));
                
                            userResponses.Add(new UserListItemResponse
                            {
                                Id = user.Id,
                                Email = user.Email,
                                FirstName = user.FirstName,
                                LastName = user.LastName,
                                IsActive = user.IsActive,
                                IsSystemAdmin = user.IsSystemAdmin,
                                LastLoginDateTime = user.LastLoginDateTime,
                                PreferredTimeZone = user.PreferredTimeZone,
                                PermissionCount = individualPermissionCount,
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

    /// <summary>
    /// Retrieves the current authenticated user's information and permissions.
    /// This endpoint is used for claims enrichment when using external identity providers.
    /// </summary>
    /// <returns>The current user's details including permissions.</returns>
    /// <response code="200">Returns the current user's details.</response>
    /// <response code="404">The user was not found in the database.</response>
    /// <response code="500">An error occurred while retrieving the user.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser()
    {
		try
		{
		// Log all claims for debugging
		if (_env.IsDevelopment() || _env.IsStaging())
			{
				var authHeader = Request.Headers["Authorization"].FirstOrDefault();
				_logger.LogInformation("Raw Authorization header: {Header}", authHeader);
				foreach (var claim in User.Claims)
				{
					_logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
				}
			}
		} catch (Exception ex) { 
			_logger.LogError(ex, "Error logging user claims");
		}


		try
        {
            // Try to get email from various claim types (including mapped WS-Fed URIs)
            var email = User.FindFirst("email")?.Value
                       ?? User.FindFirst("preferred_username")?.Value
                       ?? User.FindFirst("upn")?.Value
                       ?? User.FindFirst(ClaimTypes.Email)?.Value
                       ?? User.FindFirst(ClaimTypes.Upn)?.Value;

            // If no email claim found, try to get it from the userinfo endpoint
            if (string.IsNullOrEmpty(email))
            {
                email = await GetEmailFromUserInfoAsync();
            }

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("No email claim found for authenticated user");
                return NotFound("User email not found in claims");
            }

            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                _logger.LogWarning("User with email {Email} not found in database", email);
                return NotFound("User not found");
            }

            // Update LastLoginDateTime since IdentityServer doesn't have database access
            user.LastLoginDateTime = DateTime.UtcNow;
            user.ModifiedDateTime = DateTime.UtcNow;
            user.ModifiedBy = user.Email;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var permissions = await _unitOfWork.UserPermissions.GetByUserIdAsync(user.Id);
            var permissionsList = permissions.ToList();

            var permissionStrings = new List<string>();
            foreach (var perm in permissionsList)
            {
                if (perm.CanRead)
                    permissionStrings.Add($"{perm.PermissionName}:read");
                if (perm.CanCreate)
                    permissionStrings.Add($"{perm.PermissionName}:create");
                if (perm.CanUpdate)
                    permissionStrings.Add($"{perm.PermissionName}:update");
                if (perm.CanDelete)
                    permissionStrings.Add($"{perm.PermissionName}:delete");
                if (perm.CanExecute)
                    permissionStrings.Add($"{perm.PermissionName}:execute");
            }

            var response = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsSystemAdmin = user.IsSystemAdmin,
                Role = DetermineUserRole(user, permissionsList),
                Permissions = permissionStrings,
                ClientId = user.ClientId,
                PreferredTimeZone = user.PreferredTimeZone
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, "An error occurred while retrieving the current user");
        }
    }

    /// <summary>
    /// Retrieves a specific user by ID with their permissions. Requires Users.Manage.Read policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <returns>The user details including their permissions.</returns>
    /// <response code="200">Returns the user details.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while retrieving the user.</response>
    [HttpGet("{id}")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(UserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                LastLoginDateTime = user.LastLoginDateTime,
                ClientId = user.ClientId,
                PreferredTimeZone = user.PreferredTimeZone,
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

    /// <summary>
    /// Updates the permissions for a specific user. Requires Users.Manage.Update policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The updated permissions.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The permissions were successfully updated.</response>
    /// <response code="400">Cannot modify permissions for system administrators.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while updating permissions.</response>
    [HttpPut("{id}/permissions")]
    [Authorize(Policy = "Users.Manage.Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                var now = DateTime.UtcNow;
                var createdBy = User.Identity?.Name ?? "System";
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
                    CreatedDateTime = now,
                    CreatedBy = createdBy,
                    ModifiedDateTime = now,
                    ModifiedBy = createdBy
                };

                await _unitOfWork.UserPermissions.AddAsync(permission);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated permissions for user {UserId} by {ModifiedBy}", 
                id, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for user {UserId}", id);
            return StatusCode(500, "An error occurred while updating user permissions");
        }
    }

    /// <summary>
    /// Applies a predefined permission template to a user. Requires Users.Manage.Update policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="templateName">The template name (Viewer, Editor, or Admin).</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The template was successfully applied.</response>
    /// <response code="400">Cannot modify permissions for system administrators or unknown template.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while applying the template.</response>
    [HttpPost("{id}/templates/{templateName}")]
    [Authorize(Policy = "Users.Manage.Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                var now = DateTime.UtcNow;
                var createdBy = User.Identity?.Name ?? "System";
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
                    CreatedDateTime = now,
                    CreatedBy = createdBy,
                    ModifiedDateTime = now,
                    ModifiedBy = createdBy
                };

                await _unitOfWork.UserPermissions.AddAsync(permission);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Applied template {TemplateName} to user {UserId} by {ModifiedBy}",
                templateName, id, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying template {TemplateName} to user {UserId}", templateName, id);
            return StatusCode(500, "An error occurred while applying the permission template");
        }
    }

    /// <summary>
    /// Creates a new user with optional permissions. Requires Users.Manage.Create policy.
    /// Sends a temporary password email to the new user.
    /// </summary>
    /// <param name="request">The user creation request including email, name, and optional permissions.</param>
    /// <returns>The created user details.</returns>
    /// <response code="201">Returns the newly created user.</response>
    /// <response code="400">A user with this email already exists.</response>
    /// <response code="500">An error occurred while creating the user.</response>
    [HttpPost]
    [Authorize(Policy = "Users.Manage.Create")]
    [ProducesResponseType(typeof(UserDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDetailResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var existingUsers = await _unitOfWork.Users.GetAllAsync();
            if (existingUsers.Any(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { message = "A user with this email already exists" });
            }

            var temporaryPassword = PasswordGenerator.GeneratePassword();
            var passwordHash = _passwordHasher.HashPassword(null!, temporaryPassword);

            var userNow = DateTime.UtcNow;
            var userCreatedBy = User.Identity?.Name ?? "System";
            var user = new User
            {
                Email = request.Email,
                Username = request.Username ?? request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                ClientId = 1, // All users are internal (Cass Information Systems)
                IsActive = request.IsActive,
                IsSystemAdmin = false,
                PasswordHash = passwordHash,
                MustChangePassword = true,
                PasswordChangedDateTime = userNow,
                PreferredTimeZone = "Central Standard Time", // Default timezone for new users
                CreatedDateTime = userNow,
                CreatedBy = userCreatedBy,
                ModifiedDateTime = userNow,
                ModifiedBy = userCreatedBy,
                IsDeleted = false
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var passwordHistory = new PasswordHistory
            {
                UserId = user.Id,
                PasswordHash = passwordHash,
                ChangedDateTime = userNow,
                CreatedDateTime = userNow,
                CreatedBy = userCreatedBy,
                ModifiedDateTime = userNow,
                ModifiedBy = userCreatedBy,
                IsDeleted = false
            };
            await _unitOfWork.PasswordHistories.AddAsync(passwordHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created user {Email} with ID {UserId} by {CreatedBy}", 
                request.Email, user.Id, User.Identity?.Name ?? "System");

            try
            {
                var emailSubject = "Your Scheduler Platform Account";
                var emailBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Welcome to Scheduler Platform</h2>
                        <p>Hello {user.FirstName} {user.LastName},</p>
                        <p>Your account has been created. Please use the following credentials to log in:</p>
                        <div style='background-color: #f5f5f5; padding: 15px; margin: 20px 0; border-left: 4px solid #4CAF50;'>
                            <p><strong>Email:</strong> {user.Email}</p>
                            <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
                        </div>
                        <p><strong>Important:</strong> You will be required to change your password upon first login.</p>
                        <p>Please keep this password secure and do not share it with anyone.</p>
                        <p>Best regards,<br/>Scheduler Platform Team</p>
                    </body>
                    </html>";

                await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody, isHtml: true);
                _logger.LogInformation("Sent temporary password email to {Email}", user.Email);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send temporary password email to {Email}", user.Email);
            }

            if (request.CustomPermissions != null && request.CustomPermissions.Any())
            {
                foreach (var permissionRequest in request.CustomPermissions)
                {
                    var permNow = DateTime.UtcNow;
                    var permCreatedBy = User.Identity?.Name ?? "System";
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
                        CreatedDateTime = permNow,
                        CreatedBy = permCreatedBy,
                        ModifiedDateTime = permNow,
                        ModifiedBy = permCreatedBy,
                        IsDeleted = false
                    };

                    await _unitOfWork.UserPermissions.AddAsync(permission);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Applied custom permissions to new user {UserId}", user.Id);
            }
            else if (!string.IsNullOrEmpty(request.TemplateName))
            {
                var template = GetPermissionTemplate(request.TemplateName);
                if (template != null)
                {
                    foreach (var permissionRequest in template.Permissions)
                    {
                        var tplNow = DateTime.UtcNow;
                            var tplCreatedBy = User.Identity?.Name ?? "System";
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
                                CreatedDateTime = tplNow,
                                CreatedBy = tplCreatedBy,
                                ModifiedDateTime = tplNow,
                                ModifiedBy = tplCreatedBy,
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
                LastLoginDateTime = user.LastLoginDateTime,
                ClientId = user.ClientId,
                PreferredTimeZone = user.PreferredTimeZone,
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

    /// <summary>
    /// Updates the active status of a user. Requires Users.Manage.Update policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The status update request.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The user status was successfully updated.</response>
    /// <response code="400">Cannot modify status for system administrators.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while updating user status.</response>
    [HttpPut("{id}/status")]
    [Authorize(Policy = "Users.Manage.Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request)
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
                return BadRequest(new { message = "Cannot modify status for system administrators" });
            }

            user.IsActive = request.IsActive;
            user.ModifiedDateTime = DateTime.UtcNow;
            user.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated status for user {UserId} to {IsActive} by {ModifiedBy}", 
                id, request.IsActive, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for user {UserId}", id);
            return StatusCode(500, "An error occurred while updating user status");
        }
    }

    /// <summary>
    /// Updates user details (email, first name, last name, timezone). Requires Users.Manage.Update policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The user details update request.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The user details were successfully updated.</response>
    /// <response code="400">Email already exists for another user or cannot modify system administrators.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while updating user details.</response>
    [HttpPut("{id}/details")]
    [Authorize(Policy = "Users.Manage.Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUserDetails(int id, [FromBody] UpdateUserDetailsRequest request)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Check if email is being changed and if it already exists
            if (!string.IsNullOrWhiteSpace(request.Email) && 
                !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUsers = await _unitOfWork.Users.GetAllAsync();
                if (existingUsers.Any(u => u.Id != id && 
                    string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { message = "A user with this email already exists" });
                }
                user.Email = request.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName.Trim();
            }

            // Allow timezone to be set to null (Browser Default)
            if (request.PreferredTimeZone != null || request.ClearTimezone)
            {
                user.PreferredTimeZone = request.PreferredTimeZone;
            }

            user.ModifiedDateTime = DateTime.UtcNow;
            user.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated details for user {UserId} by {ModifiedBy}", 
                id, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating details for user {UserId}", id);
            return StatusCode(500, "An error occurred while updating user details");
        }
    }

    /// <summary>
    /// Updates the preferred timezone for a user. Requires Users.Manage.Update policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The timezone update request containing the new timezone ID.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The user timezone was successfully updated.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while updating user timezone.</response>
    [HttpPut("{id}/timezone")]
    [Authorize(Policy = "Users.Manage.Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUserTimezone(int id, [FromBody] UpdateUserTimezoneRequest request)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.PreferredTimeZone = request.PreferredTimeZone;
            user.ModifiedDateTime = DateTime.UtcNow;
            user.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated timezone for user {UserId} to {TimeZone} by {ModifiedBy}", 
                id, request.PreferredTimeZone, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating timezone for user {UserId}", id);
            return StatusCode(500, "An error occurred while updating user timezone");
        }
    }

    /// <summary>
    /// Resets a user's password and sends them a new temporary password via email. Requires Users.Manage.Update policy.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <returns>A success message indicating the password was reset.</returns>
    /// <response code="200">The password was successfully reset and emailed to the user.</response>
    /// <response code="400">Cannot reset password for external authentication users.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while resetting the password.</response>
    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = "Users.Manage.Update")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword(int id)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                return BadRequest(new { message = "Cannot reset password for external authentication users" });
            }

            var newPassword = PasswordGenerator.GeneratePassword();
            var passwordHash = _passwordHasher.HashPassword(user, newPassword);

            user.PasswordHash = passwordHash;
            user.MustChangePassword = true;
            user.PasswordChangedDateTime = DateTime.UtcNow;
            user.ModifiedDateTime = DateTime.UtcNow;
            user.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Users.UpdateAsync(user);

            var passwordHistory = new PasswordHistory
            {
                UserId = user.Id,
                PasswordHash = passwordHash,
                ChangedDateTime = DateTime.UtcNow,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",
                IsDeleted = false
            };
            await _unitOfWork.PasswordHistories.AddAsync(passwordHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Reset password for user {UserId} by {ModifiedBy}", 
                id, User.Identity?.Name ?? "System");

            try
            {
                var emailSubject = "Your Password Has Been Reset";
                var emailBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Password Reset</h2>
                        <p>Hello {user.FirstName} {user.LastName},</p>
                        <p>Your password has been reset by an administrator. Please use the following temporary password to log in:</p>
                        <div style='background-color: #f5f5f5; padding: 15px; margin: 20px 0; border-left: 4px solid #FF9800;'>
                            <p><strong>Email:</strong> {user.Email}</p>
                            <p><strong>Temporary Password:</strong> {newPassword}</p>
                        </div>
                        <p><strong>Important:</strong> You will be required to change your password upon next login.</p>
                        <p>Please keep this password secure and do not share it with anyone.</p>
                        <p>Best regards,<br/>Scheduler Platform Team</p>
                    </body>
                    </html>";

                await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody, isHtml: true);
                _logger.LogInformation("Sent password reset email to {Email}", user.Email);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send password reset email to {Email}", user.Email);
            }

            return Ok(new { message = "Password reset successfully. New password has been sent to user's email." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", id);
            return StatusCode(500, "An error occurred while resetting the password");
        }
    }

    /// <summary>
    /// Updates the Super Admin status of a user. Requires Super Admin privileges.
    /// Cannot remove Super Admin status from the last Super Admin in the system.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The Super Admin status update request.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The Super Admin status was successfully updated.</response>
    /// <response code="400">Cannot remove the last Super Admin or invalid request.</response>
    /// <response code="403">Only Super Admins can modify Super Admin status.</response>
    /// <response code="404">The user was not found.</response>
    /// <response code="500">An error occurred while updating Super Admin status.</response>
    [HttpPut("{id}/super-admin")]
    [Authorize(Policy = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSuperAdminStatus(int id, [FromBody] UpdateSuperAdminStatusRequest request)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // If trying to remove Super Admin status, check if this is the last Super Admin
            if (!request.IsSuperAdmin && user.IsSystemAdmin)
            {
                var allUsers = await _unitOfWork.Users.GetAllAsync();
                var superAdminCount = allUsers.Count(u => u.IsSystemAdmin && u.IsActive);
                
                if (superAdminCount <= 1)
                {
                    return BadRequest(new { message = "Cannot remove Super Admin status from the last Super Admin. Promote another user to Super Admin first." });
                }
            }

            var oldStatus = user.IsSystemAdmin;
            user.IsSystemAdmin = request.IsSuperAdmin;
            user.ModifiedDateTime = DateTime.UtcNow;
            user.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning("Super Admin status changed for user {UserId} ({Email}) from {OldStatus} to {NewStatus} by {ModifiedBy}",
                id, user.Email, oldStatus, request.IsSuperAdmin, User.Identity?.Name ?? "System");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Super Admin status for user {UserId}", id);
            return StatusCode(500, "An error occurred while updating Super Admin status");
        }
    }

    /// <summary>
    /// Retrieves all available permission templates. Requires Users.Manage.Read policy.
    /// </summary>
    /// <returns>A list of permission templates (Viewer, Editor, Admin).</returns>
    /// <response code="200">Returns the list of permission templates.</response>
    /// <response code="500">An error occurred while retrieving permission templates.</response>
    [HttpGet("templates")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(List<PermissionTemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                    new() { PermissionName = "jobs", CanRead = true },
                    new() { PermissionName = "adr", CanRead = true }
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
                    new() { PermissionName = "jobs", CanRead = true },
                    new() { PermissionName = "adr", CanRead = true, CanUpdate = true }
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
                    new() { PermissionName = "users:manage", CanRead = true, CanUpdate = true },
                    new() { PermissionName = "adr", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true, CanExecute = true }
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

    /// <summary>
    /// Attempts to retrieve the user's email from the IdentityServer userinfo endpoint.
    /// This is used as a fallback when the access token doesn't contain an email claim.
    /// </summary>
    private async Task<string?> GetEmailFromUserInfoAsync()
    {
        try
        {
            var authority = _configuration["Authentication:Authority"];
            if (string.IsNullOrEmpty(authority))
            {
                _logger.LogWarning("Authentication:Authority not configured. Cannot call userinfo endpoint.");
                return null;
            }

            // Extract the bearer token from the Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("No Bearer token in Authorization header for userinfo call");
                return null;
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            // Build userinfo endpoint URL
            var userInfoUrl = authority.TrimEnd('/') + "/connect/userinfo";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogDebug("Calling userinfo endpoint: {Url}", userInfoUrl);
            var response = await httpClient.GetAsync(userInfoUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Userinfo endpoint returned {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Userinfo response: {Content}", content);
            
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Try to get email from userinfo response
            if (root.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
            {
                var email = emailProp.GetString();
                _logger.LogInformation("Retrieved email from userinfo endpoint: {Email}", email);
                return email;
            }

            // Try preferred_username as fallback
            if (root.TryGetProperty("preferred_username", out var usernameProp) && usernameProp.ValueKind == JsonValueKind.String)
            {
                var username = usernameProp.GetString();
                // Only use if it looks like an email
                if (username?.Contains("@") == true)
                {
                    _logger.LogInformation("Retrieved email from userinfo preferred_username: {Email}", username);
                    return username;
                }
            }

            // Try name as last resort (some IdPs put email in name)
            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (name?.Contains("@") == true)
                {
                    _logger.LogInformation("Retrieved email from userinfo name: {Email}", name);
                    return name;
                }
            }

            _logger.LogWarning("Userinfo endpoint did not return an email claim. Response: {Content}", content);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling userinfo endpoint");
            return null;
        }
    }
}
