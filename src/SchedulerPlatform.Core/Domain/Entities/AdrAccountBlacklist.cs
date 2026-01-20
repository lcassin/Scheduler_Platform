using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Stores blacklist entries for ADR accounts that should be excluded from job creation.
/// Allows excluding specific vendors, accounts, or credentials from the orchestration process.
/// Only Admin and Super Admin users can modify blacklist entries.
/// </summary>
public class AdrAccountBlacklist : BaseEntity
{
    /// <summary>
    /// Primary vendor code to exclude. If set, all accounts for this vendor are excluded.
    /// Can be used alone or in combination with other fields for more specific exclusions.
    /// </summary>
    public string? PrimaryVendorCode { get; set; }
    
    /// <summary>
    /// Master vendor code to exclude. If set, all accounts under this master vendor are excluded.
    /// Can be used alone or in combination with other fields for more specific exclusions.
    /// </summary>
    public string? MasterVendorCode { get; set; }
    
    /// <summary>
    /// VM Account ID to exclude. If set, this specific account is excluded.
    /// Can be used alone or in combination with VendorCode.
    /// </summary>
    public long? VMAccountId { get; set; }
    
    /// <summary>
    /// VM Account Number to exclude. If set, accounts with this number are excluded.
    /// Can be used alone or in combination with other fields.
    /// </summary>
    public string? VMAccountNumber { get; set; }
    
    /// <summary>
    /// Credential ID to exclude. If set, all accounts using this credential are excluded.
    /// Can be used alone or in combination with other fields.
    /// </summary>
    public int? CredentialId { get; set; }
    
    /// <summary>
    /// Type of exclusion: "All" (exclude from all job types), "CredentialCheck" (exclude from credential verification only),
    /// "Download" (exclude from invoice download only).
    /// Default: "All"
    /// </summary>
    public string ExclusionType { get; set; } = "All";
    
    /// <summary>
    /// Reason for blacklisting this vendor/account/credential.
    /// Required for audit purposes.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this blacklist entry is currently active.
    /// Allows temporarily disabling exclusions without deleting them.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Optional start date for the blacklist entry.
    /// If set, the exclusion only applies after this date.
    /// </summary>
    public DateTime? EffectiveStartDate { get; set; }
    
    /// <summary>
    /// Optional end date for the blacklist entry.
    /// If set, the exclusion automatically expires after this date.
    /// </summary>
    public DateTime? EffectiveEndDate { get; set; }
    
    /// <summary>
    /// User who created or last modified this blacklist entry.
    /// </summary>
    public string? BlacklistedBy { get; set; }
    
    /// <summary>
    /// Date/time when this entry was blacklisted.
    /// </summary>
    public DateTime? BlacklistedDateTime { get; set; }
    
    /// <summary>
    /// Optional notes about this blacklist entry.
    /// </summary>
    public string? Notes { get; set; }
}
