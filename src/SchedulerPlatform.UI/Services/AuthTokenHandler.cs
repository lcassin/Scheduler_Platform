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

/// <summary>
/// Key used to pass the user identifier through HttpRequestMessage.Options.
/// This allows circuit-scoped services to pass the user identity to the pooled handler.
/// </summary>
public static class AuthTokenHandlerOptions
{
    /// <summary>
    /// The key used to store the user identifier in HttpRequestMessage.Options.
    /// </summary>
    public static readonly HttpRequestOptionsKey<string> UserKeyOption = new("UserKey");
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
    public static string? GetUserKey(System.Security.Claims.ClaimsPrincipal? user)
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
        
        // First, check if the caller passed a userKey via request options
        // This is the preferred method for Blazor Server SignalR circuits where HttpContext is null
        if (request.Options.TryGetValue(AuthTokenHandlerOptions.UserKeyOption, out var requestUserKey) && 
            !string.IsNullOrEmpty(requestUserKey))
        {
            userKey = requestUserKey;
            accessToken = GlobalTokenStore.GetToken(userKey);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogDebug("Using token from GlobalTokenStore via request options (UserKey: {UserKey})", userKey);
            }
            else
            {
                // Token not in GlobalTokenStore - try to get from HttpContext as fallback
                // This can happen on the first request after login before the token is cached
                _logger.LogDebug("UserKey {UserKey} provided but no token in GlobalTokenStore, trying HttpContext fallback", userKey);
                
                if (httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    try
                    {
                        accessToken = await httpContext.GetTokenAsync("access_token");
                        
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            // Store in GlobalTokenStore for future requests
                            var expiresAtStr = await httpContext.GetTokenAsync("expires_at");
                            DateTimeOffset? expiresAt = null;
                            if (!string.IsNullOrEmpty(expiresAtStr) && 
                                DateTimeOffset.TryParse(expiresAtStr, System.Globalization.CultureInfo.InvariantCulture, 
                                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                            {
                                expiresAt = parsed;
                            }
                            
                            GlobalTokenStore.SetToken(userKey, accessToken, expiresAt);
                            _logger.LogInformation("Bootstrapped token for user {UserKey} from HttpContext into GlobalTokenStore", userKey);
                        }
                        else
                        {
                            _logger.LogWarning("UserKey {UserKey} provided but no token in GlobalTokenStore and HttpContext.GetTokenAsync returned null", userKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get access token from HttpContext for userKey {UserKey}", userKey);
                    }
                }
                else
                {
                    _logger.LogWarning("UserKey {UserKey} provided but no token in GlobalTokenStore and HttpContext is not authenticated", userKey);
                }
            }
        }
        else if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            // HttpContext is available (initial page load, keepalive requests)
            // Extract user key and cache the token for later use
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
                    _logger.LogDebug("Cached token for user {UserKey} from HttpContext", userKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get access token from HttpContext");
            }
        }
        else
        {
            // No userKey from request options and no HttpContext
            // This is a fallback - try AsyncLocal (may work in some cases)
            accessToken = GlobalTokenStore.GetToken();
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogDebug("Using cached token from GlobalTokenStore via AsyncLocal (UserKey: {UserKey})", 
                    GlobalTokenStore.CurrentUserKey ?? "unknown");
            }
            else
            {
                _logger.LogWarning(
                    "No access token available. HttpContext: {HasHttpContext}, RequestOptionsUserKey: {RequestUserKey}, AsyncLocalUserKey: {AsyncLocalKey}",
                    httpContext != null, requestUserKey ?? "null", GlobalTokenStore.CurrentUserKey ?? "null");
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
                            "Access token is EXPIRED. Expires: {Expires}, Now: {Now}. Removing expired token — request will fall through to API key auth.",
                            exp, DateTime.UtcNow);
                        
                        // Clear the stale expired token from the store so subsequent requests
                        // don't keep re-detecting the same expired token in a loop
                        if (!string.IsNullOrEmpty(userKey))
                        {
                            GlobalTokenStore.RemoveToken(userKey);
                        }
                        
                        // Remove the expired Bearer token from this request so the API
                        // doesn't reject it outright. The X-Scheduler-Api-Key header
                        // (added below) will serve as fallback authentication for
                        // background/scheduled operations that have no active user session.
                        request.Headers.Authorization = null;
                        
                        // Notify session expired so the UI can redirect for interactive users
                        _sessionStateService.NotifySessionExpired();
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
            var wwwAuthenticate = response.Headers.WwwAuthenticate.ToString();
            _logger.LogWarning(
                "Received 401 Unauthorized from API. Request: {Method} {Uri}, WWW-Authenticate: {WwwAuthenticate}",
                request.Method, request.RequestUri, wwwAuthenticate);
            
            // Clear the stale token from the store so subsequent requests don't keep using it
            if (!string.IsNullOrEmpty(userKey))
            {
                GlobalTokenStore.RemoveToken(userKey);
            }
            
            // Notify all subscribers (like MainLayout) that the session has expired
            // This allows centralized handling of session expiration with automatic redirect
            _sessionStateService.NotifySessionExpired();
            
            // Return the response without throwing - let the redirect happen gracefully
            // instead of causing exceptions to propagate through the component tree
        }
        
        // Check for 403 Forbidden — but only treat it as session expiration if the token
        // is near expiry or missing. A 403 with a valid, non-expired token is a legitimate
        // permission denial (e.g., non-admin hitting admin endpoint) and should NOT trigger logout.
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            // Use accessToken (the original value) rather than request.Headers.Authorization
            // because Authorization may have been cleared at line 232 for expired tokens.
            bool tokenIsExpiredOrMissing = string.IsNullOrEmpty(accessToken);
            
            if (!tokenIsExpiredOrMissing)
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(accessToken))
                    {
                        var jwt = handler.ReadJwtToken(accessToken);
                        // Consider token problematic if it expires within 5 minutes
                        tokenIsExpiredOrMissing = jwt.ValidTo <= DateTime.UtcNow.AddMinutes(5);
                    }
                }
                catch
                {
                    // If we can't parse the token, assume it's bad
                    tokenIsExpiredOrMissing = true;
                }
            }
            
            if (tokenIsExpiredOrMissing)
            {
                _logger.LogWarning(
                    "Received 403 Forbidden with expired/missing token — treating as session expiration. Request: {Method} {Uri}",
                    request.Method, request.RequestUri);
                
                if (!string.IsNullOrEmpty(userKey))
                {
                    GlobalTokenStore.RemoveToken(userKey);
                }
                
                _sessionStateService.NotifySessionExpired();
            }
            else
            {
                // Legitimate permission denial — log but do NOT trigger session expiration
                _logger.LogWarning(
                    "Received 403 Forbidden (permission denied) from API. Request: {Method} {Uri}",
                    request.Method, request.RequestUri);
            }
        }

        return response;
    }
}
