namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service to manage session state and notify components when the session expires or is about to expire.
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
    /// Event raised when the session is about to expire (within warning threshold).
    /// Components can subscribe to show a warning dialog to the user.
    /// </summary>
    public event Action<int>? OnSessionExpiringWarning;
    
    /// <summary>
    /// Event raised when the session has been successfully refreshed.
    /// Components can subscribe to dismiss any warning dialogs.
    /// </summary>
    public event Action? OnSessionRefreshed;

    /// <summary>
    /// Notifies all subscribers that the session has expired.
    /// Called by AuthTokenHandler when a 401 Unauthorized response is received.
    /// </summary>
    public void NotifySessionExpired()
    {
        OnSessionExpired?.Invoke();
    }
    
    /// <summary>
    /// Notifies all subscribers that the session is about to expire.
    /// </summary>
    /// <param name="minutesRemaining">Minutes remaining before session expires</param>
    public void NotifySessionExpiring(int minutesRemaining)
    {
        OnSessionExpiringWarning?.Invoke(minutesRemaining);
    }
    
    /// <summary>
    /// Notifies all subscribers that the session has been refreshed.
    /// </summary>
    public void NotifySessionRefreshed()
    {
        OnSessionRefreshed?.Invoke();
    }
}
