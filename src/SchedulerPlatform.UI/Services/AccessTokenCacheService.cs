namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Circuit-scoped service that caches the user's access token for use when HttpContext is unavailable.
/// In Blazor Server, HttpContext can be null during SignalR circuit events (after the initial HTTP request).
/// This service stores the access token when HttpContext is available and provides it when HttpContext is null.
/// </summary>
public class AccessTokenCacheService
{
    private string? _cachedAccessToken;
    private DateTimeOffset? _tokenExpiry;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the cached access token, or null if no token is cached or the token has expired.
    /// </summary>
    public string? GetCachedToken()
    {
        lock (_lock)
        {
            // Return null if token is expired
            if (_tokenExpiry.HasValue && _tokenExpiry.Value <= DateTimeOffset.UtcNow)
            {
                _cachedAccessToken = null;
                _tokenExpiry = null;
                return null;
            }
            
            return _cachedAccessToken;
        }
    }

    /// <summary>
    /// Updates the cached access token. Call this when HttpContext is available
    /// (e.g., during initial page load, keepalive requests, or after token refresh).
    /// </summary>
    /// <param name="accessToken">The access token to cache.</param>
    /// <param name="expiresAt">Optional expiry time for the token.</param>
    public void UpdateToken(string? accessToken, DateTimeOffset? expiresAt = null)
    {
        lock (_lock)
        {
            _cachedAccessToken = accessToken;
            _tokenExpiry = expiresAt;
        }
    }

    /// <summary>
    /// Clears the cached token. Call this on logout or when the session expires.
    /// </summary>
    public void ClearToken()
    {
        lock (_lock)
        {
            _cachedAccessToken = null;
            _tokenExpiry = null;
        }
    }

    /// <summary>
    /// Gets whether a valid (non-expired) token is cached.
    /// </summary>
    public bool HasValidToken
    {
        get
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_cachedAccessToken))
                    return false;
                    
                if (_tokenExpiry.HasValue && _tokenExpiry.Value <= DateTimeOffset.UtcNow)
                    return false;
                    
                return true;
            }
        }
    }
}
