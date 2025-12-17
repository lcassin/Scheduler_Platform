using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing notification settings for schedules.
/// Provides endpoints for CRUD operations on notification configurations.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationSettingsController : ControllerBase
{
    private readonly IRepository<NotificationSetting> _repository;
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationSettingsController> _logger;

    public NotificationSettingsController(
        IRepository<NotificationSetting> repository,
        IScheduleRepository scheduleRepository,
        IUnitOfWork unitOfWork,
        ILogger<NotificationSettingsController> logger)
    {
        _repository = repository;
        _scheduleRepository = scheduleRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves notification settings for a specific schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <returns>The notification settings for the schedule.</returns>
    /// <response code="200">Returns the notification settings.</response>
    /// <response code="404">The schedule or notification settings were not found.</response>
    [HttpGet("schedule/{scheduleId}")]
    [ProducesResponseType(typeof(NotificationSetting), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationSetting>> GetByScheduleId(int scheduleId)
    {
        var schedule = await _scheduleRepository.GetByIdWithNotificationSettingsAsync(scheduleId);
        if (schedule == null)
            return NotFound();

        if (schedule.NotificationSetting == null)
            return NotFound();

        return Ok(schedule.NotificationSetting);
    }

    /// <summary>
    /// Creates notification settings for a schedule.
    /// </summary>
    /// <param name="notificationSetting">The notification settings to create.</param>
    /// <returns>The created notification settings.</returns>
    /// <response code="201">Returns the newly created notification settings.</response>
    /// <response code="400">The associated schedule was not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationSetting), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationSetting>> Create([FromBody] NotificationSetting notificationSetting)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(notificationSetting.ScheduleId);
        if (schedule == null)
            return BadRequest("Schedule not found");

        var now = DateTime.UtcNow;
        var createdBy = User.Identity?.Name ?? "System";
        notificationSetting.CreatedDateTime = now;
        notificationSetting.CreatedBy = createdBy;
        notificationSetting.ModifiedDateTime = now;
        notificationSetting.ModifiedBy = createdBy;

        await _repository.AddAsync(notificationSetting);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Notification settings created for schedule {ScheduleId} by {User}", 
            notificationSetting.ScheduleId, notificationSetting.CreatedBy);

        return CreatedAtAction(nameof(GetByScheduleId), new { scheduleId = notificationSetting.ScheduleId }, notificationSetting);
    }

    /// <summary>
    /// Updates existing notification settings.
    /// </summary>
    /// <param name="id">The notification settings ID.</param>
    /// <param name="notificationSetting">The updated notification settings.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The notification settings were successfully updated.</response>
    /// <response code="400">The ID in the URL does not match the ID in the body.</response>
    /// <response code="404">The notification settings were not found.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] NotificationSetting notificationSetting)
    {
        if (id != notificationSetting.Id)
            return BadRequest();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        existing.EnableSuccessNotifications = notificationSetting.EnableSuccessNotifications;
        existing.EnableFailureNotifications = notificationSetting.EnableFailureNotifications;
        existing.SuccessEmailRecipients = notificationSetting.SuccessEmailRecipients;
        existing.FailureEmailRecipients = notificationSetting.FailureEmailRecipients;
        existing.SuccessEmailSubject = notificationSetting.SuccessEmailSubject;
        existing.FailureEmailSubject = notificationSetting.FailureEmailSubject;
        existing.IncludeExecutionDetails = notificationSetting.IncludeExecutionDetails;
        existing.IncludeOutput = notificationSetting.IncludeOutput;
        existing.ModifiedDateTime = DateTime.UtcNow;
        existing.ModifiedBy = User.Identity?.Name ?? "System";

        await _repository.UpdateAsync(existing);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Notification settings updated for schedule {ScheduleId} by {User}", 
            existing.ScheduleId, existing.ModifiedBy);

        return NoContent();
    }

    /// <summary>
    /// Deletes notification settings.
    /// </summary>
    /// <param name="id">The notification settings ID.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The notification settings were successfully deleted.</response>
    /// <response code="404">The notification settings were not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var notificationSetting = await _repository.GetByIdAsync(id);
        if (notificationSetting == null)
            return NotFound();

        await _repository.DeleteAsync(notificationSetting);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Notification settings deleted for schedule {ScheduleId} by {User}", 
            notificationSetting.ScheduleId, User.Identity?.Name ?? "System");

        return NoContent();
    }
}
