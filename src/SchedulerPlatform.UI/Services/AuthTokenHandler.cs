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
    /// Gets the AccessTokenCacheService from the current request's scope.
    /// IHttpClientFactory pools handlers with a 2-minute lifetime, so we can't hold a direct
    /// reference to scoped services - they may be from a stale scope. Instead, we resolve
    /// from the current HttpContext.RequestServices when available, or fall back to the
    /// root service provider (which will create a new scope).
    /// </summary>
    private AccessTokenCacheService GetTokenCache()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.RequestServices != null)
        {
            return httpContext.RequestServices.GetRequiredService<AccessTokenCacheService>();
        }
        
        // Fallback to root service provider - this creates a new instance but at least
        // allows the request to proceed. This path is taken when HttpContext is null
        // (e.g., during Blazor Server SignalR circuit events).
        return _serviceProvider.GetRequiredService<AccessTokenCacheService>();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        string? accessToken = null;
        
        // Get the token cache from the current request's scope (not the handler's stale scope)
        var tokenCache = GetTokenCache();
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            try
            {
                accessToken = await httpContext.GetTokenAsync("access_token");
                
                // Update the token cache when we have HttpContext available
                if (!string.IsNullOrEmpty(accessToken))
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
                    tokenCache.UpdateToken(accessToken, expiresAt);
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
            // Try to use the cached token instead
            accessToken = tokenCache.GetCachedToken();
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogDebug("Using cached access token (HttpContext unavailable)");
            }
            else
            {
                _logger.LogWarning(
                    "No access token available. HttpContext: {HasHttpContext}, CachedToken: {HasCachedToken}",
                    httpContext != null, tokenCache.HasValidToken);
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
