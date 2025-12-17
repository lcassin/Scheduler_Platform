using Microsoft.AspNetCore.Authentication;
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

    public AuthTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthTokenHandler> logger,
        SessionStateService sessionStateService)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _sessionStateService = sessionStateService;
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

        var response = await base.SendAsync(request, cancellationToken);

        // Check for 401 Unauthorized - session has expired
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                "Received 401 Unauthorized from API. User session may have expired. Request: {Method} {Uri}",
                request.Method, request.RequestUri);
            
            // Notify all subscribers (like MainLayout) that the session has expired
            // This allows centralized handling of session expiration with automatic redirect
            _sessionStateService.NotifySessionExpired();
            
            // Return the response without throwing - let the redirect happen gracefully
            // instead of causing exceptions to propagate through the component tree
        }

        return response;
    }
}
