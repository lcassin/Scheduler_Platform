using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.API.Extensions;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing Power BI report links.
/// Provides endpoints for CRUD operations on Power BI reports that are displayed in the navigation menu.
/// </summary>
[ApiController]
[Route("api/powerbi-reports")]
[Authorize]
public class PowerBiReportsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PowerBiReportsController> _logger;

    /// <summary>
    /// Initializes a new instance of the PowerBiReportsController.
    /// </summary>
    /// <param name="unitOfWork">The unit of work for database operations.</param>
    /// <param name="logger">The logger instance.</param>
    public PowerBiReportsController(IUnitOfWork unitOfWork, ILogger<PowerBiReportsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all active Power BI reports for display in the navigation menu.
    /// Reports are filtered by IsActive=true and IsDeleted=false, ordered by Category and DisplayOrder.
    /// </summary>
    /// <param name="category">Optional category filter (e.g., "ADR").</param>
    /// <returns>A list of active Power BI reports.</returns>
    /// <response code="200">Returns the list of active reports.</response>
    /// <response code="500">An error occurred while retrieving reports.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PowerBiReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<PowerBiReportResponse>>> GetActiveReports([FromQuery] string? category = null)
    {
        try
        {
            var allReports = await _unitOfWork.PowerBiReports.GetAllAsync();
            var activeReports = allReports
                .Where(r => !r.IsDeleted && r.IsActive)
                .Where(r => string.IsNullOrEmpty(category) || r.Category == category)
                .OrderBy(r => r.Category)
                .ThenBy(r => r.DisplayOrder)
                .Select(r => new PowerBiReportResponse
                {
                    Id = r.Id,
                    Name = r.Name,
                    Url = r.Url,
                    Description = r.Description,
                    Category = r.Category,
                    DisplayOrder = r.DisplayOrder,
                    OpenInNewTab = r.OpenInNewTab
                })
                .ToList();

            return Ok(activeReports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active Power BI reports");
            return StatusCode(500, "An error occurred while retrieving Power BI reports");
        }
    }

    /// <summary>
    /// Retrieves all Power BI reports including inactive ones for admin management.
    /// Requires Admin role.
    /// </summary>
    /// <returns>A list of all Power BI reports.</returns>
    /// <response code="200">Returns the list of all reports.</response>
    /// <response code="500">An error occurred while retrieving reports.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<PowerBiReportAdminResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<PowerBiReportAdminResponse>>> GetAllReports()
    {
        try
        {
            // Check if user is admin
            if (!User.IsAdminOrAbove())
            {
                return Forbid();
            }

            var allReports = await _unitOfWork.PowerBiReports.GetAllAsync();
            var reports = allReports
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Category)
                .ThenBy(r => r.DisplayOrder)
                .Select(r => new PowerBiReportAdminResponse
                {
                    Id = r.Id,
                    Name = r.Name,
                    Url = r.Url,
                    Description = r.Description,
                    Category = r.Category,
                    DisplayOrder = r.DisplayOrder,
                    IsActive = r.IsActive,
                    OpenInNewTab = r.OpenInNewTab,
                    CreatedDateTime = r.CreatedDateTime,
                    CreatedBy = r.CreatedBy,
                    ModifiedDateTime = r.ModifiedDateTime,
                    ModifiedBy = r.ModifiedBy
                })
                .ToList();

            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all Power BI reports");
            return StatusCode(500, "An error occurred while retrieving Power BI reports");
        }
    }

    /// <summary>
    /// Retrieves a specific Power BI report by ID.
    /// Requires Admin role.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <returns>The Power BI report with the specified ID.</returns>
    /// <response code="200">Returns the report.</response>
    /// <response code="403">User does not have permission to access this resource.</response>
    /// <response code="404">The report was not found.</response>
    /// <response code="500">An error occurred while retrieving the report.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PowerBiReportAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PowerBiReportAdminResponse>> GetReport(int id)
    {
        try
        {
            // Check if user is admin
            if (!User.IsAdminOrAbove())
            {
                return Forbid();
            }

            var report = await _unitOfWork.PowerBiReports.GetByIdAsync(id);
            if (report == null || report.IsDeleted)
            {
                return NotFound($"Power BI report with ID {id} was not found");
            }

            return Ok(new PowerBiReportAdminResponse
            {
                Id = report.Id,
                Name = report.Name,
                Url = report.Url,
                Description = report.Description,
                Category = report.Category,
                DisplayOrder = report.DisplayOrder,
                IsActive = report.IsActive,
                OpenInNewTab = report.OpenInNewTab,
                CreatedDateTime = report.CreatedDateTime,
                CreatedBy = report.CreatedBy,
                ModifiedDateTime = report.ModifiedDateTime,
                ModifiedBy = report.ModifiedBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Power BI report {ReportId}", id);
            return StatusCode(500, "An error occurred while retrieving the Power BI report");
        }
    }

    /// <summary>
    /// Creates a new Power BI report link.
    /// Requires Admin role.
    /// </summary>
    /// <param name="request">The report creation request.</param>
    /// <returns>The created report.</returns>
    /// <response code="201">Returns the newly created report.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="403">User does not have permission to create reports.</response>
    /// <response code="500">An error occurred while creating the report.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PowerBiReportAdminResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PowerBiReportAdminResponse>> CreateReport([FromBody] CreatePowerBiReportRequest request)
    {
        try
        {
            // Check if user is admin
            if (!User.IsAdminOrAbove())
            {
                return Forbid();
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Report name is required");
            }
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest("Report URL is required");
            }

            var now = DateTime.UtcNow;
            var createdBy = User.Identity?.Name ?? "System";

            var report = new PowerBiReport
            {
                Name = request.Name.Trim(),
                Url = request.Url.Trim(),
                Description = request.Description?.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? "ADR" : request.Category.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                OpenInNewTab = request.OpenInNewTab,
                CreatedDateTime = now,
                CreatedBy = createdBy,
                ModifiedDateTime = now,
                ModifiedBy = createdBy,
                IsDeleted = false
            };

            await _unitOfWork.PowerBiReports.AddAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Power BI report '{ReportName}' created by {User}", report.Name, createdBy);

            var response = new PowerBiReportAdminResponse
            {
                Id = report.Id,
                Name = report.Name,
                Url = report.Url,
                Description = report.Description,
                Category = report.Category,
                DisplayOrder = report.DisplayOrder,
                IsActive = report.IsActive,
                OpenInNewTab = report.OpenInNewTab,
                CreatedDateTime = report.CreatedDateTime,
                CreatedBy = report.CreatedBy,
                ModifiedDateTime = report.ModifiedDateTime,
                ModifiedBy = report.ModifiedBy
            };

            return CreatedAtAction(nameof(GetReport), new { id = report.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Power BI report");
            return StatusCode(500, "An error occurred while creating the Power BI report");
        }
    }

    /// <summary>
    /// Updates an existing Power BI report link.
    /// Requires Admin role.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <param name="request">The report update request.</param>
    /// <returns>The updated report.</returns>
    /// <response code="200">Returns the updated report.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="403">User does not have permission to update reports.</response>
    /// <response code="404">The report was not found.</response>
    /// <response code="500">An error occurred while updating the report.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PowerBiReportAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PowerBiReportAdminResponse>> UpdateReport(int id, [FromBody] UpdatePowerBiReportRequest request)
    {
        try
        {
            // Check if user is admin
            if (!User.IsAdminOrAbove())
            {
                return Forbid();
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Report name is required");
            }
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest("Report URL is required");
            }

            var report = await _unitOfWork.PowerBiReports.GetByIdAsync(id);
            if (report == null || report.IsDeleted)
            {
                return NotFound($"Power BI report with ID {id} was not found");
            }

            var modifiedBy = User.Identity?.Name ?? "System";

            report.Name = request.Name.Trim();
            report.Url = request.Url.Trim();
            report.Description = request.Description?.Trim();
            report.Category = string.IsNullOrWhiteSpace(request.Category) ? "ADR" : request.Category.Trim();
            report.DisplayOrder = request.DisplayOrder;
            report.IsActive = request.IsActive;
            report.OpenInNewTab = request.OpenInNewTab;
            report.ModifiedDateTime = DateTime.UtcNow;
            report.ModifiedBy = modifiedBy;

            await _unitOfWork.PowerBiReports.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Power BI report '{ReportName}' (ID: {ReportId}) updated by {User}", report.Name, id, modifiedBy);

            return Ok(new PowerBiReportAdminResponse
            {
                Id = report.Id,
                Name = report.Name,
                Url = report.Url,
                Description = report.Description,
                Category = report.Category,
                DisplayOrder = report.DisplayOrder,
                IsActive = report.IsActive,
                OpenInNewTab = report.OpenInNewTab,
                CreatedDateTime = report.CreatedDateTime,
                CreatedBy = report.CreatedBy,
                ModifiedDateTime = report.ModifiedDateTime,
                ModifiedBy = report.ModifiedBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Power BI report {ReportId}", id);
            return StatusCode(500, "An error occurred while updating the Power BI report");
        }
    }

    /// <summary>
    /// Deletes a Power BI report link (soft delete).
    /// Requires Admin role.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The report was successfully deleted.</response>
    /// <response code="403">User does not have permission to delete reports.</response>
    /// <response code="404">The report was not found.</response>
    /// <response code="500">An error occurred while deleting the report.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteReport(int id)
    {
        try
        {
            // Check if user is admin
            if (!User.IsAdminOrAbove())
            {
                return Forbid();
            }

            var report = await _unitOfWork.PowerBiReports.GetByIdAsync(id);
            if (report == null || report.IsDeleted)
            {
                return NotFound($"Power BI report with ID {id} was not found");
            }

            var modifiedBy = User.Identity?.Name ?? "System";

            // Soft delete
            report.IsDeleted = true;
            report.ModifiedDateTime = DateTime.UtcNow;
            report.ModifiedBy = modifiedBy;

            await _unitOfWork.PowerBiReports.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Power BI report '{ReportName}' (ID: {ReportId}) deleted by {User}", report.Name, id, modifiedBy);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Power BI report {ReportId}", id);
            return StatusCode(500, "An error occurred while deleting the Power BI report");
        }
    }
}

/// <summary>
/// Response model for Power BI reports displayed in the navigation menu.
/// </summary>
public class PowerBiReportResponse
{
    /// <summary>The unique identifier of the report.</summary>
    public int Id { get; set; }
    
    /// <summary>The display name of the report.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>The URL to the Power BI report.</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>Optional description of the report.</summary>
    public string? Description { get; set; }
    
    /// <summary>The category/group for organizing reports.</summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>Display order within the category.</summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>Whether to open the report in a new tab.</summary>
    public bool OpenInNewTab { get; set; }
}

/// <summary>
/// Response model for Power BI reports in the admin management page.
/// </summary>
public class PowerBiReportAdminResponse : PowerBiReportResponse
{
    /// <summary>Whether the report is currently active.</summary>
    public bool IsActive { get; set; }
    
    /// <summary>When the report was created.</summary>
    public DateTime CreatedDateTime { get; set; }
    
    /// <summary>Who created the report.</summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    /// <summary>When the report was last modified.</summary>
    public DateTime ModifiedDateTime { get; set; }
    
    /// <summary>Who last modified the report.</summary>
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request model for creating a new Power BI report.
/// </summary>
public class CreatePowerBiReportRequest
{
    /// <summary>The display name of the report (required).</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>The URL to the Power BI report (required).</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>Optional description of the report.</summary>
    public string? Description { get; set; }
    
    /// <summary>The category/group for organizing reports (defaults to "ADR").</summary>
    public string? Category { get; set; }
    
    /// <summary>Display order within the category (defaults to 0).</summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>Whether the report is active (defaults to true).</summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>Whether to open the report in a new tab (defaults to true).</summary>
    public bool OpenInNewTab { get; set; } = true;
}

/// <summary>
/// Request model for updating an existing Power BI report.
/// </summary>
public class UpdatePowerBiReportRequest
{
    /// <summary>The display name of the report (required).</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>The URL to the Power BI report (required).</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>Optional description of the report.</summary>
    public string? Description { get; set; }
    
    /// <summary>The category/group for organizing reports.</summary>
    public string? Category { get; set; }
    
    /// <summary>Display order within the category.</summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>Whether the report is active.</summary>
    public bool IsActive { get; set; }
    
    /// <summary>Whether to open the report in a new tab.</summary>
    public bool OpenInNewTab { get; set; }
}
