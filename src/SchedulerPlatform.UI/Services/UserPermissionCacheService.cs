namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service to cache user permissions fetched from the API.
/// Permissions are refreshed on keepalive to ensure they stay current.
/// </summary>
public class UserPermissionCacheService
{
    private readonly AuthenticatedHttpClientService _httpClient;
    private readonly ILogger<UserPermissionCacheService> _logger;
    
    private CachedUserPermissions? _cachedPermissions;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    
    /// <summary>
    /// Event raised when permissions have been refreshed.
    /// Components can subscribe to update their UI when permissions change.
    /// </summary>
    public event Action? OnPermissionsRefreshed;

    public UserPermissionCacheService(
        AuthenticatedHttpClientService httpClient,
        ILogger<UserPermissionCacheService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the cached user permissions, or null if not yet loaded.
    /// </summary>
    public CachedUserPermissions? CachedPermissions => _cachedPermissions;

    /// <summary>
    /// Refreshes the permission cache by calling the /api/users/me endpoint.
    /// This should be called on initial login and on each keepalive.
    /// </summary>
    /// <returns>True if refresh was successful, false otherwise.</returns>
    public async Task<bool> RefreshPermissionsAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var response = await _httpClient.GetAsync("users/me");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to refresh permissions: {StatusCode}", response.StatusCode);
                return false;
            }
            
            var userInfo = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
            if (userInfo == null)
            {
                _logger.LogWarning("Failed to deserialize user info response");
                return false;
            }
            
            var previousPermissions = _cachedPermissions;
            _cachedPermissions = new CachedUserPermissions
            {
                UserId = userInfo.Id,
                Email = userInfo.Email,
                FirstName = userInfo.FirstName,
                LastName = userInfo.LastName,
                IsSystemAdmin = userInfo.IsSystemAdmin,
                Role = userInfo.Role,
                Permissions = userInfo.Permissions ?? new List<string>(),
                ClientId = userInfo.ClientId,
                PreferredTimeZone = userInfo.PreferredTimeZone,
                LastRefreshed = DateTime.UtcNow
            };
            
            // Notify subscribers if permissions changed
            if (previousPermissions == null || PermissionsChanged(previousPermissions, _cachedPermissions))
            {
                _logger.LogInformation("User permissions refreshed for {Email}, IsSystemAdmin: {IsSystemAdmin}, Role: {Role}", 
                    userInfo.Email, userInfo.IsSystemAdmin, userInfo.Role);
                OnPermissionsRefreshed?.Invoke();
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing user permissions");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Clears the permission cache. Should be called on logout.
    /// </summary>
    public void ClearCache()
    {
        _cachedPermissions = null;
    }

    /// <summary>
    /// Checks if the user has a specific permission.
    /// </summary>
    public bool HasPermission(string permission)
    {
        if (_cachedPermissions == null)
            return false;
        
        // System admins have all permissions
        if (_cachedPermissions.IsSystemAdmin)
            return true;
        
        return _cachedPermissions.Permissions.Contains(permission);
    }

    /// <summary>
    /// Checks if the user is a system admin (Super Admin).
    /// </summary>
    public bool IsSystemAdmin()
    {
        return _cachedPermissions?.IsSystemAdmin ?? false;
    }

    /// <summary>
    /// Checks if the user is an admin or above (Admin or Super Admin).
    /// </summary>
    public bool IsAdminOrAbove()
    {
        if (_cachedPermissions == null)
            return false;
        
        return _cachedPermissions.IsSystemAdmin || _cachedPermissions.Role == "Admin";
    }

    /// <summary>
    /// Gets the user's display name from cached permissions.
    /// </summary>
    public string GetDisplayName()
    {
        if (_cachedPermissions == null)
            return "User";
        
        if (!string.IsNullOrWhiteSpace(_cachedPermissions.FirstName) && !string.IsNullOrWhiteSpace(_cachedPermissions.LastName))
            return $"{_cachedPermissions.FirstName} {_cachedPermissions.LastName}";
        if (!string.IsNullOrWhiteSpace(_cachedPermissions.FirstName))
            return _cachedPermissions.FirstName;
        if (!string.IsNullOrWhiteSpace(_cachedPermissions.LastName))
            return _cachedPermissions.LastName;
        if (!string.IsNullOrWhiteSpace(_cachedPermissions.Email))
            return _cachedPermissions.Email.Split('@')[0];
        
        return "User";
    }

    /// <summary>
    /// Gets the user's email from cached permissions.
    /// </summary>
    public string GetEmail()
    {
        return _cachedPermissions?.Email ?? "";
    }

    /// <summary>
    /// Gets the user's role from cached permissions.
    /// </summary>
    public string GetRole()
    {
        if (_cachedPermissions == null)
            return "User";
        
        if (_cachedPermissions.IsSystemAdmin)
            return "Super Admin";
        
        return _cachedPermissions.Role ?? "User";
    }

    /// <summary>
    /// Gets the user's preferred timezone from cached permissions.
    /// Returns null if not set or not yet loaded.
    /// </summary>
    public string? GetPreferredTimeZone()
    {
        return _cachedPermissions?.PreferredTimeZone;
    }

    private bool PermissionsChanged(CachedUserPermissions previous, CachedUserPermissions current)
    {
        if (previous.IsSystemAdmin != current.IsSystemAdmin)
            return true;
        if (previous.Role != current.Role)
            return true;
        if (previous.Permissions.Count != current.Permissions.Count)
            return true;
        
        return !previous.Permissions.OrderBy(p => p).SequenceEqual(current.Permissions.OrderBy(p => p));
    }
}

/// <summary>
/// Cached user permissions fetched from the API.
/// </summary>
public class CachedUserPermissions
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsSystemAdmin { get; set; }
    public string? Role { get; set; }
    public List<string> Permissions { get; set; } = new();
    public int ClientId { get; set; }
    public string? PreferredTimeZone { get; set; }
    public DateTime LastRefreshed { get; set; }
}

/// <summary>
/// Response model matching the API's CurrentUserResponse.
/// </summary>
internal class CurrentUserResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsSystemAdmin { get; set; }
    public string? Role { get; set; }
    public List<string>? Permissions { get; set; }
    public int ClientId { get; set; }
    public string? PreferredTimeZone { get; set; }
}
