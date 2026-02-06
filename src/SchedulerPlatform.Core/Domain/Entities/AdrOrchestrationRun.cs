namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents a single ADR orchestration run with step-by-step progress tracking.
/// Persisted to database to survive application restarts.
/// </summary>
public class AdrOrchestrationRun : BaseEntity
{
    /// <summary>
    /// Unique identifier for this orchestration run (GUID)
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
    
    /// <summary>
    /// User who requested the orchestration run
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// When the orchestration was requested/queued
    /// </summary>
    public DateTime RequestedDateTime { get; set; }
    
    /// <summary>
    /// When the orchestration actually started processing
    /// </summary>
    public DateTime? StartedDateTime { get; set; }
    
    /// <summary>
    /// When the orchestration completed (success or failure)
    /// </summary>
    public DateTime? CompletedDateTime { get; set; }
    
    /// <summary>
    /// Current status: Queued, Running, Completed, Failed, Cancelled
    /// </summary>
    public string Status { get; set; } = "Queued";
    
    /// <summary>
    /// Current step being executed (for running orchestrations)
    /// </summary>
    public string? CurrentStep { get; set; }
    
    /// <summary>
    /// Current progress within the step (e.g., "150/500")
    /// </summary>
    public string? CurrentProgress { get; set; }
    
    /// <summary>
    /// Total items to process in current step
    /// </summary>
    public int? TotalItems { get; set; }
    
    /// <summary>
    /// Items processed so far in current step
    /// </summary>
    public int? ProcessedItems { get; set; }
    
    /// <summary>
    /// Error message if the run failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    // Step 1: Sync Accounts results
    public int? SyncAccountsInserted { get; set; }
    public int? SyncAccountsUpdated { get; set; }
    public int? SyncAccountsTotal { get; set; }
    
    // Step 2: Create Jobs results
    public int? JobsCreated { get; set; }
    public int? JobsSkipped { get; set; }
    
    // Step 3: Process Rebill results (replaces Verify Credentials)
    public int? CredentialsVerified { get; set; }
    public int? CredentialsFailed { get; set; }
    
    // Step 4: Process Scraping results
    public int? ScrapingRequested { get; set; }
    public int? ScrapingFailed { get; set; }
    
    // Step 5: Check Statuses results
    public int? StatusesChecked { get; set; }
    public int? StatusesFailed { get; set; }
}
