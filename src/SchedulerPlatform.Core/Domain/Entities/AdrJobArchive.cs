using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Archive table for AdrJob records that have exceeded the retention period.
/// Maintains the same schema as AdrJob for historical reference.
/// </summary>
public class AdrJobArchive
{
    /// <summary>
    /// Original AdrJobId from the source table
    /// </summary>
    public int AdrJobArchiveId { get; set; }
    
    /// <summary>
    /// Original AdrJobId before archiving
    /// </summary>
    public int OriginalAdrJobId { get; set; }
    
    /// <summary>
    /// FK to AdrAccount table
    /// </summary>
    public int AdrAccountId { get; set; }
    
    /// <summary>
    /// FK to AdrAccountRule table
    /// </summary>
    public int? AdrAccountRuleId { get; set; }
    
    /// <summary>
    /// External account ID from VendorCred
    /// </summary>
    public long VMAccountId { get; set; }
    
    /// <summary>
    /// Vendor account number
    /// </summary>
    public string VMAccountNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Primary vendor code
    /// </summary>
    public string? PrimaryVendorCode { get; set; }
    
    /// <summary>
    /// Master vendor code that groups related primary vendor codes
    /// </summary>
    public string? MasterVendorCode { get; set; }
    
    /// <summary>
    /// Credential ID used for this job
    /// </summary>
    public int CredentialId { get; set; }
    
    /// <summary>
    /// Billing period type
    /// </summary>
    public string? PeriodType { get; set; }
    
    /// <summary>
    /// Start of the billing period
    /// </summary>
    public DateTime BillingPeriodStartDateTime { get; set; }
    
    /// <summary>
    /// End of the billing period
    /// </summary>
    public DateTime BillingPeriodEndDateTime { get; set; }
    
    /// <summary>
    /// Expected scrape date
    /// </summary>
    public DateTime? NextRunDateTime { get; set; }
    
    /// <summary>
    /// Search window start
    /// </summary>
    public DateTime? NextRangeStartDateTime { get; set; }
    
    /// <summary>
    /// Search window end
    /// </summary>
    public DateTime? NextRangeEndDateTime { get; set; }
    
    /// <summary>
    /// Job status at time of archival
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Flag for missing accounts
    /// </summary>
    public bool IsMissing { get; set; }
    
    /// <summary>
    /// Latest ADR status ID
    /// </summary>
    public int? AdrStatusId { get; set; }
    
    /// <summary>
    /// Latest ADR status description
    /// </summary>
    public string? AdrStatusDescription { get; set; }
    
    /// <summary>
    /// Index ID from ADR API
    /// </summary>
    public long? AdrIndexId { get; set; }
    
    /// <summary>
    /// Credential verification timestamp
    /// </summary>
    public DateTime? CredentialVerifiedDateTime { get; set; }
    
    /// <summary>
    /// Scraping completion timestamp
    /// </summary>
    public DateTime? ScrapingCompletedDateTime { get; set; }
    
    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Whether this was a manual request
    /// </summary>
    public bool IsManualRequest { get; set; }
    
    /// <summary>
    /// Manual request reason
    /// </summary>
    public string? ManualRequestReason { get; set; }
    
    /// <summary>
    /// Last status check response
    /// </summary>
    public string? LastStatusCheckResponse { get; set; }
    
    /// <summary>
    /// Last status check timestamp
    /// </summary>
    public DateTime? LastStatusCheckDateTime { get; set; }
    
    // Original audit fields
    public DateTime CreatedDateTime { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedDateTime { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when this record was archived
    /// </summary>
    public DateTime ArchivedDateTime { get; set; }
    
    /// <summary>
    /// User or process that performed the archival
    /// </summary>
    public string ArchivedBy { get; set; } = string.Empty;
}
