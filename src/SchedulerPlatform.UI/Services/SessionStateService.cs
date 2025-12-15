namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service to manage session state and notify components when the session expires.
/// This allows centralized handling of session expiration across the application.
/// </summary>
public class SessionStateService
{
    /// <summary>
    /// Event raised when the session expires (401 Unauthorized received from API).
    /// Components should subscribe to this event to handle session expiration.
    /// </summary>
    public event Action? OnSessionExpired;

    /// <summary>
    /// Notifies all subscribers that the session has expired.
    /// Called by AuthTokenHandler when a 401 Unauthorized response is received.
    /// </summary>
    public void NotifySessionExpired()
    {
        OnSessionExpired?.Invoke();
    }
}
