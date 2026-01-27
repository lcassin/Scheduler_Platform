namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service interface for managing Power BI report links.
/// </summary>
public interface IPowerBiReportService
{
    /// <summary>
    /// Gets all active Power BI reports for display in the navigation menu.
    /// </summary>
    /// <param name="category">Optional category filter (e.g., "ADR").</param>
    /// <returns>A list of active Power BI reports.</returns>
    Task<List<PowerBiReportDto>> GetActiveReportsAsync(string? category = null);
    
    /// <summary>
    /// Gets all Power BI reports including inactive ones for admin management.
    /// </summary>
    /// <returns>A list of all Power BI reports.</returns>
    Task<List<PowerBiReportAdminDto>> GetAllReportsAsync();
    
    /// <summary>
    /// Gets a specific Power BI report by ID.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <returns>The Power BI report, or null if not found.</returns>
    Task<PowerBiReportAdminDto?> GetReportAsync(int id);
    
    /// <summary>
    /// Creates a new Power BI report link.
    /// </summary>
    /// <param name="request">The report creation request.</param>
    /// <returns>The created report.</returns>
    Task<PowerBiReportAdminDto?> CreateReportAsync(CreatePowerBiReportDto request);
    
    /// <summary>
    /// Updates an existing Power BI report link.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <param name="request">The report update request.</param>
    /// <returns>The updated report.</returns>
    Task<PowerBiReportAdminDto?> UpdateReportAsync(int id, UpdatePowerBiReportDto request);
    
    /// <summary>
    /// Deletes a Power BI report link.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <returns>True if deleted successfully, false otherwise.</returns>
    Task<bool> DeleteReportAsync(int id);
}

/// <summary>
/// DTO for Power BI reports displayed in the navigation menu.
/// </summary>
public class PowerBiReportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool OpenInNewTab { get; set; }
}

/// <summary>
/// DTO for Power BI reports in the admin management page.
/// </summary>
public class PowerBiReportAdminDto : PowerBiReportDto
{
    public bool IsActive { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedDateTime { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating a new Power BI report.
/// </summary>
public class CreatePowerBiReportDto
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool OpenInNewTab { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing Power BI report.
/// </summary>
public class UpdatePowerBiReportDto
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public bool OpenInNewTab { get; set; }
}
