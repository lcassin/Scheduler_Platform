using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Exception thrown when the user's session has expired and they need to re-authenticate.
/// UI components should catch this exception and redirect to login.
/// </summary>
public class SessionExpiredException : Exception
{
    public SessionExpiredException() : base("Your session has expired. Please log in again.") { }
    public SessionExpiredException(string message) : base(message) { }
}

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthTokenHandler> _logger;
    private readonly SessionStateService _sessionStateService;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public AuthTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthTokenHandler> logger,
        SessionStateService sessionStateService,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _sessionStateService = sessionStateService;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Extracts a user identifier from the claims principal for use as a cache key.
    /// Tries multiple claim types to find a stable identifier.
    /// </summary>
    private static string? GetUserKey(System.Security.Claims.ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;
            
        // Try various claim types that could identify the user
        var email = user.FindFirst("email")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst("upn")?.Value
            ?? user.FindFirst("unique_name")?.Value;
            
        if (!string.IsNullOrEmpty(email))
            return email.ToLowerInvariant();
            
        // Fall back to sub or nameidentifier
        var sub = user.FindFirst("sub")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
        return sub;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        string? accessToken = null;
        string? userKey = null;
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            // Extract user key for token caching
            userKey = GetUserKey(httpContext.User);
            
            try
            {
                accessToken = await httpContext.GetTokenAsync("access_token");
                
                // Update the global token store when we have HttpContext available
                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(userKey))
                {
                    // Try to get expiry for cache
                    var expiresAtStr = await httpContext.GetTokenAsync("expires_at");
                    DateTimeOffset? expiresAt = null;
                    if (!string.IsNullOrEmpty(expiresAtStr) && 
                        DateTimeOffset.TryParse(expiresAtStr, System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                    {
                        expiresAt = parsed;
                    }
                    
                    // Store in global token store keyed by user
                    GlobalTokenStore.SetToken(userKey, accessToken, expiresAt);
                    _logger.LogDebug("Cached token for user {UserKey}", userKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get access token from HttpContext");
            }
        }
        else
        {
            // HttpContext is null (common in Blazor Server SignalR circuits)
            // Try to get the cached token using the AsyncLocal user key
            accessToken = GlobalTokenStore.GetToken();
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogDebug("Using cached token from GlobalTokenStore (HttpContext unavailable, UserKey: {UserKey})", 
                    GlobalTokenStore.CurrentUserKey ?? "unknown");
            }
            else
            {
                _logger.LogWarning(
                    "No access token available. HttpContext: {HasHttpContext}, CurrentUserKey: {UserKey}, HasCachedToken: {HasCachedToken}",
                    httpContext != null, GlobalTokenStore.CurrentUserKey ?? "null", GlobalTokenStore.HasValidToken());
            }
        }
        
        // Attach the Bearer token if we have one
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            // Log token metadata for debugging (never log the actual token)
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(accessToken))
                {
                    var jwt = handler.ReadJwtToken(accessToken);
                    var exp = jwt.ValidTo;
                    var aud = string.Join(",", jwt.Audiences);
                    var iss = jwt.Issuer;
                    var clientId = jwt.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value ?? "unknown";
                    var isExpired = exp < DateTime.UtcNow;
                    
                    _logger.LogDebug(
                        "Token attached. Issuer: {Issuer}, Audience: {Audience}, ClientId: {ClientId}, Expires: {Expires}, IsExpired: {IsExpired}",
                        iss, aud, clientId, exp, isExpired);
                    
                    if (isExpired)
                    {
                        _logger.LogWarning(
                            "Access token is EXPIRED. Expires: {Expires}, Now: {Now}",
                            exp, DateTime.UtcNow);
                    }
                }
            }
            catch (Exception tokenEx)
            {
                _logger.LogDebug(tokenEx, "Could not parse JWT token for logging");
            }
        }

        // Add internal API key as fallback authentication for long-running operations
        // The API accepts both JWT and API key authentication, so if the JWT token expires
        // during a long-running operation, the API key will still authenticate the request
        var internalApiKey = _configuration["Scheduler:InternalApiKey"];
        if (!string.IsNullOrEmpty(internalApiKey))
        {
            request.Headers.Add("X-Scheduler-Api-Key", internalApiKey);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Check for 401 Unauthorized - session has expired
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Log the WWW-Authenticate header to understand why the token was rejected
            var wwwAuthenticate = response.Headers.WwwAuthenticate.ToString();
            _logger.LogWarning(
                "Received 401 Unauthorized from API. Request: {Method} {Uri}, WWW-Authenticate: {WwwAuthenticate}",
                request.Method, request.RequestUri, wwwAuthenticate);
            
            // Notify all subscribers (like MainLayout) that the session has expired
            // This allows centralized handling of session expiration with automatic redirect
            _sessionStateService.NotifySessionExpired();
            
            // Return the response without throwing - let the redirect happen gracefully
            // instead of causing exceptions to propagate through the component tree
        }

        return response;
    }
}
