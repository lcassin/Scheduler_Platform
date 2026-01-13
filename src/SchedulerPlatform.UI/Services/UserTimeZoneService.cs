using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Implementation of IUserTimeZoneService that detects the user's timezone from their browser
/// and provides consistent date/time display across the application.
/// Uses the user's stored PreferredTimeZone from the database when available.
/// </summary>
public class UserTimeZoneService : IUserTimeZoneService, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly UserPermissionCacheService _permissionCache;
    private string? _cachedTimeZoneId;
    private TimeZoneInfo? _cachedTimeZone;
    private bool _disposed;
    
    /// <summary>
    /// Default timezone for users who don't have a preference set.
    /// </summary>
    private const string DefaultTimeZone = "Central Standard Time";
    
    // Map of Windows timezone IDs to abbreviations
    private static readonly Dictionary<string, string> TimeZoneAbbreviations = new()
    {
        { "Eastern Standard Time", "ET" },
        { "Central Standard Time", "CT" },
        { "Mountain Standard Time", "MT" },
        { "Pacific Standard Time", "PT" },
        { "US Mountain Standard Time", "MST" },
        { "Alaskan Standard Time", "AKT" },
        { "Hawaiian Standard Time", "HST" },
        { "US Eastern Standard Time", "ET" },
        { "UTC", "UTC" }
    };
    
    // Map of IANA timezone IDs to Windows timezone IDs
    private static readonly Dictionary<string, string> IanaToWindowsMap = new()
    {
        { "America/New_York", "Eastern Standard Time" },
        { "America/Chicago", "Central Standard Time" },
        { "America/Denver", "Mountain Standard Time" },
        { "America/Phoenix", "US Mountain Standard Time" },
        { "America/Los_Angeles", "Pacific Standard Time" },
        { "America/Anchorage", "Alaskan Standard Time" },
        { "Pacific/Honolulu", "Hawaiian Standard Time" },
        { "America/Detroit", "Eastern Standard Time" },
        { "America/Indiana/Indianapolis", "US Eastern Standard Time" },
        { "America/Boise", "Mountain Standard Time" },
        { "UTC", "UTC" },
        { "Etc/UTC", "UTC" }
    };

    public UserTimeZoneService(IJSRuntime jsRuntime, AuthenticationStateProvider authStateProvider, UserPermissionCacheService permissionCache)
    {
        _jsRuntime = jsRuntime;
        _authStateProvider = authStateProvider;
        _permissionCache = permissionCache;
        
        // Subscribe to permission refresh events to update timezone when user preferences change
        _permissionCache.OnPermissionsRefreshed += OnPermissionsRefreshed;
    }
    
    /// <summary>
    /// Clears the cached timezone when permissions are refreshed.
    /// This ensures the timezone is re-evaluated with the latest user preferences.
    /// </summary>
    private void OnPermissionsRefreshed()
    {
        // Clear the cache so the next call will re-evaluate the timezone
        // using the updated permission cache
        _cachedTimeZoneId = null;
        _cachedTimeZone = null;
    }
    
    /// <summary>
    /// Disposes the service and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _permissionCache.OnPermissionsRefreshed -= OnPermissionsRefreshed;
            _disposed = true;
        }
    }

    public async Task<string> GetUserTimeZoneIdAsync()
    {
        if (_cachedTimeZoneId != null)
            return _cachedTimeZoneId;

        try
        {
            // First, check if user has a stored PreferredTimeZone in the database
            var preferredTimeZone = _permissionCache.GetPreferredTimeZone();
            if (!string.IsNullOrEmpty(preferredTimeZone))
            {
                _cachedTimeZoneId = preferredTimeZone;
                return _cachedTimeZoneId;
            }

            // Second, try to get timezone from user claims
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var timezoneClaim = authState.User.FindFirst("timezone")?.Value;
            
            if (!string.IsNullOrEmpty(timezoneClaim))
            {
                _cachedTimeZoneId = timezoneClaim;
                return _cachedTimeZoneId;
            }

            // Third, try browser timezone detection via JavaScript
            try
            {
                var ianaTimeZone = await _jsRuntime.InvokeAsync<string>("Intl.DateTimeFormat().resolvedOptions().timeZone");
                
                if (!string.IsNullOrEmpty(ianaTimeZone))
                {
                    // Convert IANA to Windows timezone ID if we have a mapping
                    if (IanaToWindowsMap.TryGetValue(ianaTimeZone, out var windowsId))
                    {
                        _cachedTimeZoneId = windowsId;
                        return _cachedTimeZoneId;
                    }
                    else
                    {
                        // Try to find a matching Windows timezone by searching
                        var foundTimeZone = FindWindowsTimeZoneFromIana(ianaTimeZone);
                        if (foundTimeZone != null)
                        {
                            _cachedTimeZoneId = foundTimeZone;
                            return _cachedTimeZoneId;
                        }
                    }
                }
            }
            catch
            {
                // JS interop failed (e.g., during prerendering), continue to default
            }

            // Ultimate fallback to configured default timezone (not server local)
            _cachedTimeZoneId = DefaultTimeZone;
        }
        catch (Exception)
        {
            // If anything fails, use the configured default timezone
            _cachedTimeZoneId = DefaultTimeZone;
        }

        return _cachedTimeZoneId;
    }

    public async Task<DateTime> ToUserTimeAsync(DateTime utcDateTime)
    {
        var timeZone = await GetTimeZoneInfoAsync();
        
        // Handle Unspecified kind by treating it as UTC
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
    }

    public async Task<string> GetUserTimeZoneAbbreviationAsync()
    {
        var timeZoneId = await GetUserTimeZoneIdAsync();
        
        if (TimeZoneAbbreviations.TryGetValue(timeZoneId, out var abbr))
            return abbr;

        try
        {
            var tz = await GetTimeZoneInfoAsync();
            return tz.IsDaylightSavingTime(DateTime.Now) ? tz.DaylightName : tz.StandardName;
        }
        catch
        {
            return timeZoneId;
        }
    }

    public async Task<string> FormatDateTimeAsync(DateTime utcDateTime, string format = "g")
    {
        var localTime = await ToUserTimeAsync(utcDateTime);
        var abbr = await GetUserTimeZoneAbbreviationAsync();
        return $"{localTime.ToString(format)} ({abbr})";
    }

    public async Task<string> FormatDateTimeAsync(DateTime? utcDateTime, string format = "g", string nullDisplay = "N/A")
    {
        if (!utcDateTime.HasValue)
            return nullDisplay;
        
        return await FormatDateTimeAsync(utcDateTime.Value, format);
    }

    private async Task<TimeZoneInfo> GetTimeZoneInfoAsync()
    {
        if (_cachedTimeZone != null)
            return _cachedTimeZone;

        var timeZoneId = await GetUserTimeZoneIdAsync();
        
        try
        {
            _cachedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            _cachedTimeZone = TimeZoneInfo.Local;
        }

        return _cachedTimeZone;
    }

    private static string? FindWindowsTimeZoneFromIana(string ianaId)
    {
        // Try to find a Windows timezone that contains the IANA city name
        var cityPart = ianaId.Split('/').LastOrDefault()?.Replace("_", " ");
        if (string.IsNullOrEmpty(cityPart))
            return null;

        try
        {
            foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                if (tz.DisplayName.Contains(cityPart, StringComparison.OrdinalIgnoreCase) ||
                    tz.StandardName.Contains(cityPart, StringComparison.OrdinalIgnoreCase))
                {
                    return tz.Id;
                }
            }
        }
        catch
        {
            // Ignore errors when searching timezones
        }

        return null;
    }
}
