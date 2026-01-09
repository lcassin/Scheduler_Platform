using Microsoft.AspNetCore.Components.Authorization;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Circuit-scoped service that provides an HttpClient with automatic user authentication.
/// This service resolves the current user from AuthenticationStateProvider (which IS circuit-scoped)
/// and passes the userKey to the AuthTokenHandler via HttpRequestMessage.Options.
/// 
/// This solves the IHttpClientFactory handler pooling issue where handlers are created in a different
/// scope than the Blazor circuit, making it impossible for them to access circuit-scoped state directly.
/// </summary>
public class AuthenticatedHttpClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<AuthenticatedHttpClientService> _logger;
    private string? _cachedUserKey;
    private readonly SemaphoreSlim _userKeyLock = new(1, 1);

    public AuthenticatedHttpClientService(
        IHttpClientFactory httpClientFactory,
        AuthenticationStateProvider authStateProvider,
        ILogger<AuthenticatedHttpClientService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user's key from the AuthenticationStateProvider.
    /// The key is cached for the lifetime of this service (circuit-scoped).
    /// </summary>
    private async Task<string?> GetUserKeyAsync()
    {
        // Return cached key if available
        if (!string.IsNullOrEmpty(_cachedUserKey))
            return _cachedUserKey;

        await _userKeyLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_cachedUserKey))
                return _cachedUserKey;

            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            
            if (user?.Identity?.IsAuthenticated != true)
            {
                _logger.LogDebug("User is not authenticated, cannot get user key");
                return null;
            }

            _cachedUserKey = AuthTokenHandler.GetUserKey(user);
            
            if (string.IsNullOrEmpty(_cachedUserKey))
            {
                _logger.LogWarning("Could not extract user key from authenticated user claims");
            }
            else
            {
                _logger.LogDebug("Cached user key: {UserKey}", _cachedUserKey);
            }

            return _cachedUserKey;
        }
        finally
        {
            _userKeyLock.Release();
        }
    }

    /// <summary>
    /// Creates an HttpClient configured for the SchedulerAPI with the current user's authentication.
    /// </summary>
    public HttpClient CreateClient(string name = "SchedulerAPI")
    {
        return _httpClientFactory.CreateClient(name);
    }

    /// <summary>
    /// Sends an HTTP request with the current user's authentication.
    /// The userKey is automatically added to the request options so the AuthTokenHandler
    /// can retrieve the correct token from the GlobalTokenStore.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var userKey = await GetUserKeyAsync();
        
        if (!string.IsNullOrEmpty(userKey))
        {
            request.Options.Set(AuthTokenHandlerOptions.UserKeyOption, userKey);
        }

        var client = CreateClient();
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a GET request with the current user's authentication.
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a POST request with JSON content and the current user's authentication.
    /// </summary>
    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T content, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(content)
        };
        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a PUT request with JSON content and the current user's authentication.
    /// </summary>
    public async Task<HttpResponseMessage> PutAsJsonAsync<T>(string requestUri, T content, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(content)
        };
        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a DELETE request with the current user's authentication.
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Clears the cached user key. Call this when the user logs out.
    /// </summary>
    public void ClearUserKey()
    {
        _cachedUserKey = null;
    }
}
