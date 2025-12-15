namespace SchedulerPlatform.UI.Models;

public class AdrAccountSyncResult
{
    public int TotalAccountsProcessed { get; set; }
    public int AccountsInserted { get; set; }
    public int AccountsUpdated { get; set; }
    public int AccountsMarkedDeleted { get; set; }
    public int ClientsCreated { get; set; }
    public int ClientsUpdated { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public DateTime SyncStartDateTime { get; set; }
    public DateTime SyncEndDateTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class JobCreationResult
{
    public int JobsCreated { get; set; }
    public int JobsSkipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class CredentialVerificationResult
{
    public int JobsProcessed { get; set; }
    public int CredentialsVerified { get; set; }
    public int CredentialsFailed { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class ScrapeResult
{
    public int JobsProcessed { get; set; }
    public int ScrapesRequested { get; set; }
    public int ScrapesCompleted { get; set; }
    public int ScrapesFailed { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class StatusCheckResult
{
    public int JobsChecked { get; set; }
    public int JobsCompleted { get; set; }
    public int JobsNeedingReview { get; set; }
    public int JobsStillProcessing { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class FullCycleResult
{
    public AdrAccountSyncResult? SyncResult { get; set; }
    public JobCreationResult? JobCreationResult { get; set; }
    public CredentialVerificationResult? CredentialResult { get; set; }
    public ScrapeResult? ScrapeResult { get; set; }
    public StatusCheckResult? StatusResult { get; set; }
}

public class RefireJobResult
{
    public string Message { get; set; } = string.Empty;
    public int JobId { get; set; }
}

public class RefireJobsBulkResult
{
    public string Message { get; set; } = string.Empty;
    public int RefiredCount { get; set; }
    public int TotalRequested { get; set; }
    public List<string>? Errors { get; set; }
}

public class ManualScrapeResult
{
    public string Message { get; set; } = string.Empty;
    public int JobId { get; set; }
    public int ExecutionId { get; set; }
    public int AccountId { get; set; }
    public string? VMAccountNumber { get; set; }
    public int? CredentialId { get; set; }
    public DateTime RangeStartDate { get; set; }
    public DateTime RangeEndDate { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    // API Response details
    public int? HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsError { get; set; }
    public bool IsFinal { get; set; }
    public int? StatusId { get; set; }
    public string? StatusDescription { get; set; }
    public long? IndexId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CheckJobStatusResult
{
    public int JobId { get; set; }
    public int ExecutionId { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsError { get; set; }
    public bool IsFinal { get; set; }
    public int? StatusId { get; set; }
    public string? StatusDescription { get; set; }
    public long? IndexId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? JobStatus { get; set; }
}

public class BackgroundOrchestrationRequest
{
    public bool RunSync { get; set; } = true;
    public bool RunCreateJobs { get; set; } = true;
    public bool RunCredentialVerification { get; set; } = true;
    public bool RunScraping { get; set; } = true;
    public bool RunStatusCheck { get; set; } = true;
    
    /// <summary>
    /// When true and RunStatusCheck is true, checks ALL jobs with ScrapeRequested status
    /// regardless of timing criteria. Used by the "Check Statuses Only" button.
    /// </summary>
    public bool CheckAllScrapedStatuses { get; set; } = false;
}

public class BackgroundOrchestrationResponse
{
    public string Message { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
}

public class OrchestrationCurrentResponse
{
    public bool IsRunning { get; set; }
    public string? Message { get; set; }
    public AdrOrchestrationStatus? Status { get; set; }
}

public class AdrOrchestrationStatus
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Queued"; // Queued, Running, Completed, Failed, Cancelled
    public string? CurrentStep { get; set; }
    public string? CurrentStepPhase { get; set; } // Preparing, Calling API, Saving results
    public string? ErrorMessage { get; set; }
    
    // Progress tracking for current step
    public int CurrentStepProgress { get; set; }
    public int CurrentStepTotal { get; set; }
    
    // Results from each step
    public AdrAccountSyncResult? SyncResult { get; set; }
    public JobCreationResult? JobCreationResult { get; set; }
    public CredentialVerificationResult? CredentialVerificationResult { get; set; }
    public ScrapeResult? ScrapeResult { get; set; }
    public StatusCheckResult? StatusCheckResult { get; set; }
}
