namespace SchedulerPlatform.UI.Models;

public class AdrJob
{
    public int Id { get; set; }
    public int AdrAccountId { get; set; }
    public long VMAccountId { get; set; }
    public string VMAccountNumber { get; set; } = string.Empty;
    public string? VendorCode { get; set; }
    public int CredentialId { get; set; }
    public string? PeriodType { get; set; }
    public DateTime BillingPeriodStartDateTime { get; set; }
    public DateTime BillingPeriodEndDateTime { get; set; }
    public DateTime? NextRunDateTime { get; set; }
    public DateTime? NextRangeStartDateTime { get; set; }
    public DateTime? NextRangeEndDateTime { get; set; }
    public string Status { get; set; } = "Pending";
    public bool IsMissing { get; set; }
    public int? AdrStatusId { get; set; }
    public string? AdrStatusDescription { get; set; }
    public long? AdrIndexId { get; set; }
    public DateTime? CredentialVerifiedDateTime { get; set; }
    public DateTime? ScrapingCompletedDateTime { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public bool IsManualRequest { get; set; }
        public string? ManualRequestReason { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime? ModifiedDateTime { get; set; }
    
        // Navigation property
    public AdrAccount? AdrAccount { get; set; }
}

public class AdrJobStats
{
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int CredentialVerifiedCount { get; set; }
    public int CredentialFailedCount { get; set; }
    public int ScrapeRequestedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int NeedsReviewCount { get; set; }
}
