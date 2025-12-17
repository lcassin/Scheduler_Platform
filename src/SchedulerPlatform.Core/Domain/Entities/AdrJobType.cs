using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents a type of ADR job that can be scheduled and executed.
/// This table replaces the hardcoded AdrRequestType enum, allowing job types
/// to be managed via the admin UI.
/// </summary>
public class AdrJobType : BaseEntity
{
    /// <summary>
    /// Short code for the job type (e.g., "CREDENTIAL_CHECK", "DOWNLOAD_INVOICE")
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the job type (e.g., "Credential Check", "Download Invoice")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of what this job type does
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The URL endpoint to call when executing jobs of this type.
    /// This allows different job types to call different downstream services.
    /// </summary>
    public string? EndpointUrl { get; set; }
    
    /// <summary>
    /// The AdrRequestTypeId to send to the downstream ADR API (1 = AttemptLogin, 2 = DownloadInvoice)
    /// This maps to the existing ADR API contract
    /// </summary>
    public int AdrRequestTypeId { get; set; }
    
    /// <summary>
    /// Whether this job type is currently active and can be used for new rules/jobs
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Display order for UI sorting
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// Optional notes about this job type
    /// </summary>
    public string? Notes { get; set; }
    
    // Navigation properties
    [JsonIgnore]
    public ICollection<AdrAccountRule> AdrAccountRules { get; set; } = new List<AdrAccountRule>();
}
