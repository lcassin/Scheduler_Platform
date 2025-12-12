using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents a vendor account synced from the external VendorCredNewUAT database.
/// Tracks billing patterns and scraping schedules for ADR (Automated Document Retrieval).
/// </summary>
public class AdrAccount : BaseEntity
{
    /// <summary>
    /// External account ID from VendorCred database (VMAccountId)
    /// </summary>
    public long VMAccountId { get; set; }
    
    /// <summary>
    /// Vendor account number string
    /// </summary>
    public string VMAccountNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Bank payment/tracking ID for invoices
    /// </summary>
    public string? InterfaceAccountId { get; set; }
    
    /// <summary>
    /// FK to Client table
    /// </summary>
    public int? ClientId { get; set; }
    
    /// <summary>
    /// Client name (denormalized for display)
    /// </summary>
    public string? ClientName { get; set; }
    
    /// <summary>
    /// Current active credential ID used by ADR
    /// </summary>
    public int CredentialId { get; set; }
    
    /// <summary>
    /// Vendor identifier code
    /// </summary>
    public string? VendorCode { get; set; }
    
    /// <summary>
    /// Billing frequency type (Bi-Weekly, Monthly, Bi-Monthly, Quarterly, Semi-Annually, Annually)
    /// </summary>
    public string? PeriodType { get; set; }
    
    /// <summary>
    /// Standard days between invoices for this period type
    /// </summary>
    public int? PeriodDays { get; set; }
    
    /// <summary>
    /// Calculated median interval between invoices
    /// </summary>
    public double? MedianDays { get; set; }
    
    /// <summary>
    /// Number of historical invoices found
    /// </summary>
    public int InvoiceCount { get; set; }
    
    /// <summary>
    /// Most recent invoice date found
    /// </summary>
    public DateTime? LastInvoiceDateTime { get; set; }
    
    /// <summary>
    /// Expected next invoice date based on billing pattern
    /// </summary>
    public DateTime? ExpectedNextDateTime { get; set; }
    
    /// <summary>
    /// Search window start for expected invoice
    /// </summary>
    public DateTime? ExpectedRangeStartDateTime { get; set; }
    
    /// <summary>
    /// Search window end for expected invoice
    /// </summary>
    public DateTime? ExpectedRangeEndDateTime { get; set; }
    
    /// <summary>
    /// Scheduled scrape date
    /// </summary>
    public DateTime? NextRunDateTime { get; set; }
    
    /// <summary>
    /// Scrape window start date
    /// </summary>
    public DateTime? NextRangeStartDateTime { get; set; }
    
    /// <summary>
    /// Scrape window end date
    /// </summary>
    public DateTime? NextRangeEndDateTime { get; set; }
    
    /// <summary>
    /// Days until NextRunDateTime
    /// </summary>
    public int? DaysUntilNextRun { get; set; }
    
    /// <summary>
    /// Status for next run (Run Now, Due Soon, Upcoming, Future)
    /// </summary>
    public string? NextRunStatus { get; set; }
    
    /// <summary>
    /// Historical billing status (Missing, Overdue, Due Now, Due Soon, Upcoming, Future)
    /// </summary>
    public string? HistoricalBillingStatus { get; set; }
    
    /// <summary>
    /// Last time this account was synced from external database
    /// </summary>
    public DateTime? LastSyncedDateTime { get; set; }
    
    /// <summary>
    /// Flag indicating if billing dates/frequency have been manually overridden.
    /// When true, account sync will skip updating: LastInvoiceDateTime, PeriodType, 
    /// PeriodDays, MedianDays, ExpectedNextDateTime, ExpectedRangeStartDateTime, ExpectedRangeEndDateTime
    /// </summary>
    public bool IsManuallyOverridden { get; set; }
    
    /// <summary>
    /// User who manually overrode the billing data
    /// </summary>
    public string? OverriddenBy { get; set; }
    
    /// <summary>
    /// Date/time when billing data was manually overridden
    /// </summary>
    public DateTime? OverriddenDateTime { get; set; }
    
    // Navigation properties
    [JsonIgnore]
    public Client? Client { get; set; }
    
    [JsonIgnore]
    public ICollection<AdrJob> AdrJobs { get; set; } = new List<AdrJob>();
}
