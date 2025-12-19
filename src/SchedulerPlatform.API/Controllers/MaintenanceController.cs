using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.API.Services;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for system maintenance operations.
/// Provides endpoints for data archival, log cleanup, and other maintenance tasks.
/// These endpoints can be called by scheduled jobs or manually by administrators.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceController : ControllerBase
{
    private readonly DataArchivalService _archivalService;
    private readonly ILogger<MaintenanceController> _logger;

    /// <summary>
    /// Initializes a new instance of the MaintenanceController.
    /// </summary>
    /// <param name="archivalService">The data archival service.</param>
    /// <param name="logger">The logger instance.</param>
    public MaintenanceController(
        DataArchivalService archivalService,
        ILogger<MaintenanceController> logger)
    {
        _archivalService = archivalService;
        _logger = logger;
    }

    /// <summary>
    /// Runs the complete maintenance process including data archival, archive purge, and log cleanup.
    /// This endpoint can be called by a scheduled job to perform nightly maintenance.
    /// </summary>
    /// <remarks>
    /// The maintenance process includes:
    /// 1. **Data Archival**: Moves old records to archive tables based on retention settings
    ///    - AdrJobs older than JobRetentionMonths (default: 12 months)
    ///    - AdrJobExecutions older than JobExecutionRetentionMonths (default: 12 months)
    ///    - AuditLogs older than AuditLogRetentionDays (default: 90 days)
    ///    - JobExecutions (schedule executions) older than JobExecutionRetentionMonths
    /// 2. **Archive Purge**: Permanently deletes archive records older than ArchiveRetentionYears (default: 7 years)
    /// 3. **Log Cleanup**: Deletes log files older than LogRetentionDays (default: 30 days)
    /// 
    /// All retention periods are configurable via the ADR Configuration page.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of the maintenance operations performed.</returns>
    /// <response code="200">Maintenance completed successfully.</response>
    /// <response code="500">An error occurred during maintenance.</response>
    [HttpPost("run")]
    [Authorize(Policy = "Schedules.Execute")]
    [ProducesResponseType(typeof(MaintenanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MaintenanceResult>> RunMaintenance(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Maintenance triggered via API by user {User}", User.Identity?.Name ?? "Unknown");

        try
        {
            var result = await _archivalService.RunMaintenanceAsync(cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Maintenance completed with errors: {Error}", result.ErrorMessage);
                return StatusCode(500, result);
            }

            _logger.LogInformation(
                "Maintenance completed successfully. Archived: {Archived}, Purged: {Purged}, Log files: {LogFiles}",
                result.AdrJobsArchived + result.AdrJobExecutionsArchived + result.AuditLogsArchived + result.ScheduleExecutionsArchived,
                result.AdrJobArchivesPurged + result.AdrJobExecutionArchivesPurged + result.AuditLogArchivesPurged + result.JobExecutionArchivesPurged,
                result.LogFilesDeleted);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running maintenance");
            return StatusCode(500, new MaintenanceResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets the current maintenance configuration settings.
    /// </summary>
    /// <returns>The current retention and archival settings.</returns>
    /// <response code="200">Returns the maintenance configuration.</response>
    [HttpGet("config")]
    [Authorize(Policy = "Schedules.Read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetMaintenanceConfig()
    {
        // This would need to be implemented to return the current AdrConfiguration settings
        // For now, return a placeholder that describes the configurable settings
        return Ok(new
        {
            message = "Maintenance settings are configured via the ADR Configuration page",
            configurableSettings = new[]
            {
                "JobRetentionMonths - Months to keep AdrJob records before archiving (default: 12)",
                "JobExecutionRetentionMonths - Months to keep execution records before archiving (default: 12)",
                "AuditLogRetentionDays - Days to keep audit logs before archiving (default: 90)",
                "ArchiveRetentionYears - Years to keep archived records before permanent deletion (default: 7)",
                "LogRetentionDays - Days to keep log files before deletion (default: 30)",
                "IsArchivalEnabled - Whether archival is enabled (default: true)",
                "ArchivalBatchSize - Records to process per batch (default: 5000)"
            }
        });
    }
}
