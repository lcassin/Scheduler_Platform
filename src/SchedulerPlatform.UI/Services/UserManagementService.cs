using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class UserManagementService : IUserManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(HttpClient httpClient, ILogger<UserManagementService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItem>> GetUsersAsync(string? searchTerm, int pageNumber, int pageSize)
    {
        try
        {
            var query = $"api/Users?pageNumber={pageNumber}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
            }

            var response = await _httpClient.GetFromJsonAsync<PagedResult<UserListItem>>(query);
            if (response == null)
            {
                return new PagedResult<UserListItem>();
            }

            return response;
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
            return await _httpClient.GetFromJsonAsync<UserDetail>($"api/Users/{id}");
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
            var response = await _httpClient.PutAsJsonAsync($"api/Users/{id}/permissions", request);
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
            var response = await _httpClient.PostAsync($"api/Users/{id}/templates/{templateName}", null);
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
            var templates = await _httpClient.GetFromJsonAsync<List<PermissionTemplate>>("api/Users/templates");
            return templates ?? new List<PermissionTemplate>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching permission templates");
            throw;
        }
    }
}
