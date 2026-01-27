namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Represents a Power BI report link that can be displayed in the navigation menu.
/// This allows administrators to configure report links without code changes.
/// </summary>
public class PowerBiReport : BaseEntity
{
    /// <summary>
    /// Display name for the report (shown in the navigation menu)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The full URL to the Power BI report
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description of what the report shows
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Category/group for organizing reports (e.g., "ADR", "Scheduling")
    /// </summary>
    public string Category { get; set; } = "ADR";
    
    /// <summary>
    /// Display order within the category (lower numbers appear first)
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// Whether this report link is currently active and should be shown
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether to open the report in a new tab/window
    /// </summary>
    public bool OpenInNewTab { get; set; } = true;
}
