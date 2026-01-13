using System.Collections.Concurrent;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Stores cached access tokens in a static dictionary keyed by user identifier.
/// This allows tokens to persist across different DI scopes and IHttpClientFactory handler lifetimes.
/// </summary>
public static class GlobalTokenStore
{
    private static readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();
    
    // AsyncLocal to track the current user key across async operations within a circuit
    private static readonly AsyncLocal<string?> _currentUserKey = new();
    
    internal record TokenEntry(string Token, DateTimeOffset? ExpiresAt);
    
    /// <summary>
    /// Gets or sets the current user key for this async context.
    /// This persists across async operations within the same logical flow.
    /// </summary>
    public static string? CurrentUserKey
    {
        get => _currentUserKey.Value;
        set => _currentUserKey.Value = value;
    }
    
    public static string? GetToken(string? userKey = null)
    {
        // Use provided key or fall back to current async context key
        var key = userKey ?? CurrentUserKey;
        
        if (string.IsNullOrEmpty(key))
            return null;
            
        if (_tokens.TryGetValue(key, out var entry))
        {
            // Check if expired
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                _tokens.TryRemove(key, out _);
                return null;
            }
            return entry.Token;
        }
        return null;
    }
    
    public static void SetToken(string userKey, string token, DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrEmpty(userKey) || string.IsNullOrEmpty(token))
            return;
            
        _tokens[userKey] = new TokenEntry(token, expiresAt);
        
        // Also set as current user key for this async context
        CurrentUserKey = userKey;
    }
    
    public static void RemoveToken(string? userKey = null)
    {
        var key = userKey ?? CurrentUserKey;
        if (!string.IsNullOrEmpty(key))
            _tokens.TryRemove(key, out _);
    }
    
    public static bool HasValidToken(string? userKey = null)
    {
        return GetToken(userKey) != null;
    }
}

/// <summary>
/// Circuit-scoped service that caches the user's access token for use when HttpContext is unavailable.
/// In Blazor Server, HttpContext can be null during SignalR circuit events (after the initial HTTP request).
/// 
/// This service uses a static token store keyed by user identifier to ensure tokens persist
/// across different DI scopes and IHttpClientFactory handler lifetimes. This is necessary because
/// IHttpClientFactory pools handlers with a 2-minute lifetime, and resolving scoped services
/// from different scopes would result in different (empty) cache instances.
/// </summary>
public class AccessTokenCacheService
{
    private string? _userKey;
    private readonly object _lock = new();

    /// <summary>
    /// Sets the user key for this cache instance. Must be called when the user is known
    /// (typically from HttpContext.User claims during initial page load).
    /// </summary>
    public void SetUserKey(string? userKey)
    {
        lock (_lock)
        {
            _userKey = userKey;
        }
    }
    
    /// <summary>
    /// Gets the current user key.
    /// </summary>
    public string? UserKey
    {
        get
        {
            lock (_lock)
            {
                return _userKey;
            }
        }
    }

    /// <summary>
    /// Gets the cached access token, or null if no token is cached or the token has expired.
    /// </summary>
    public string? GetCachedToken()
    {
        var key = UserKey;
        if (string.IsNullOrEmpty(key))
            return null;
            
        return GlobalTokenStore.GetToken(key);
    }

    /// <summary>
    /// Updates the cached access token. Call this when HttpContext is available
    /// (e.g., during initial page load, keepalive requests, or after token refresh).
    /// </summary>
    /// <param name="accessToken">The access token to cache.</param>
    /// <param name="expiresAt">Optional expiry time for the token.</param>
    public void UpdateToken(string? accessToken, DateTimeOffset? expiresAt = null)
    {
        var key = UserKey;
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(accessToken))
            return;
            
        GlobalTokenStore.SetToken(key, accessToken, expiresAt);
    }

    /// <summary>
    /// Clears the cached token. Call this on logout or when the session expires.
    /// </summary>
    public void ClearToken()
    {
        var key = UserKey;
        if (!string.IsNullOrEmpty(key))
            GlobalTokenStore.RemoveToken(key);
    }

    /// <summary>
    /// Gets whether a valid (non-expired) token is cached.
    /// </summary>
    public bool HasValidToken
    {
        get
        {
            var key = UserKey;
            if (string.IsNullOrEmpty(key))
                return false;
                
            return GlobalTokenStore.HasValidToken(key);
        }
    }
}
