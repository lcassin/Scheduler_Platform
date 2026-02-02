namespace SchedulerPlatform.Core.Domain.Enums;

/// <summary>
/// ADR API status codes
/// </summary>
public enum AdrStatus
{
    /// <summary>
    /// Request received
    /// </summary>
    Inserted = 1,
    
    /// <summary>
    /// High-priority request received
    /// </summary>
    InsertedWithPriority = 2,
    
    /// <summary>
    /// Credential validation failed (IsError = true)
    /// </summary>
    InvalidCredentialId = 3,
    
    /// <summary>
    /// VCM connection failure (IsError = true)
    /// </summary>
    CannotConnectToVcm = 4,
    
    /// <summary>
    /// Queue insertion failed (IsError = true)
    /// </summary>
    CannotInsertIntoQueue = 5,
    
    /// <summary>
    /// Request sent to AI scraper
    /// </summary>
    SentToAi = 6,
    
    /// <summary>
    /// AI connection failure (IsError = true)
    /// </summary>
    CannotConnectToAi = 7,
    
    /// <summary>
    /// Result storage failed (IsError = true)
    /// </summary>
    CannotSaveResult = 8,
    
    /// <summary>
    /// Manual intervention required (IsError = true, IsFinal = true)
    /// </summary>
    NeedsHumanReview = 9,
    
    /// <summary>
    /// AI returned results
    /// </summary>
    ReceivedFromAi = 10,
    
    /// <summary>
    /// Successfully completed (IsFinal = true)
    /// </summary>
    Complete = 11,
    
    /// <summary>
    /// Credential verification succeeded (used for credential checks, not scraping)
    /// </summary>
    LoginAttemptSucceeded = 12,
    
    /// <summary>
    /// No documents found for this scrape attempt (NOT final - retry next day)
    /// </summary>
    NoDocumentsFound = 13,
    
    /// <summary>
    /// Partial failure - some documents may have succeeded but others failed (IsError = true, IsFinal = true)
    /// </summary>
    FailedToProcessAllDocuments = 14,
    
    /// <summary>
    /// No documents processed - status meaning TBD (NOT final)
    /// </summary>
    NoDocumentsProcessed = 15
}

/// <summary>
/// Extension methods for AdrStatus
/// </summary>
public static class AdrStatusExtensions
{
    /// <summary>
    /// Returns true if this status indicates an error
    /// </summary>
    public static bool IsError(this AdrStatus status)
    {
        return status switch
        {
            AdrStatus.InvalidCredentialId => true,
            AdrStatus.CannotConnectToVcm => true,
            AdrStatus.CannotInsertIntoQueue => true,
            AdrStatus.CannotConnectToAi => true,
            AdrStatus.CannotSaveResult => true,
            AdrStatus.NeedsHumanReview => true,
            AdrStatus.FailedToProcessAllDocuments => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Returns true if this status is final (no more processing needed)
    /// </summary>
    public static bool IsFinal(this AdrStatus status)
    {
        return status switch
        {
            AdrStatus.NeedsHumanReview => true,
            AdrStatus.Complete => true,
            AdrStatus.FailedToProcessAllDocuments => true,
            // Note: LoginAttemptSucceeded (12) is NOT final for scraping context
            // Note: NoDocumentsFound (13) is NOT final - retry next day
            _ => false
        };
    }
    
    /// <summary>
    /// Gets the description for this status
    /// </summary>
    public static string GetDescription(this AdrStatus status)
    {
        return status switch
        {
            AdrStatus.Inserted => "Inserted",
            AdrStatus.InsertedWithPriority => "Inserted with Priority",
            AdrStatus.InvalidCredentialId => "Invalid CredentialID",
            AdrStatus.CannotConnectToVcm => "Cannot Connect To VCM",
            AdrStatus.CannotInsertIntoQueue => "Cannot Insert Into Queue",
            AdrStatus.SentToAi => "Sent To AI",
            AdrStatus.CannotConnectToAi => "Cannot Connect To AI",
            AdrStatus.CannotSaveResult => "Cannot Save Result",
            AdrStatus.NeedsHumanReview => "Needs Human Review",
            AdrStatus.ReceivedFromAi => "Received From AI",
            AdrStatus.Complete => "Complete",
            AdrStatus.LoginAttemptSucceeded => "Login Attempt Succeeded",
            AdrStatus.NoDocumentsFound => "No Documents Found",
            AdrStatus.FailedToProcessAllDocuments => "Failed To Process All Documents",
            AdrStatus.NoDocumentsProcessed => "No Documents Processed",
            _ => "Unknown"
        };
    }
}
