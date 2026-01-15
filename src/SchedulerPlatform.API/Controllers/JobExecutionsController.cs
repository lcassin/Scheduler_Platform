using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using SchedulerPlatform.API.Extensions;
using SchedulerPlatform.API.Services;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing job execution history and operations.
/// Provides endpoints for retrieving, exporting, and cancelling job executions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobExecutionsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobExecutionsController> _logger;
    private readonly IScheduler _scheduler;
    private readonly ISchedulerService _schedulerService;

    public JobExecutionsController(
        IUnitOfWork unitOfWork, 
        ILogger<JobExecutionsController> logger, 
        IScheduler scheduler,
        ISchedulerService schedulerService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _scheduler = scheduler;
        _schedulerService = schedulerService;
    }

    /// <summary>
    /// Retrieves a list of job executions with optional filtering.
    /// </summary>
    /// <param name="scheduleId">Optional schedule ID to filter executions.</param>
    /// <param name="status">Optional job status to filter executions.</param>
    /// <returns>A list of job executions matching the filter criteria.</returns>
    /// <response code="200">Returns the list of job executions.</response>
    /// <response code="500">An error occurred while retrieving job executions.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JobExecution>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobExecution>>> GetJobExecutions(
        [FromQuery] int? scheduleId = null,
        [FromQuery] JobStatus? status = null)
    {
        try
        {
            IEnumerable<JobExecution> executions;

            if (scheduleId.HasValue)
            {
                executions = await _unitOfWork.JobExecutions.GetByScheduleIdAsync(scheduleId.Value);
            }
            else if (status.HasValue)
            {
                executions = await _unitOfWork.JobExecutions.GetByStatusAsync(status.Value);
            }
            else
            {
                executions = await _unitOfWork.JobExecutions.GetAllAsync();
            }

            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job executions");
            return StatusCode(500, "An error occurred while retrieving job executions");
        }
    }

    /// <summary>
    /// Retrieves a specific job execution by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the job execution.</param>
    /// <returns>The job execution with the specified ID.</returns>
    /// <response code="200">Returns the requested job execution.</response>
    /// <response code="404">Job execution with the specified ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the job execution.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(JobExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobExecution>> GetJobExecution(int id)
    {
        try
        {
            var execution = await _unitOfWork.JobExecutions.GetByIdAsync(id);
            if (execution == null)
            {
                return NotFound();
            }

            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job execution {ExecutionId}", id);
            return StatusCode(500, "An error occurred while retrieving the job execution");
        }
    }

    /// <summary>
    /// Retrieves the most recent job execution for a specific schedule.
    /// </summary>
    /// <param name="scheduleId">The unique identifier of the schedule.</param>
    /// <returns>The latest job execution for the specified schedule.</returns>
    /// <response code="200">Returns the latest job execution.</response>
    /// <response code="404">No executions found for the specified schedule.</response>
    /// <response code="500">An error occurred while retrieving the latest execution.</response>
    [HttpGet("schedule/{scheduleId}/latest")]
    [ProducesResponseType(typeof(JobExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobExecution>> GetLatestExecution(int scheduleId)
    {
        try
        {
            var execution = await _unitOfWork.JobExecutions.GetLastExecutionAsync(scheduleId);
            if (execution == null)
            {
                return NotFound();
            }

            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest execution for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, "An error occurred while retrieving the latest execution");
        }
    }

    /// <summary>
    /// Retrieves all failed job executions for a specific schedule.
    /// </summary>
    /// <param name="scheduleId">The unique identifier of the schedule.</param>
    /// <returns>A list of failed job executions for the specified schedule.</returns>
    /// <response code="200">Returns the list of failed executions.</response>
    /// <response code="500">An error occurred while retrieving failed executions.</response>
    [HttpGet("schedule/{scheduleId}/failed")]
    [ProducesResponseType(typeof(IEnumerable<JobExecution>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobExecution>>> GetFailedExecutions(int scheduleId)
    {
        try
        {
            var executions = await _unitOfWork.JobExecutions.GetFailedExecutionsAsync(scheduleId);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failed executions for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, "An error occurred while retrieving failed executions");
        }
    }

    /// <summary>
    /// Exports job executions to Excel or CSV format.
    /// </summary>
    /// <param name="scheduleId">Optional schedule ID to filter executions.</param>
    /// <param name="status">Optional job status to filter executions.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="format">Export format: 'excel' (default) or 'csv'.</param>
    /// <returns>A file download containing the exported job executions.</returns>
    /// <response code="200">Returns the exported file.</response>
    /// <response code="500">An error occurred while exporting job executions.</response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportJobExecutions(
        [FromQuery] int? scheduleId = null,
        [FromQuery] JobStatus? status = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string format = "excel")
    {
        try
        {
            IEnumerable<JobExecution> executions;
            
            if (scheduleId.HasValue || status.HasValue || startDate.HasValue || endDate.HasValue)
            {
                executions = await _unitOfWork.JobExecutions.GetByFiltersAsync(scheduleId, status, startDate, endDate);
            }
            else
            {
                executions = await _unitOfWork.JobExecutions.GetAllAsync();
            }

            var headers = new[] {
                "Id", "ScheduleId", "ScheduleName", "StartDateTime", "EndDateTime",
                "Duration", "Status", "RetryCount", "ErrorMessage"
            };

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csvBytes = ExcelExportHelper.CreateCsvExport(
                    string.Join(",", headers),
                    executions,
                    e =>
                    {
                        var duration = e.EndDateTime.HasValue
                            ? (e.EndDateTime.Value - e.StartDateTime).TotalSeconds
                            : (double?)null;
                        return string.Join(",",
                            e.Id,
                            e.ScheduleId,
                            ExcelExportHelper.CsvEscape(e.Schedule.Name),
                            e.StartDateTime.ToString("o"),
                            e.EndDateTime?.ToString("o") ?? "",
                            duration?.ToString(CultureInfo.InvariantCulture) ?? "",
                            e.Status,
                            e.RetryCount,
                            ExcelExportHelper.CsvEscape(e.ErrorMessage)
                        );
                    });
                return File(csvBytes, "text/csv", $"executions_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            // Excel format using centralized helper
            var excelBytes = ExcelExportHelper.CreateExcelExport(
                "JobExecutions",
                "JobExecutionsTable",
                headers,
                executions,
                e =>
                {
                    var duration = e.EndDateTime.HasValue
                        ? (e.EndDateTime.Value - e.StartDateTime).ToString()
                        : null;
                    return new object?[]
                    {
                        e.Id,
                        e.ScheduleId,
                        e.Schedule.Name,
                        e.StartDateTime,
                        e.EndDateTime,
                        duration,
                        e.Status.ToString(),
                        e.RetryCount,
                        e.ErrorMessage
                    };
                });
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"executions_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting job executions");
            return StatusCode(500, "An error occurred while exporting job executions");
        }
    }

    private static string CsvEscape(string? value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    /// <summary>
    /// Cancels a running job execution.
    /// Only jobs with a 'Running' status can be cancelled.
    /// </summary>
    /// <param name="id">The unique identifier of the job execution to cancel.</param>
    /// <returns>A success message if the job was cancelled.</returns>
    /// <response code="200">Job execution was cancelled successfully.</response>
    /// <response code="400">Job is not in a running state and cannot be cancelled.</response>
    /// <response code="404">Job execution or associated schedule was not found.</response>
    /// <response code="500">An error occurred while cancelling the job execution.</response>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelJobExecution(int id)
    {
        try
        {
            var execution = await _unitOfWork.JobExecutions.GetByIdAsync(id);
            if (execution == null)
            {
                return NotFound();
            }
            
            if (execution.Status != JobStatus.Running)
            {
                return BadRequest("Only running jobs can be cancelled");
            }
            
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(execution.ScheduleId);
            if (schedule == null)
            {
                return NotFound("Schedule not found");
            }
            
            var jobKey = new JobKey($"Job_{schedule.Id}", $"Group_{schedule.ClientId}");
            
            var currentlyExecuting = await _scheduler.GetCurrentlyExecutingJobs();
            var isExecuting = currentlyExecuting.Any(j => j.JobDetail.Key.Equals(jobKey));
            
            if (isExecuting)
            {
                await _scheduler.Interrupt(jobKey);
                _logger.LogInformation("Interrupted job {JobKey} for execution {ExecutionId}", jobKey, id);
            }
            
            execution.Status = JobStatus.Cancelled;
            execution.EndDateTime = DateTime.UtcNow;
            execution.ErrorMessage = "Job was manually cancelled by user";
            execution.CancelledBy = User.Identity?.Name ?? "Unknown";
            
            await _unitOfWork.JobExecutions.UpdateAsync(execution);
            await _unitOfWork.SaveChangesAsync();
            
            return Ok(new { message = "Job execution cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job execution {ExecutionId}", id);
            return StatusCode(500, "An error occurred while cancelling the job execution");
        }
    }

    /// <summary>
    /// Retries a failed or cancelled job execution by triggering the associated schedule.
    /// Only jobs with 'Failed' or 'Cancelled' status can be retried.
    /// </summary>
    /// <param name="id">The unique identifier of the job execution to retry.</param>
    /// <returns>A success message if the job was triggered for retry.</returns>
    /// <response code="200">Job was triggered for retry successfully.</response>
    /// <response code="400">Job is not in a failed or cancelled state and cannot be retried.</response>
    /// <response code="404">Job execution or associated schedule was not found.</response>
    /// <response code="500">An error occurred while retrying the job execution.</response>
    [HttpPost("{id}/retry")]
    [Authorize(Policy = "Schedules.Execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RetryJobExecution(int id)
    {
        try
        {
            var execution = await _unitOfWork.JobExecutions.GetByIdAsync(id);
            if (execution == null)
            {
                return NotFound("Job execution not found");
            }
            
            if (execution.Status != JobStatus.Failed && execution.Status != JobStatus.Cancelled)
            {
                return BadRequest("Only failed or cancelled jobs can be retried");
            }
            
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(execution.ScheduleId);
            if (schedule == null)
            {
                return NotFound("Associated schedule not found");
            }
            
            // Check authorization - use extension methods for cleaner authorization checks
            var isAdmin = User.IsAdminOrAbove();
            
            // System schedules can be retried by Admin or Super Admin; regular schedules require matching client
            if (schedule.IsSystemSchedule && !isAdmin)
            {
                _logger.LogWarning("Non-admin user attempted to retry system schedule execution {ExecutionId}", id);
                return Forbid();
            }
            
            if (!schedule.IsSystemSchedule && !User.CanAccessClient(schedule.ClientId))
            {
                _logger.LogWarning(
                    "Unauthorized retry attempt: User with ClientId {UserClientId} attempted to retry execution {ExecutionId} for Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    User.GetClientId(), id, schedule.Id, schedule.ClientId);
                return Forbid();
            }
            
            // Ensure the job exists in Quartz before triggering
            var jobKey = new JobKey($"Job_{schedule.Id}", $"Group_{schedule.ClientId}");
            if (!await _scheduler.CheckExists(jobKey))
            {
                _logger.LogWarning("Job {JobKey} not found in Quartz scheduler for schedule {ScheduleId}, attempting to register it now", 
                    jobKey, schedule.Id);
                
                try
                {
                    await _schedulerService.ScheduleJob(schedule);
                    _logger.LogInformation("Successfully registered job {JobKey} for schedule {ScheduleId}", 
                        jobKey, schedule.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register job {JobKey} for schedule {ScheduleId}", 
                        jobKey, schedule.Id);
                    return StatusCode(500, $"Failed to register job before retrying: {ex.Message}");
                }
            }
            
            // Trigger the job
            await _schedulerService.TriggerJobNow(schedule.Id, schedule.ClientId, User.Identity?.Name ?? "Manual Retry");
            
            _logger.LogInformation("Retry triggered for execution {ExecutionId}, schedule {ScheduleId} by user {User}", 
                id, schedule.Id, User.Identity?.Name ?? "Unknown");
            
            return Ok(new { message = "Job retry triggered successfully", scheduleId = schedule.Id, scheduleName = schedule.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying job execution {ExecutionId}", id);
            return StatusCode(500, "An error occurred while retrying the job execution");
        }
    }
}
