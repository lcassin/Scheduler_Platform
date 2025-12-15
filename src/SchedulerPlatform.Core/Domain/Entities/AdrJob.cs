using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents a single ADR scraping job for one account/billing period.
/// Each billing period for an account gets its own Job record.
/// </summary>
public class AdrJob : BaseEntity
{
    /// <summary>
    /// FK to AdrAccount table
    /// </summary>
    public int AdrAccountId { get; set; }
    
    /// <summary>
    /// External account ID from VendorCred (denormalized for queries)
    /// </summary>
    public long VMAccountId { get; set; }
    
        /// <summary>
        /// Vendor account number (denormalized for queries)
        /// </summary>
        public string VMAccountNumber { get; set; } = string.Empty;
    
        /// <summary>
        /// Vendor code (denormalized for queries and schedule linking)
        /// </summary>
        public string? VendorCode { get; set; }
    
        /// <summary>
        /// Credential ID used for this job
        /// </summary>
        public int CredentialId { get; set; }
    
    /// <summary>
    /// Billing period type (e.g., Monthly, Quarterly)
    /// </summary>
    public string? PeriodType { get; set; }
    
    /// <summary>
    /// Start of the billing period this Job targets (e.g., 2025-01-01 for January)
    /// </summary>
    public DateTime BillingPeriodStartDateTime { get; set; }
    
    /// <summary>
    /// End of the billing period this Job targets (e.g., 2025-01-31 for January)
    /// </summary>
    public DateTime BillingPeriodEndDateTime { get; set; }
    
    /// <summary>
    /// Expected scrape date
    /// </summary>
    public DateTime? NextRunDateTime { get; set; }
    
    /// <summary>
    /// Search window start for scraping
    /// </summary>
    public DateTime? NextRangeStartDateTime { get; set; }
    
    /// <summary>
    /// Search window end for scraping
    /// </summary>
    public DateTime? NextRangeEndDateTime { get; set; }
    
    /// <summary>
    /// Current job status (Pending, CredentialVerified, Scraping, Complete, Failed, NeedsReview)
    /// </summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>
    /// Flag for missing accounts
    /// </summary>
    public bool IsMissing { get; set; }
    
    /// <summary>
    /// Latest ADR status ID from the API
    /// </summary>
    public int? AdrStatusId { get; set; }
    
    /// <summary>
    /// Latest ADR status description
    /// </summary>
    public string? AdrStatusDescription { get; set; }
    
    /// <summary>
    /// Index ID returned from ADR API
    /// </summary>
    public long? AdrIndexId { get; set; }
    
    /// <summary>
    /// Date/time credential verification was completed
    /// </summary>
    public DateTime? CredentialVerifiedDateTime { get; set; }
    
    /// <summary>
    /// Date/time scraping was completed
    /// </summary>
    public DateTime? ScrapingCompletedDateTime { get; set; }
    
    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
        /// <summary>
        /// Number of scrape retry attempts
        /// </summary>
        public int RetryCount { get; set; }
    
        /// <summary>
        /// Indicates this job was created manually by an admin (not through orchestration).
        /// Manual jobs are excluded from normal orchestration processing but can be tracked
        /// and have their status checked through the UI.
        /// </summary>
        public bool IsManualRequest { get; set; }
    
        /// <summary>
        /// Reason provided by admin for manual request (for audit purposes)
        /// </summary>
        public string? ManualRequestReason { get; set; }
    
        /// <summary>
        /// Raw API response from the last status check (for debugging).
        /// Stores the JSON response to help diagnose API format changes.
        /// </summary>
        public string? LastStatusCheckResponse { get; set; }
    
        /// <summary>
        /// Timestamp of the last status check
        /// </summary>
        public DateTime? LastStatusCheckDateTime { get; set; }
    
        // Navigation properties
    [JsonIgnore]
    public AdrAccount AdrAccount { get; set; } = null!;
    
    [JsonIgnore]
    public ICollection<AdrJobExecution> AdrJobExecutions { get; set; } = new List<AdrJobExecution>();
}
