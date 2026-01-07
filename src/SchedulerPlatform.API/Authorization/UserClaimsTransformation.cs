using Microsoft.AspNetCore.Authentication;
using SchedulerPlatform.Core.Domain.Interfaces;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace SchedulerPlatform.API.Authorization;

/// <summary>
/// Transforms claims by enriching the principal with user permissions from the database.
/// This is necessary when using an external identity provider (like corporate Duende) that
/// doesn't have access to the application's user table.
/// </summary>
public class UserClaimsTransformation : IClaimsTransformation
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserClaimsTransformation> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public UserClaimsTransformation(
        IServiceProvider serviceProvider,
        ILogger<UserClaimsTransformation> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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

        // Try to get email from various claim types (including mapped WS-Fed URIs)
        var email = principal.FindFirst("email")?.Value
                   ?? principal.FindFirst("preferred_username")?.Value
                   ?? principal.FindFirst("upn")?.Value
                   ?? principal.FindFirst(ClaimTypes.Email)?.Value
                   ?? principal.FindFirst(ClaimTypes.Upn)?.Value;

        // If no email claim found, try to get it from the userinfo endpoint
        if (string.IsNullOrEmpty(email))
        {
            email = await GetEmailFromUserInfoAsync(principal);
        }

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("No email claim found for authenticated user. Cannot enrich claims.");
            return principal;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var users = await unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                _logger.LogDebug("User with email {Email} not found in database. Using default claims.", email);
                return principal;
            }

            var identity = principal.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return principal;
            }

            if (user.IsSystemAdmin)
            {
                identity.AddClaim(new Claim("is_system_admin", "true"));
                _logger.LogDebug("Added is_system_admin claim for user {Email}", email);
            }

            var permissions = await unitOfWork.UserPermissions.GetAllAsync();
            var userPermissions = permissions.Where(p => p.UserId == user.Id && !p.IsDeleted).ToList();

            foreach (var perm in userPermissions)
            {
                if (perm.CanRead)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:read"));
                if (perm.CanCreate)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:create"));
                if (perm.CanUpdate)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:update"));
                if (perm.CanDelete)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:delete"));
                if (perm.CanExecute)
                    identity.AddClaim(new Claim("permission", $"{perm.PermissionName}:execute"));
            }

            var role = user.IsSystemAdmin ? "Super Admin" : 
                       userPermissions.Any(p => p.CanCreate || p.CanUpdate || p.CanDelete) ? "Editor" : "Viewer";
            identity.AddClaim(new Claim("role", role));
            identity.AddClaim(new Claim("user_client_id", user.ClientId.ToString()));

            _logger.LogDebug("Enriched claims for user {Email}: IsSystemAdmin={IsSystemAdmin}, Role={Role}, Permissions={PermissionCount}",
                email, user.IsSystemAdmin, role, userPermissions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching claims for user {Email}", email);
        }

        return principal;
    }

    /// <summary>
    /// Attempts to retrieve the user's email from the IdentityServer userinfo endpoint.
    /// This is used as a fallback when the access token doesn't contain an email claim.
    /// </summary>
    private async Task<string?> GetEmailFromUserInfoAsync(ClaimsPrincipal principal)
    {
        try
        {
            var authority = _configuration["Authentication:Authority"];
            if (string.IsNullOrEmpty(authority))
            {
                _logger.LogWarning("Authentication:Authority not configured. Cannot call userinfo endpoint.");
                return null;
            }

            // Get the access token from the current request context
            using var scope = _serviceProvider.CreateScope();
            var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
            var httpContext = httpContextAccessor?.HttpContext;
            
            if (httpContext == null)
            {
                _logger.LogDebug("No HttpContext available for userinfo call");
                return null;
            }

            // Extract the bearer token from the Authorization header
            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
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
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Try to get email from userinfo response
            if (root.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
            {
                var email = emailProp.GetString();
                _logger.LogDebug("Retrieved email from userinfo endpoint: {Email}", email);
                return email;
            }

            // Try preferred_username as fallback
            if (root.TryGetProperty("preferred_username", out var usernameProp) && usernameProp.ValueKind == JsonValueKind.String)
            {
                var username = usernameProp.GetString();
                // Only use if it looks like an email
                if (username?.Contains("@") == true)
                {
                    _logger.LogDebug("Retrieved email from userinfo preferred_username: {Email}", username);
                    return username;
                }
            }

            // Try name as last resort (some IdPs put email in name)
            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (name?.Contains("@") == true)
                {
                    _logger.LogDebug("Retrieved email from userinfo name: {Email}", name);
                    return name;
                }
            }

            _logger.LogWarning("Userinfo endpoint did not return an email claim");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling userinfo endpoint");
            return null;
        }
    }
}
