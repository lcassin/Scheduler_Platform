using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class UserManagementService : IUserManagementService
{
    private readonly AuthenticatedHttpClientService _httpClient;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(AuthenticatedHttpClientService httpClient, ILogger<UserManagementService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItem>> GetUsersAsync(string? searchTerm, int pageNumber, int pageSize, bool showInactive = false)
    {
        try
        {
            var query = $"Users?pageNumber={pageNumber}&pageSize={pageSize}&showInactive={showInactive}";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
            }

            var response = await _httpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListItem>>();
            if (result == null)
            {
                return new PagedResult<UserListItem>();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            throw;
        }
    }

    public async Task<UserDetail?> GetUserAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"Users/{id}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserDetail>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId}", id);
            throw;
        }
    }

    public async Task UpdateUserPermissionsAsync(int id, List<UserPermissionDto> permissions)
    {
        try
        {
            var request = new { Permissions = permissions };
            var response = await _httpClient.PutAsJsonAsync($"Users/{id}/permissions", request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for user {UserId}", id);
            throw;
        }
    }

    public async Task ApplyPermissionTemplateAsync(int id, string templateName)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"Users/{id}/templates/{templateName}", new { });
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying template {TemplateName} to user {UserId}", templateName, id);
            throw;
        }
    }

    public async Task<List<PermissionTemplate>> GetPermissionTemplatesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("Users/templates");
            response.EnsureSuccessStatusCode();
            var templates = await response.Content.ReadFromJsonAsync<List<PermissionTemplate>>();
            return templates ?? new List<PermissionTemplate>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching permission templates");
            throw;
        }
    }

    public async Task<UserDetail> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Users", request);
            response.EnsureSuccessStatusCode();
            
            var createdUser = await response.Content.ReadFromJsonAsync<UserDetail>();
            if (createdUser == null)
            {
                throw new Exception("Failed to parse created user response");
            }
            
            _logger.LogInformation("Created user {Email}", request.Email);
            return createdUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email}", request.Email);
            throw;
        }
    }

    public async Task UpdateUserStatusAsync(int id, bool isActive)
    {
        try
        {
            var request = new UpdateUserStatusRequest { IsActive = isActive };
            var response = await _httpClient.PutAsJsonAsync($"Users/{id}/status", request);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Updated user {UserId} status to {IsActive}", id, isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for user {UserId}", id);
            throw;
        }
    }

    public async Task UpdateSuperAdminStatusAsync(int id, bool isSuperAdmin)
    {
        try
        {
            var request = new { IsSuperAdmin = isSuperAdmin };
            var response = await _httpClient.PutAsJsonAsync($"Users/{id}/super-admin", request);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Updated user {UserId} Super Admin status to {IsSuperAdmin}", id, isSuperAdmin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Super Admin status for user {UserId}", id);
            throw;
        }
    }

    public async Task UpdateUserTimezoneAsync(int id, string? preferredTimeZone)
    {
        try
        {
            var request = new { PreferredTimeZone = preferredTimeZone };
            var response = await _httpClient.PutAsJsonAsync($"Users/{id}/timezone", request);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Updated user {UserId} timezone to {TimeZone}", id, preferredTimeZone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating timezone for user {UserId}", id);
            throw;
        }
    }
}
