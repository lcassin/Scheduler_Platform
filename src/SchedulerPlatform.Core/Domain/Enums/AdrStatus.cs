namespace SchedulerPlatform.Core.Domain.Enums;

/// <summary>
/// ADR API status codes - must match the ADRStatus table in the ADR database.
/// Reference:
///   1  Inserted                        IsError=0  IsFinal=0  (still in queue)
///   2  Inserted with Priority          IsError=0  IsFinal=0  (still in queue)
///   3  Invalid CredentialID            IsError=1  IsFinal=0  (manual refire only)
///   4  Cannot Connect To VCM           IsError=1  IsFinal=0  (auto-refire)
///   5  Cannot Insert Into Queue        IsError=1  IsFinal=0  (auto-refire)
///   6  Sent To AI                      IsError=0  IsFinal=0  (still in queue)
///   7  Cannot Connect To AI            IsError=1  IsFinal=0  (auto-refire)
///   8  Cannot Save Result              IsError=1  IsFinal=0  (auto-refire)
///   9  Needs Human Review              IsError=1  IsFinal=1
///  10  Received From AI                IsError=0  IsFinal=0  (still in queue)
///  11  Document Retrieval Complete     IsError=0  IsFinal=1
///  12  AI Canceled                     IsError=0  IsFinal=1
///  13  Login Attempt Succeeded         IsError=0  IsFinal=0  (auto-refire - cred check only, scrape still needed)
///  14  No Documents Found              IsError=0  IsFinal=0  (auto-refire - docs may arrive later)
///  15  Failed to Process All Documents IsError=1  IsFinal=0  (auto-refire)
///  16  No Documents Processed          IsError=1  IsFinal=0  (auto-refire)
///  17  AI Timeout                      IsError=1  IsFinal=0  (auto-refire)
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
    /// AI cancelled the request. Not an error. (IsFinal = true)
    /// </summary>
    AiCanceled = 12,
    
    /// <summary>
    /// Credential verification succeeded. Used for credential check jobs.
    /// Not final for scrape jobs - the scrape still needs to happen. (auto-refire)
    /// </summary>
    LoginAttemptSucceeded = 13,
    
    /// <summary>
    /// No documents found for this scrape attempt. Not an error.
    /// Not final - documents may arrive later within the billing window. (auto-refire)
    /// </summary>
    NoDocumentsFound = 14,
    
    /// <summary>
    /// Partial failure - some documents may have succeeded but others failed (IsError = true)
    /// </summary>
    FailedToProcessAllDocuments = 15,
    
    /// <summary>
    /// No documents were processed (IsError = true)
    /// </summary>
    NoDocumentsProcessed = 16,
    
    /// <summary>
    /// AI processing timed out (IsError = true). Requires automatic re-fire. (auto-refire)
    /// </summary>
    AiTimeout = 17
}

/// <summary>
/// Extension methods for AdrStatus
/// </summary>
public static class AdrStatusExtensions
{
    /// <summary>
    /// Returns true if this status indicates an error (per ADR API IsError column)
    /// </summary>
    public static bool IsError(this AdrStatus status)
    {
        return status switch
        {
            AdrStatus.InvalidCredentialId => true,       // 3
            AdrStatus.CannotConnectToVcm => true,        // 4
            AdrStatus.CannotInsertIntoQueue => true,     // 5
            AdrStatus.CannotConnectToAi => true,         // 7
            AdrStatus.CannotSaveResult => true,          // 8
            AdrStatus.NeedsHumanReview => true,          // 9
            AdrStatus.FailedToProcessAllDocuments => true, // 15
            AdrStatus.NoDocumentsProcessed => true,      // 16
            AdrStatus.AiTimeout => true,                  // 17
            _ => false
        };
    }
    
    /// <summary>
    /// Returns true if this status is final (per ADR API IsFinal column)
    /// </summary>
    public static bool IsFinal(this AdrStatus status)
    {
        return status switch
        {
            AdrStatus.NeedsHumanReview => true,          // 9
            AdrStatus.Complete => true,                   // 11
            AdrStatus.AiCanceled => true,                // 12 - not an error, but final
            // Note: 13 (LoginAttemptSucceeded) and 14 (NoDocumentsFound) are NOT final - they trigger auto-refire
            _ => false
        };
    }
    
    /// <summary>
    /// Returns true if this status should trigger an automatic re-fire of the scrape request.
    /// These statuses indicate transient errors, timeouts, or incomplete processing that
    /// warrants retrying the same billing range. Jobs with these statuses are reverted to
    /// CredentialVerified so the scraping step picks them up and re-fires.
    /// </summary>
    public static bool ShouldRefire(this AdrStatus status)
    {
        return status switch
        {
            AdrStatus.CannotConnectToVcm => true,        // 4 - VCM connection error, retry
            AdrStatus.CannotInsertIntoQueue => true,     // 5 - Queue error, retry
            AdrStatus.CannotConnectToAi => true,         // 7 - AI connection error, retry
            AdrStatus.CannotSaveResult => true,          // 8 - Storage error, retry
            AdrStatus.LoginAttemptSucceeded => true,     // 13 - Cred check only, scrape still needed
            AdrStatus.NoDocumentsFound => true,          // 14 - Docs may arrive later
            AdrStatus.FailedToProcessAllDocuments => true, // 15 - Partial failure, retry
            AdrStatus.NoDocumentsProcessed => true,      // 16 - Processing failure, retry
            AdrStatus.AiTimeout => true,                  // 17 - AI timed out, retry
            _ => false
        };
    }
    
    /// <summary>
    /// Gets the set of all StatusIds that should trigger automatic re-fire.
    /// Useful for database queries where the enum isn't available.
    /// </summary>
    public static HashSet<int> GetRefireStatusIds()
    {
        return new HashSet<int> { 4, 5, 7, 8, 13, 14, 15, 16, 17 };
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
            AdrStatus.Complete => "Document Retrieval Complete",
            AdrStatus.AiCanceled => "AI Canceled",
            AdrStatus.LoginAttemptSucceeded => "Login Attempt Succeeded",
            AdrStatus.NoDocumentsFound => "No Documents Found",
            AdrStatus.FailedToProcessAllDocuments => "Failed to Process All Documents",
            AdrStatus.NoDocumentsProcessed => "No Documents Processed",
            AdrStatus.AiTimeout => "AI Timeout",
            _ => "Unknown"
        };
    }
}
