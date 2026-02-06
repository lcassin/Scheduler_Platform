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
    DownloadInvoice = 2,
    
    /// <summary>
    /// Rebill Check - weekly check for off-cycle invoices, updated bills, and credential verification.
    /// Unlike DownloadInvoice, rebill checks do NOT create Zendesk tickets when no document is found
    /// (only creates tickets for credential failures).
    /// </summary>
    Rebill = 3
}
