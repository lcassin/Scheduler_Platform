using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents an individual execution attempt for an ADR job.
/// Records login checks, scrape requests, and status polls.
/// </summary>
public class AdrJobExecution : BaseEntity
{
    /// <summary>
    /// FK to AdrJob table
    /// </summary>
    public int AdrJobId { get; set; }
    
    /// <summary>
    /// Type of ADR request (1 = Attempt Login, 2 = Download Invoice)
    /// </summary>
    public int AdrRequestTypeId { get; set; }
    
    /// <summary>
    /// Execution start time
    /// </summary>
    public DateTime StartDateTime { get; set; }
    
    /// <summary>
    /// Execution end time
    /// </summary>
    public DateTime? EndDateTime { get; set; }
    
    /// <summary>
    /// ADR status ID returned from API
    /// </summary>
    public int? AdrStatusId { get; set; }
    
    /// <summary>
    /// ADR status description
    /// </summary>
    public string? AdrStatusDescription { get; set; }
    
    /// <summary>
    /// Whether this status indicates an error
    /// </summary>
    public bool IsError { get; set; }
    
    /// <summary>
    /// Whether this status is final (complete or needs review)
    /// </summary>
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// Index ID returned from ADR API
    /// </summary>
    public long? AdrIndexId { get; set; }
    
    /// <summary>
    /// HTTP status code from API response
    /// </summary>
    public int? HttpStatusCode { get; set; }
    
    /// <summary>
    /// Success or failure
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Full API response (JSON)
    /// </summary>
    public string? ApiResponse { get; set; }
    
    /// <summary>
    /// Request payload sent to API (JSON)
    /// </summary>
    public string? RequestPayload { get; set; }
    
    // Navigation property
    [JsonIgnore]
    public AdrJob AdrJob { get; set; } = null!;
}
