namespace SchedulerPlatform.UI.Models;

public class AdrAccountSyncResult
{
    public int TotalAccountsProcessed { get; set; }
    public int AccountsInserted { get; set; }
    public int AccountsUpdated { get; set; }
    public int AccountsMarkedDeleted { get; set; }
    public List<string> Errors { get; set; } = new();
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
