using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents a scheduling rule for an ADR account.
/// Rules define when and how to run different job types (credential check, invoice download) for an account.
/// Multiple rules can exist per account for different job types.
/// </summary>
public class AdrAccountRule : BaseEntity
{
    /// <summary>
    /// FK to AdrAccount table
    /// </summary>
    public int AdrAccountId { get; set; }
    
    /// <summary>
    /// Type of job this rule applies to (1 = AttemptLogin/Credential Check, 2 = DownloadInvoice/Scrape)
    /// Maps to AdrRequestType enum values
    /// </summary>
    public int JobTypeId { get; set; }
    
    /// <summary>
    /// Display name for this rule (e.g., "Monthly Invoice Download", "Credential Verification")
    /// </summary>
    public string RuleName { get; set; } = string.Empty;
    
    /// <summary>
    /// Billing frequency type (Bi-Weekly, Monthly, Bi-Monthly, Quarterly, Semi-Annually, Annually)
    /// </summary>
    public string? PeriodType { get; set; }
    
    /// <summary>
    /// Standard days between invoices for this period type
    /// </summary>
    public int? PeriodDays { get; set; }
    
    /// <summary>
    /// Day of month to run (for monthly/quarterly rules), or null for calculated dates
    /// </summary>
    public int? DayOfMonth { get; set; }
    
    /// <summary>
    /// Scheduled run date for the next execution
    /// </summary>
    public DateTime? NextRunDateTime { get; set; }
    
    /// <summary>
    /// Search window start for expected invoice
    /// </summary>
    public DateTime? NextRangeStartDateTime { get; set; }
    
    /// <summary>
    /// Search window end for expected invoice
    /// </summary>
    public DateTime? NextRangeEndDateTime { get; set; }
    
    /// <summary>
    /// Number of days before NextRunDateTime to start looking (window start offset)
    /// </summary>
    public int? WindowDaysBefore { get; set; }
    
    /// <summary>
    /// Number of days after NextRunDateTime to keep looking (window end offset)
    /// </summary>
    public int? WindowDaysAfter { get; set; }
    
    /// <summary>
    /// Whether this rule is currently active and should generate jobs
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Priority for rule execution (lower = higher priority)
    /// </summary>
    public int Priority { get; set; } = 100;
    
    /// <summary>
    /// Flag indicating if this rule was manually created/overridden by a user
    /// </summary>
    public bool IsManuallyOverridden { get; set; }
    
    /// <summary>
    /// User who manually created or overrode this rule
    /// </summary>
    public string? OverriddenBy { get; set; }
    
    /// <summary>
    /// Date/time when rule was manually created or overridden
    /// </summary>
    public DateTime? OverriddenDateTime { get; set; }
    
    /// <summary>
    /// Optional notes about this rule
    /// </summary>
    public string? Notes { get; set; }
    
    // Navigation properties
    [JsonIgnore]
    public AdrAccount AdrAccount { get; set; } = null!;
    
    [JsonIgnore]
    public AdrJobType? AdrJobType { get; set; }
    
    [JsonIgnore]
    public ICollection<AdrJob> AdrJobs { get; set; } = new List<AdrJob>();
}
