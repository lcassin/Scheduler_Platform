using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Drawing.Charts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Quartz;
using SchedulerPlatform.API.Models;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing job schedules in the Scheduler Platform.
/// Provides endpoints for CRUD operations, triggering jobs, and managing schedule execution.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<SchedulesController> _logger;
    private readonly IScheduler _scheduler;

    public SchedulesController(
        IUnitOfWork unitOfWork, 
        ISchedulerService schedulerService,
        ILogger<SchedulesController> logger,
        IScheduler scheduler)
    {
        _unitOfWork = unitOfWork;
        _schedulerService = schedulerService;
        _logger = logger;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Retrieves a list of schedules with optional filtering and pagination.
    /// </summary>
    /// <param name="clientId">Optional client ID to filter schedules by client.</param>
    /// <param name="searchTerm">Optional search term to filter schedules by name or description.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <param name="paginated">Whether to return paginated results (default: true).</param>
    /// <param name="startDate">Optional start date for filtering schedules by next run date.</param>
    /// <param name="endDate">Optional end date for filtering schedules by next run date.</param>
    /// <param name="isEnabled">Optional filter for enabled/disabled schedules.</param>
    /// <returns>A paginated list of schedules or all schedules matching the criteria.</returns>
    /// <response code="200">Returns the list of schedules.</response>
    /// <response code="500">An error occurred while retrieving schedules.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetSchedules(
        [FromQuery] int? clientId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool paginated = true,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] bool? isEnabled = null)
    {
        try
        {
            if (paginated)
            {
                var (items, totalCount) = await _unitOfWork.Schedules.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    clientId, 
                    searchTerm,
                    isEnabled);

                return Ok(new 
                {
                    items = items,
                    totalCount = totalCount,
                    pageNumber = pageNumber,
                    pageSize = pageSize
                });
            }
            else
            {
                IEnumerable<Schedule> schedules;
                
                if (startDate.HasValue && endDate.HasValue)
                {
                    schedules = await _unitOfWork.Schedules.GetSchedulesForCalendarAsync(
                        startDate.Value, 
                        endDate.Value, 
                        clientId,
                        maxPerDay: 1000); // High limit for non-calendar views
                }
                else
                {
                    schedules = clientId.HasValue
                        ? await _unitOfWork.Schedules.GetByClientIdAsync(clientId.Value)
                        : await _unitOfWork.Schedules.GetAllAsync();
                }

                return Ok(schedules);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules");
            return StatusCode(500, "An error occurred while retrieving schedules");
        }
    }

    /// <summary>
    /// Retrieves a specific schedule by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the schedule.</param>
    /// <returns>The schedule with the specified ID.</returns>
    /// <response code="200">Returns the requested schedule.</response>
    /// <response code="403">User is not authorized to access this schedule.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the schedule.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Schedule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Schedule>> GetSchedule(int id)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdWithNotificationSettingsAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }

            var userClientId = User.FindFirst("user_client_id")?.Value;
            var isSystemAdminValue = User.FindFirst("is_system_admin")?.Value;
            var isSystemAdmin = string.Equals(isSystemAdminValue, "True", StringComparison.OrdinalIgnoreCase) || isSystemAdminValue == "1";
            
            if (!isSystemAdmin && schedule.ClientId.ToString() != userClientId)
            {
                _logger.LogWarning(
                    "Unauthorized access attempt: User with ClientId {UserClientId} attempted to access Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    userClientId, id, schedule.ClientId);
                return Forbid();
            }

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while retrieving the schedule");
        }
    }

    /// <summary>
    /// Creates a new schedule with the specified configuration.
    /// </summary>
    /// <param name="schedule">The schedule object containing the configuration details.</param>
    /// <returns>The newly created schedule.</returns>
    /// <response code="201">Schedule was created successfully.</response>
    /// <response code="400">Invalid schedule data provided.</response>
    /// <response code="500">An error occurred while creating the schedule.</response>
    [HttpPost]
    [Authorize(Policy = "Schedules.Create")]
    [ProducesResponseType(typeof(Schedule), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Schedule>> CreateSchedule([FromBody] Schedule schedule)
    {
        try
        {
            var notificationSetting = schedule.NotificationSetting;
            schedule.NotificationSetting = null;

            var now = DateTime.UtcNow;
            var createdBy = User.Identity?.Name ?? "System";
            schedule.CreatedDateTime = now;
            schedule.CreatedBy = createdBy;
            schedule.ModifiedDateTime = now;
            schedule.ModifiedBy = createdBy;

            if (!schedule.IsDeleted && schedule.IsEnabled && !schedule.NextRunDateTime.HasValue && !string.IsNullOrWhiteSpace(schedule.CronExpression))
            {
                try
                {
                    var trigger = TriggerBuilder.Create()
                        .WithCronSchedule(schedule.CronExpression)
                        .Build();
                    schedule.NextRunDateTime = trigger.GetNextFireTimeUtc()?.DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate NextRunDateTime for schedule {ScheduleId}", schedule.Id);
                }
            }

            await _unitOfWork.Schedules.AddAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            if (notificationSetting != null)
            {
                notificationSetting.ScheduleId = schedule.Id;
                notificationSetting.CreatedDateTime = now;
                notificationSetting.CreatedBy = createdBy;
                notificationSetting.ModifiedDateTime = now;
                notificationSetting.ModifiedBy = createdBy;
                
                await _unitOfWork.NotificationSettings.AddAsync(notificationSetting);
                await _unitOfWork.SaveChangesAsync();
                
                schedule.NotificationSetting = notificationSetting;
            }

            if (schedule.IsEnabled)
            {
                await _schedulerService.ScheduleJob(schedule);
            }

            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule");
            return StatusCode(500, "An error occurred while creating the schedule");
        }
    }

    /// <summary>
    /// Updates an existing schedule with the specified configuration.
    /// System schedules cannot be modified.
    /// </summary>
    /// <param name="id">The unique identifier of the schedule to update.</param>
    /// <param name="schedule">The updated schedule object.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Schedule was updated successfully.</response>
    /// <response code="400">Schedule ID mismatch or invalid data provided.</response>
    /// <response code="403">Cannot modify system schedules.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while updating the schedule.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "Schedules.Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] Schedule schedule)
    {
        try
        {
            if (id != schedule.Id)
            {
                return BadRequest("Schedule ID mismatch");
            }

            var existingSchedule = await _unitOfWork.Schedules.GetByIdWithNotificationSettingsAsync(id);
            if (existingSchedule == null)
            {
                return NotFound();
            }

            if (existingSchedule.IsSystemSchedule)
            {
                _logger.LogWarning("Attempted to modify system schedule {ScheduleId} ({ScheduleName})", id, existingSchedule.Name);
                return StatusCode(403, "System schedules cannot be modified. This schedule is required for core system operations.");
            }

            var notificationSetting = schedule.NotificationSetting;
            schedule.NotificationSetting = null;

            schedule.ModifiedDateTime = DateTime.UtcNow;
            schedule.ModifiedBy = User.Identity?.Name ?? "System";

            if (!schedule.IsDeleted && schedule.IsEnabled && !schedule.NextRunDateTime.HasValue && !string.IsNullOrWhiteSpace(schedule.CronExpression))
            {
                try
                {
                    var trigger = TriggerBuilder.Create()
                        .WithCronSchedule(schedule.CronExpression)
                        .Build();
                    schedule.NextRunDateTime = trigger.GetNextFireTimeUtc()?.DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate NextRunDateTime for schedule {ScheduleId}", schedule.Id);
                }
            }
            else if (schedule.IsDeleted || !schedule.IsEnabled)
            {
                schedule.NextRunDateTime = null;
            }

            await _unitOfWork.Schedules.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            if (notificationSetting != null)
            {
                var existingNotification = existingSchedule.NotificationSetting;
                if (existingNotification != null)
                {
                    notificationSetting.Id = existingNotification.Id;
                    notificationSetting.ScheduleId = schedule.Id;
                    notificationSetting.ModifiedDateTime = DateTime.UtcNow;
                    notificationSetting.ModifiedBy = User.Identity?.Name ?? "System";
                    notificationSetting.CreatedDateTime = existingNotification.CreatedDateTime;
                    notificationSetting.CreatedBy = existingNotification.CreatedBy;
                    
                    await _unitOfWork.NotificationSettings.UpdateAsync(notificationSetting);
                }
                else
                {
                    var notifNow = DateTime.UtcNow;
                    var notifCreatedBy = User.Identity?.Name ?? "System";
                    notificationSetting.ScheduleId = schedule.Id;
                    notificationSetting.CreatedDateTime = notifNow;
                    notificationSetting.CreatedBy = notifCreatedBy;
                    notificationSetting.ModifiedDateTime = notifNow;
                    notificationSetting.ModifiedBy = notifCreatedBy;
                    
                    await _unitOfWork.NotificationSettings.AddAsync(notificationSetting);
                }
                await _unitOfWork.SaveChangesAsync();
            }

            if (schedule.IsEnabled)
            {
                await _schedulerService.ScheduleJob(schedule);
            }
            else
            {
                await _schedulerService.UnscheduleJob(schedule.Id, schedule.ClientId);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while updating the schedule");
        }
    }

    /// <summary>
    /// Soft deletes a schedule by marking it as deleted.
    /// System schedules cannot be deleted.
    /// </summary>
    /// <param name="id">The unique identifier of the schedule to delete.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Schedule was deleted successfully.</response>
    /// <response code="403">Cannot delete system schedules or unauthorized access.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while deleting the schedule.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Schedules.Delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }

            if (schedule.IsSystemSchedule)
            {
                _logger.LogWarning("Attempted to delete system schedule {ScheduleId} ({ScheduleName})", id, schedule.Name);
                return StatusCode(403, "System schedules cannot be deleted. This schedule is required for core system operations.");
            }

            var userClientId = User.FindFirst("client_id")?.Value;
            var isSystemAdmin = User.FindFirst("is_system_admin")?.Value == "True";
            
            if (!isSystemAdmin && schedule.ClientId.ToString() != userClientId)
            {
                _logger.LogWarning(
                    "Unauthorized delete attempt: User with ClientId {UserClientId} attempted to delete Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    userClientId, id, schedule.ClientId);
                return Forbid();
            }

            await _schedulerService.UnscheduleJob(schedule.Id, schedule.ClientId);

            schedule.IsDeleted = true;
            schedule.ModifiedDateTime = DateTime.UtcNow;
            schedule.ModifiedBy = User.Identity?.Name ?? "System";

            await _unitOfWork.Schedules.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while deleting the schedule");
        }
    }

    /// <summary>
    /// Manually triggers immediate execution of a schedule.
    /// </summary>
    /// <param name="id">The unique identifier of the schedule to trigger.</param>
    /// <returns>A success message if the job was triggered.</returns>
    /// <response code="200">Job was triggered successfully.</response>
    /// <response code="403">User is not authorized to trigger this schedule.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while triggering the schedule.</response>
    [HttpPost("{id}/trigger")]
    [Authorize(Policy = "Schedules.Execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerSchedule(int id)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }

            var userClientId = User.FindFirst("client_id")?.Value;
            var isSystemAdmin = User.FindFirst("is_system_admin")?.Value == "True";
            
            if (!isSystemAdmin && schedule.ClientId.ToString() != userClientId)
            {
                _logger.LogWarning(
                    "Unauthorized trigger attempt: User with ClientId {UserClientId} attempted to trigger Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    userClientId, id, schedule.ClientId);
                return Forbid();
            }

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
                    return StatusCode(500, $"Failed to register job before triggering: {ex.Message}");
                }
            }

            await _schedulerService.TriggerJobNow(schedule.Id, schedule.ClientId, User.Identity?.Name ?? "Manual");
            
            return Ok(new { message = "Job triggered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while triggering the schedule");
        }
    }

    /// <summary>
    /// Pauses a schedule, disabling its automatic execution.
    /// </summary>
    /// <param name="id">The unique identifier of the schedule to pause.</param>
    /// <returns>A success message if the job was paused.</returns>
    /// <response code="200">Job was paused successfully.</response>
    /// <response code="403">User is not authorized to pause this schedule.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while pausing the schedule.</response>
    [HttpPost("{id}/pause")]
    [Authorize(Policy = "Schedules.Update")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PauseSchedule(int id)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }

            var userClientId = User.FindFirst("client_id")?.Value;
            var isSystemAdmin = User.FindFirst("is_system_admin")?.Value == "True";
            
            if (!isSystemAdmin && schedule.ClientId.ToString() != userClientId)
            {
                _logger.LogWarning(
                    "Unauthorized pause attempt: User with ClientId {UserClientId} attempted to pause Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    userClientId, id, schedule.ClientId);
                return Forbid();
            }

            // Persist the IsEnabled = false to the database
            schedule.IsEnabled = false;
            schedule.ModifiedDateTime = DateTime.UtcNow;
            schedule.ModifiedBy = User.Identity?.Name ?? "System";
            
            await _unitOfWork.Schedules.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            await _schedulerService.PauseJob(schedule.Id, schedule.ClientId);
            
            return Ok(new { message = "Job paused successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while pausing the schedule");
        }
    }

    /// <summary>
    /// Resumes a paused schedule, enabling its automatic execution.
    /// </summary>
    /// <param name="id">The unique identifier of the schedule to resume.</param>
    /// <returns>A success message if the job was resumed.</returns>
    /// <response code="200">Job was resumed successfully.</response>
    /// <response code="403">User is not authorized to resume this schedule.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while resuming the schedule.</response>
    [HttpPost("{id}/resume")]
    [Authorize(Policy = "Schedules.Update")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResumeSchedule(int id)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }

            var userClientId = User.FindFirst("client_id")?.Value;
            var isSystemAdmin = User.FindFirst("is_system_admin")?.Value == "True";
            
            if (!isSystemAdmin && schedule.ClientId.ToString() != userClientId)
            {
                _logger.LogWarning(
                    "Unauthorized resume attempt: User with ClientId {UserClientId} attempted to resume Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    userClientId, id, schedule.ClientId);
                return Forbid();
            }

            // Persist the IsEnabled = true to the database
            schedule.IsEnabled = true;
            schedule.ModifiedDateTime = DateTime.UtcNow;
            schedule.ModifiedBy = User.Identity?.Name ?? "System";
            
            // Recalculate NextRunDateTime if we have a cron expression
            if (!string.IsNullOrWhiteSpace(schedule.CronExpression))
            {
                try
                {
                    var trigger = TriggerBuilder.Create()
                        .WithCronSchedule(schedule.CronExpression)
                        .Build();
                    schedule.NextRunDateTime = trigger.GetNextFireTimeUtc()?.DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate NextRunDateTime for schedule {ScheduleId}", schedule.Id);
                }
            }
            
            await _unitOfWork.Schedules.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            await _schedulerService.ResumeJob(schedule.Id, schedule.ClientId);
            
            return Ok(new { message = "Job resumed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while resuming the schedule");
        }
    }

    /// <summary>
    /// Creates multiple schedules in bulk from a list of date/time specifications.
    /// </summary>
    /// <param name="request">The bulk schedule request containing schedule dates and configuration.</param>
    /// <returns>A response containing the results of each schedule creation attempt.</returns>
    /// <response code="200">Returns the bulk creation results with success/failure counts.</response>
    /// <response code="500">An error occurred while processing bulk schedule creation.</response>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BulkScheduleResponse>> CreateBulkSchedules([FromBody] BulkScheduleRequest request)
    {
        var response = new BulkScheduleResponse();
        
        try
        {
            foreach (var scheduleDate in request.ScheduleDates)
            {
                try
                {
                    var cronExpression = GenerateCronExpression(scheduleDate.DateTime, includeYear: true);
                    
                    var bulkNow = DateTime.UtcNow;
                    var bulkCreatedBy = User.Identity?.Name ?? "System";
                    var schedule = new Schedule
                    {
                        Name = scheduleDate.Name,
                        Description = scheduleDate.Description,
                        ClientId = request.ClientId,
                        JobType = request.JobType,
                        Frequency = ScheduleFrequency.Monthly,
                        CronExpression = cronExpression,
                        IsEnabled = request.IsEnabled,
                        TimeZone = request.TimeZone,
                        JobConfiguration = request.JobConfiguration,
                        MaxRetries = request.MaxRetries,
                        RetryDelayMinutes = request.RetryDelayMinutes,
                        CreatedDateTime = bulkNow,
                        CreatedBy = bulkCreatedBy,
                        ModifiedDateTime = bulkNow,
                        ModifiedBy = bulkCreatedBy
                    };
                    
                    await _unitOfWork.Schedules.AddAsync(schedule);
                    await _unitOfWork.SaveChangesAsync();
                    
                    if (schedule.IsEnabled)
                    {
                        await _schedulerService.ScheduleJob(schedule);
                    }
                    
                    response.Results.Add(new ScheduleResult
                    {
                        Success = true,
                        ScheduleId = schedule.Id,
                        CronExpression = cronExpression,
                        DateTime = scheduleDate.DateTime
                    });
                    response.SuccessCount++;
                    
                    _logger.LogInformation("Created schedule {ScheduleId} for {DateTime} with CRON {CronExpression}", 
                        schedule.Id, scheduleDate.DateTime, cronExpression);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating schedule for {DateTime}", scheduleDate.DateTime);
                    response.Results.Add(new ScheduleResult
                    {
                        Success = false,
                        DateTime = scheduleDate.DateTime,
                        ErrorMessage = ex.Message
                    });
                    response.FailureCount++;
                }
            }
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk schedule creation");
            return StatusCode(500, "An error occurred while processing bulk schedule creation");
        }
    }

    /// <summary>
    /// Generates CRON expressions from a list of date/time values.
    /// This endpoint is publicly accessible without authentication.
    /// </summary>
    /// <param name="request">The request containing date/time values to convert to CRON expressions.</param>
    /// <returns>A list of CRON expressions with their descriptions and next fire times.</returns>
    /// <response code="200">Returns the generated CRON expressions.</response>
    /// <response code="500">An error occurred while generating CRON expressions.</response>
    [HttpPost("generate-cron")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GenerateCronResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<GenerateCronResponse> GenerateCronExpressions([FromBody] GenerateCronRequest request)
    {
        try
        {
            var response = new GenerateCronResponse();
            
            foreach (var dateTime in request.DateTimes)
            {
                var cronExpression = GenerateCronExpression(dateTime, request.IncludeYear);
                var description = GenerateCronDescription(dateTime);
                
                DateTime? nextFireTime = null;
                try
                {
                    var trigger = TriggerBuilder.Create()
                        .WithCronSchedule(cronExpression)
                        .Build();
                    nextFireTime = trigger.GetNextFireTimeUtc()?.DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate next fire time for CRON {CronExpression}", cronExpression);
                }
                
                response.CronExpressions.Add(new CronExpressionResult
                {
                    DateTime = dateTime,
                    CronExpression = cronExpression,
                    Description = description,
                    NextFireTime = nextFireTime
                });
            }
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CRON expressions");
            return StatusCode(500, "An error occurred while generating CRON expressions");
        }
    }

    private string GenerateCronExpression(DateTime dateTime, bool includeYear = true)
    {
        var second = 0;
        var minute = dateTime.Minute;
        var hour = dateTime.Hour;
        var day = dateTime.Day;
        var month = dateTime.Month;
        var year = dateTime.Year;
        
        if (includeYear)
        {
            return $"{second} {minute} {hour} {day} {month} ? {year}";
        }
        else
        {
            return $"{second} {minute} {hour} {day} {month} ?";
        }
    }

    private string GenerateCronDescription(DateTime dateTime)
    {
        return $"Run once on {dateTime:MMMM dd, yyyy} at {dateTime:h:mm tt}";
    }

    /// <summary>
    /// Exports schedules to Excel or CSV format.
    /// </summary>
    /// <param name="clientId">Optional client ID to filter schedules.</param>
    /// <param name="searchTerm">Optional search term to filter schedules.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="format">Export format: 'excel' (default) or 'csv'.</param>
    /// <returns>A file download containing the exported schedules.</returns>
    /// <response code="200">Returns the exported file.</response>
    /// <response code="500">An error occurred while exporting schedules.</response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportSchedules(
        [FromQuery] int? clientId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string format = "excel")
    {
        try
        {
            IEnumerable<Schedule> schedules = clientId.HasValue
                ? await _unitOfWork.Schedules.GetByClientIdWithNotificationSettingsAsync(clientId.Value)
                : await _unitOfWork.Schedules.GetAllWithNotificationSettingsAsync();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                schedules = schedules.Where(s => 
                    (s.Name?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            if (startDate.HasValue || endDate.HasValue)
            {
                schedules = schedules.Where(s => s.NextRunDateTime.HasValue).ToList();
                
                if (startDate.HasValue)
                {
                    schedules = schedules.Where(s => s.NextRunDateTime!.Value >= startDate.Value).ToList();
                }
                
                if (endDate.HasValue)
                {
                    schedules = schedules.Where(s => s.NextRunDateTime!.Value <= endDate.Value).ToList();
                }
            }

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new StringBuilder();
                csv.AppendLine("Id,Name,Description,ClientId,JobType,Frequency,CronExpression,NextRunDateTime,LastRunDateTime,IsEnabled,MaxRetries,RetryDelayMinutes,TimeZone,EnableSuccessNotifications,EnableFailureNotifications,FailureEmailRecipients,FailureEmailSubject,IncludeExecutionDetails,IncludeOutput");
                
                foreach (var s in schedules)
                {
                    var n = s.NotificationSetting;
                    csv.AppendLine(string.Join(",",
                        s.Id,
                        CsvEscape(s.Name),
                        CsvEscape(s.Description),
                        s.ClientId,
                        s.JobType,
                        s.Frequency,
                        CsvEscape(s.CronExpression),
                        s.NextRunDateTime?.ToString("o") ?? "",
                        s.LastRunDateTime?.ToString("o") ?? "",
                        s.IsEnabled,
                        s.MaxRetries,
                        s.RetryDelayMinutes,
                        CsvEscape(s.TimeZone),
                        n?.EnableSuccessNotifications ?? false,
                        n?.EnableFailureNotifications ?? false,
                        CsvEscape(n?.FailureEmailRecipients),
                        CsvEscape(n?.FailureEmailSubject),
                        n?.IncludeExecutionDetails ?? false,
                        n?.IncludeOutput ?? false
                    ));
                }
                
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"schedules_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }
            else
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Schedules");
                
                var headers = new[] {
                    "Id", "Name", "Description", "ClientId", "JobType", "Frequency", "CronExpression",
                    "NextRunDateTime", "LastRunDateTime", "IsEnabled", "MaxRetries", "RetryDelayMinutes", "TimeZone",
                    "EnableSuccessNotifications", "EnableFailureNotifications", "FailureEmailRecipients",
                    "FailureEmailSubject", "IncludeExecutionDetails", "IncludeOutput"
                };
                
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                int row = 2;
                foreach (var s in schedules)
                {
                    var n = s.NotificationSetting;
                    worksheet.Cell(row, 1).Value = s.Id;
                    worksheet.Cell(row, 2).Value = s.Name;
                    worksheet.Cell(row, 3).Value = s.Description;
                    worksheet.Cell(row, 4).Value = s.ClientId;
                    worksheet.Cell(row, 5).Value = s.JobType.ToString();
                    worksheet.Cell(row, 6).Value = s.Frequency.ToString();
                    worksheet.Cell(row, 7).Value = s.CronExpression;
                    if (s.NextRunDateTime.HasValue) worksheet.Cell(row, 8).Value = s.NextRunDateTime.Value;
                    if (s.LastRunDateTime.HasValue) worksheet.Cell(row, 9).Value = s.LastRunDateTime.Value;
                    worksheet.Cell(row, 10).Value = s.IsEnabled;
                    worksheet.Cell(row, 11).Value = s.MaxRetries;
                    worksheet.Cell(row, 12).Value = s.RetryDelayMinutes;
                    worksheet.Cell(row, 13).Value = s.TimeZone;
                    worksheet.Cell(row, 14).Value = n?.EnableSuccessNotifications ?? false;
                    worksheet.Cell(row, 15).Value = n?.EnableFailureNotifications ?? false;
                    worksheet.Cell(row, 16).Value = n?.FailureEmailRecipients;
                    worksheet.Cell(row, 17).Value = n?.FailureEmailSubject;
                    worksheet.Cell(row, 18).Value = n?.IncludeExecutionDetails ?? false;
                    worksheet.Cell(row, 19).Value = n?.IncludeOutput ?? false;
                    row++;
                }
                
                // Create table with auto-filter and alternating row colors
                var dataRange = worksheet.Range(1, 1, row - 1, 19);
                var table = dataRange.CreateTable("SchedulesTable");
                table.Theme = XLTableTheme.TableStyleLight9; // Light blue alternating rows
                
                worksheet.Columns().AdjustToContents();
                
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    $"schedules_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting schedules");
            return StatusCode(500, "An error occurred while exporting schedules");
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
    /// Tests a database connection string to verify connectivity.
    /// </summary>
    /// <param name="request">The request containing the connection string to test.</param>
    /// <returns>A response indicating whether the connection was successful.</returns>
    /// <response code="200">Returns the connection test result (success or failure with details).</response>
    /// <response code="400">Connection string is required but was not provided.</response>
    [HttpPost("test-connection")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestConnectionResponse>> TestConnection([FromBody] TestConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            return BadRequest(new TestConnectionResponse 
            { 
                Success = false, 
                Message = "Connection string is required." 
            });
        }

        try
        {
            var connStr = request.ConnectionString;
            if (!connStr.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase) &&
                !connStr.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
            {
                connStr += ";TrustServerCertificate=True";
            }

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            return Ok(new TestConnectionResponse 
            { 
                Success = true, 
                Message = "Connection successful." 
            });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning("Connection test failed (SQL): {ErrorNumber}", ex.Number);
            return Ok(new TestConnectionResponse 
            { 
                Success = false, 
                Message = ex.Message, 
                ErrorNumber = ex.Number 
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection test failed");
            return Ok(new TestConnectionResponse 
            { 
                Success = false, 
                Message = ex.Message 
            });
        }
    }

    /// <summary>
    /// Gets the count of schedules that have missed their expected execution time.
    /// </summary>
    /// <param name="windowDays">Optional number of days to look back for missed schedules. If not specified, checks all time.</param>
    /// <returns>The count of missed schedules with metadata.</returns>
    /// <response code="200">Returns the count of missed schedules.</response>
    /// <response code="500">An error occurred while retrieving missed schedules count.</response>
    [HttpGet("missed/count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetMissedSchedulesCount([FromQuery] int? windowDays = null)
    {
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = windowDays.HasValue 
                ? now.AddDays(-windowDays.Value) 
                : DateTime.MinValue;

            var missedSchedules = await _unitOfWork.Schedules.FindAsync(s =>
                s.IsEnabled &&
                !s.IsDeleted &&
                s.NextRunDateTime.HasValue &&
                s.NextRunDateTime.Value < now &&
                s.NextRunDateTime.Value >= windowStart);

            var count = missedSchedules.Count();

            return Ok(new 
            { 
                count = count,
                windowDays = windowDays,
                asOfUtc = now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving missed schedules count");
            return StatusCode(500, "An error occurred while retrieving missed schedules count");
        }
    }

    /// <summary>
    /// Retrieves a paginated list of schedules that have missed their expected execution time.
    /// </summary>
    /// <param name="windowDays">Number of days to look back for missed schedules (default: 2).</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 100).</param>
    /// <returns>A paginated list of missed schedules with details.</returns>
    /// <response code="200">Returns the list of missed schedules.</response>
    /// <response code="500">An error occurred while retrieving missed schedules.</response>
    [HttpGet("missed")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetMissedSchedules(
        [FromQuery] int? windowDays = 2,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = windowDays.HasValue 
                ? now.AddDays(-windowDays.Value) 
                : DateTime.MinValue;

            var missedSchedules = await _unitOfWork.Schedules.FindAsync(s =>
                s.IsEnabled &&
                !s.IsDeleted &&
                s.NextRunDateTime.HasValue &&
                s.NextRunDateTime.Value < now &&
                s.NextRunDateTime.Value >= windowStart);

            var missedList = missedSchedules
                .OrderBy(s => s.NextRunDateTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                                .Select(s => new
                                {
                                    s.Id,
                                    s.Name,
                                    s.ClientId,
                                    s.NextRunDateTime,
                                    Frequency = s.Frequency.ToString(),
                                    s.CronExpression,
                                    s.LastRunDateTime,
                                    MinutesLate = s.NextRunDateTime.HasValue ? (now - s.NextRunDateTime.Value).TotalMinutes : 0
                                })
                .ToList();

            var totalCount = missedSchedules.Count();

            return Ok(new 
            { 
                items = missedList,
                totalCount = totalCount,
                pageNumber = pageNumber,
                pageSize = pageSize,
                windowDays = windowDays,
                asOfUtc = now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving missed schedules");
            return StatusCode(500, "An error occurred while retrieving missed schedules");
        }
    }

    /// <summary>
    /// Manually triggers a missed schedule for immediate execution.
    /// </summary>
    /// <param name="id">The unique identifier of the missed schedule to trigger.</param>
    /// <returns>A success message if the job was triggered.</returns>
    /// <response code="200">Job was triggered successfully.</response>
    /// <response code="403">User is not authorized to trigger this schedule.</response>
    /// <response code="404">Schedule with the specified ID was not found.</response>
    /// <response code="500">An error occurred while triggering the schedule.</response>
    [HttpPost("missed/{id}/trigger")]
    [Authorize(Policy = "Schedules.Execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerMissedSchedule(int id)
    {
        return await TriggerSchedule(id);
    }

    /// <summary>
    /// Triggers multiple missed schedules in bulk for recovery purposes.
    /// All authenticated users can trigger missed schedules.
    /// </summary>
    /// <param name="request">The request containing schedule IDs to trigger and optional delay between triggers.</param>
    /// <returns>A summary of the bulk trigger operation with success/failure counts and details.</returns>
    /// <response code="200">Returns the bulk trigger results.</response>
    /// <response code="400">No schedule IDs were provided in the request.</response>
    /// <response code="500">An error occurred while bulk triggering missed schedules.</response>
    [HttpPost("missed/bulk-trigger")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> BulkTriggerMissedSchedules([FromBody] BulkTriggerRequest request)
    {
        try
        {
            if (request.ScheduleIds == null || !request.ScheduleIds.Any())
            {
                return BadRequest("At least one schedule ID is required");
            }

            var results = new List<object>();
            int successCount = 0;
            int failureCount = 0;
            var delayMs = request.DelayBetweenTriggersMs ?? 200; // Default 200ms between triggers

            _logger.LogInformation(
                "BulkTriggerMissedSchedules: Starting bulk trigger for {Count} schedules with {DelayMs}ms delay",
                request.ScheduleIds.Count, delayMs);

            foreach (var scheduleId in request.ScheduleIds)
            {
                try
                {
                    var schedule = await _unitOfWork.Schedules.GetByIdAsync(scheduleId);
                    if (schedule == null)
                    {
                        results.Add(new { scheduleId, success = false, error = "Schedule not found" });
                        failureCount++;
                        continue;
                    }

                    var jobKey = new JobKey($"Job_{schedule.Id}", $"Group_{schedule.ClientId}");
                    if (!await _scheduler.CheckExists(jobKey))
                    {
                        _logger.LogDebug(
                            "BulkTriggerMissedSchedules: Job not in Quartz for schedule {ScheduleId}, scheduling it now",
                            schedule.Id);
                        await _schedulerService.ScheduleJob(schedule);
                    }

                    await _schedulerService.TriggerJobNow(schedule.Id, schedule.ClientId, "BulkTrigger");
                    results.Add(new { scheduleId, success = true, scheduleName = schedule.Name });
                    successCount++;

                    _logger.LogDebug(
                        "BulkTriggerMissedSchedules: Triggered schedule {ScheduleId} ({ScheduleName})",
                        schedule.Id, schedule.Name);

                    if (delayMs > 0 && scheduleId != request.ScheduleIds.Last())
                    {
                        await Task.Delay(delayMs);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BulkTriggerMissedSchedules: Failed to trigger schedule {ScheduleId}", scheduleId);
                    results.Add(new { scheduleId, success = false, error = ex.Message });
                    failureCount++;
                }
            }

            _logger.LogInformation(
                "BulkTriggerMissedSchedules: Completed. {SuccessCount} succeeded, {FailureCount} failed",
                successCount, failureCount);

            return Ok(new
            {
                successCount,
                failureCount,
                totalRequested = request.ScheduleIds.Count,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk trigger of missed schedules");
            return StatusCode(500, "An error occurred while bulk triggering missed schedules");
        }
    }

    /// <summary>
    /// Retrieves schedules formatted for calendar display within a date range.
    /// </summary>
    /// <param name="startDate">The start date of the calendar view (required).</param>
    /// <param name="endDate">The end date of the calendar view (required).</param>
    /// <param name="clientId">Optional client ID to filter schedules.</param>
    /// <param name="maxPerDay">Maximum number of schedules to return per day (default: 10).</param>
    /// <returns>A list of schedules with calendar-relevant properties.</returns>
    /// <response code="200">Returns the schedules for the calendar view.</response>
    /// <response code="400">Start date and end date are required but were not provided.</response>
    /// <response code="500">An error occurred while retrieving schedules for calendar.</response>
    [HttpGet("calendar")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<object>>> GetSchedulesForCalendar(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? clientId = null,
        [FromQuery] int maxPerDay = 10)
    {
        try
        {
            if (!startDate.HasValue || !endDate.HasValue)
            {
                return BadRequest("startDate and endDate are required for calendar view");
            }

            var schedules = await _unitOfWork.Schedules.GetSchedulesForCalendarAsync(
                startDate.Value.ToUniversalTime(),
                endDate.Value.ToUniversalTime(),
                clientId,
                maxPerDay);

            var result = schedules.Select(s => new
            {
                s.Id,
                s.Name,
                s.ClientId,
                s.NextRunDateTime,
                s.TimeZone,
                s.IsEnabled
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules for calendar");
            return StatusCode(500, "An error occurred while retrieving schedules for calendar");
        }
    }
}
