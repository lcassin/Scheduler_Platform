namespace SchedulerPlatform.Core.Domain.Enums;

/// <summary>
/// Types of ADR API requests
/// </summary>
public enum AdrRequestType
{
    /// <summary>
    /// Attempt Login - credential verification
    /// </summary>
    AttemptLogin = 1,
    
    /// <summary>
    /// Download Invoice - scrape request
    /// </summary>
    DownloadInvoice = 2
}
