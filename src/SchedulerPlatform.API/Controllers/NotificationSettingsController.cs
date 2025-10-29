using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

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

    [HttpGet("schedule/{scheduleId}")]
    public async Task<ActionResult<NotificationSetting>> GetByScheduleId(int scheduleId)
    {
        var schedule = await _scheduleRepository.GetByIdWithNotificationSettingsAsync(scheduleId);
        if (schedule == null)
            return NotFound();

        if (schedule.NotificationSetting == null)
            return NotFound();

        return Ok(schedule.NotificationSetting);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationSetting>> Create([FromBody] NotificationSetting notificationSetting)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(notificationSetting.ScheduleId);
        if (schedule == null)
            return BadRequest("Schedule not found");

        notificationSetting.CreatedAt = DateTime.UtcNow;
        notificationSetting.CreatedBy = User.Identity?.Name ?? "System";

        await _repository.AddAsync(notificationSetting);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Notification settings created for schedule {ScheduleId} by {User}", 
            notificationSetting.ScheduleId, notificationSetting.CreatedBy);

        return CreatedAtAction(nameof(GetByScheduleId), new { scheduleId = notificationSetting.ScheduleId }, notificationSetting);
    }

    [HttpPut("{id}")]
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
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "System";

        await _repository.UpdateAsync(existing);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Notification settings updated for schedule {ScheduleId} by {User}", 
            existing.ScheduleId, existing.UpdatedBy);

        return NoContent();
    }

    [HttpDelete("{id}")]
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
