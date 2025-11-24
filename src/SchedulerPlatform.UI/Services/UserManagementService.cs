using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class UserManagementService : IUserManagementService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(IHttpClientFactory httpClientFactory, ILogger<UserManagementService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("SchedulerAPI");

    public async Task<PagedResult<UserListItem>> GetUsersAsync(string? searchTerm, int pageNumber, int pageSize, bool showInactive = false)
    {
        try
        {
            var client = CreateClient();
            var query = $"Users?pageNumber={pageNumber}&pageSize={pageSize}&showInactive={showInactive}";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
            }

            var response = await client.GetFromJsonAsync<PagedResult<UserListItem>>(query);
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
            var client = CreateClient();
            return await client.GetFromJsonAsync<UserDetail>($"Users/{id}");
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
            var client = CreateClient();
            var request = new { Permissions = permissions };
            var response = await client.PutAsJsonAsync($"Users/{id}/permissions", request);
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
            var client = CreateClient();
            var response = await client.PostAsync($"Users/{id}/templates/{templateName}", null);
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
            var client = CreateClient();
            var templates = await client.GetFromJsonAsync<List<PermissionTemplate>>("Users/templates");
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
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("Users", request);
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
            var client = CreateClient();
            var request = new UpdateUserStatusRequest { IsActive = isActive };
            var response = await client.PutAsJsonAsync($"Users/{id}/status", request);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Updated user {UserId} status to {IsActive}", id, isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for user {UserId}", id);
            throw;
        }
    }
}
