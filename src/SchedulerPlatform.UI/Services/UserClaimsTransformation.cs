using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Transforms claims by enriching the principal with user permissions from the API.
/// This is necessary when using an external identity provider (like corporate Duende) that
/// doesn't have access to the application's user table.
/// </summary>
public class UserClaimsTransformation : IClaimsTransformation
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserClaimsTransformation> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserClaimsTransformation(
        IHttpClientFactory httpClientFactory,
        ILogger<UserClaimsTransformation> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var existingIsSystemAdmin = principal.Claims.FirstOrDefault(c => c.Type == "is_system_admin");
        if (existingIsSystemAdmin != null)
        {
            return principal;
        }

        var existingPermissions = principal.Claims.Where(c => c.Type == "permission").ToList();
        if (existingPermissions.Any())
        {
            return principal;
        }

        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return principal;
            }

            var accessToken = await httpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogDebug("No access token available for claims transformation");
                return principal;
            }

            var client = _httpClientFactory.CreateClient("SchedulerAPI");
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("users/me");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user info from API: {StatusCode}", response.StatusCode);
                return principal;
            }

            var userInfo = await response.Content.ReadFromJsonAsync<UserMeResponse>();
            if (userInfo == null)
            {
                return principal;
            }

            var identity = principal.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return principal;
            }

            if (userInfo.IsSystemAdmin)
            {
                identity.AddClaim(new Claim("is_system_admin", "true"));
            }

            if (!string.IsNullOrEmpty(userInfo.Role))
            {
                identity.AddClaim(new Claim("role", userInfo.Role));
            }

            if (userInfo.Permissions != null)
            {
                foreach (var permission in userInfo.Permissions)
                {
                    identity.AddClaim(new Claim("permission", permission));
                }
            }

            _logger.LogDebug("Enriched UI claims: IsSystemAdmin={IsSystemAdmin}, Role={Role}, Permissions={PermissionCount}",
                userInfo.IsSystemAdmin, userInfo.Role, userInfo.Permissions?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching UI claims");
        }

        return principal;
    }

    private class UserMeResponse
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsSystemAdmin { get; set; }
        public string? Role { get; set; }
        public List<string>? Permissions { get; set; }
    }
}
