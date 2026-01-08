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

    public AuthTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthTokenHandler> logger,
        SessionStateService sessionStateService,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _sessionStateService = sessionStateService;
        _configuration = configuration;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            try
            {
                var accessToken = await httpContext.GetTokenAsync("access_token");
                
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
                                "Token attached to request. Issuer: {Issuer}, Audience: {Audience}, ClientId: {ClientId}, Expires: {Expires}, IsExpired: {IsExpired}",
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
                else
                {
                    _logger.LogWarning("No access token available for authenticated user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get access token for request");
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
