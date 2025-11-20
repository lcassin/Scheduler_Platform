using System.Text;
using ClosedXML.Excel;
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

    [HttpGet]
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
                var schedules = clientId.HasValue
                    ? await _unitOfWork.Schedules.GetByClientIdAsync(clientId.Value)
                    : await _unitOfWork.Schedules.GetAllAsync();

                if (startDate.HasValue || endDate.HasValue)
                {
                    schedules = schedules.Where(s => s.NextRunTime.HasValue).ToList();
                    
                    if (startDate.HasValue)
                    {
                        schedules = schedules.Where(s => s.NextRunTime!.Value >= startDate.Value).ToList();
                    }
                    
                    if (endDate.HasValue)
                    {
                        schedules = schedules.Where(s => s.NextRunTime!.Value <= endDate.Value).ToList();
                    }
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

    [HttpGet("{id}")]
    public async Task<ActionResult<Schedule>> GetSchedule(int id)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdWithNotificationSettingsAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }

            var userClientId = User.FindFirst("client_id")?.Value;
            var isSystemAdmin = User.FindFirst("is_system_admin")?.Value == "True";
            
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

    [HttpPost]
    [Authorize(Policy = "Schedules.Create")]
    public async Task<ActionResult<Schedule>> CreateSchedule([FromBody] Schedule schedule)
    {
        try
        {
            var notificationSetting = schedule.NotificationSetting;
            schedule.NotificationSetting = null;

            schedule.CreatedAt = DateTime.UtcNow;
            schedule.CreatedBy = User.Identity?.Name ?? "System";

            if (!schedule.NextRunTime.HasValue && !string.IsNullOrWhiteSpace(schedule.CronExpression))
            {
                try
                {
                    var trigger = TriggerBuilder.Create()
                        .WithCronSchedule(schedule.CronExpression)
                        .Build();
                    schedule.NextRunTime = trigger.GetNextFireTimeUtc()?.DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate NextRunTime for schedule {ScheduleId}", schedule.Id);
                }
            }

            await _unitOfWork.Schedules.AddAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            if (notificationSetting != null)
            {
                notificationSetting.ScheduleId = schedule.Id;
                notificationSetting.CreatedAt = DateTime.UtcNow;
                notificationSetting.CreatedBy = User.Identity?.Name ?? "System";
                
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

    [HttpPut("{id}")]
    [Authorize(Policy = "Schedules.Update")]
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

            var notificationSetting = schedule.NotificationSetting;
            schedule.NotificationSetting = null;

            schedule.UpdatedAt = DateTime.UtcNow;
            schedule.UpdatedBy = User.Identity?.Name ?? "System";

            if (!schedule.NextRunTime.HasValue && !string.IsNullOrWhiteSpace(schedule.CronExpression))
            {
                try
                {
                    var trigger = TriggerBuilder.Create()
                        .WithCronSchedule(schedule.CronExpression)
                        .Build();
                    schedule.NextRunTime = trigger.GetNextFireTimeUtc()?.DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not calculate NextRunTime for schedule {ScheduleId}", schedule.Id);
                }
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
                    notificationSetting.UpdatedAt = DateTime.UtcNow;
                    notificationSetting.UpdatedBy = User.Identity?.Name ?? "System";
                    notificationSetting.CreatedAt = existingNotification.CreatedAt;
                    notificationSetting.CreatedBy = existingNotification.CreatedBy;
                    
                    await _unitOfWork.NotificationSettings.UpdateAsync(notificationSetting);
                }
                else
                {
                    notificationSetting.ScheduleId = schedule.Id;
                    notificationSetting.CreatedAt = DateTime.UtcNow;
                    notificationSetting.CreatedBy = User.Identity?.Name ?? "System";
                    
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

    [HttpDelete("{id}")]
    [Authorize(Policy = "Schedules.Delete")]
    public async Task<IActionResult> DeleteSchedule(int id)
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
                    "Unauthorized delete attempt: User with ClientId {UserClientId} attempted to delete Schedule {ScheduleId} belonging to ClientId {ScheduleClientId}",
                    userClientId, id, schedule.ClientId);
                return Forbid();
            }

            await _schedulerService.UnscheduleJob(schedule.Id, schedule.ClientId);

            schedule.IsDeleted = true;
            schedule.UpdatedAt = DateTime.UtcNow;
            schedule.UpdatedBy = User.Identity?.Name ?? "System";

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

    [HttpPost("{id}/trigger")]
    [Authorize(Policy = "Schedules.Execute")]
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

    [HttpPost("{id}/pause")]
    [Authorize(Policy = "Schedules.Update")]
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

            await _schedulerService.PauseJob(schedule.Id, schedule.ClientId);
            
            return Ok(new { message = "Job paused successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while pausing the schedule");
        }
    }

    [HttpPost("{id}/resume")]
    [Authorize(Policy = "Schedules.Update")]
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

            await _schedulerService.ResumeJob(schedule.Id, schedule.ClientId);
            
            return Ok(new { message = "Job resumed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while resuming the schedule");
        }
    }

    [HttpPost("bulk")]
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
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = User.Identity?.Name ?? "System"
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

    [HttpPost("generate-cron")]
    [AllowAnonymous]
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

    [HttpGet("export")]
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
                schedules = schedules.Where(s => s.NextRunTime.HasValue).ToList();
                
                if (startDate.HasValue)
                {
                    schedules = schedules.Where(s => s.NextRunTime!.Value >= startDate.Value).ToList();
                }
                
                if (endDate.HasValue)
                {
                    schedules = schedules.Where(s => s.NextRunTime!.Value <= endDate.Value).ToList();
                }
            }

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new StringBuilder();
                csv.AppendLine("Id,Name,Description,ClientId,JobType,Frequency,CronExpression,NextRunTime,LastRunTime,IsEnabled,MaxRetries,RetryDelayMinutes,TimeZone,EnableSuccessNotifications,EnableFailureNotifications,FailureEmailRecipients,FailureEmailSubject,IncludeExecutionDetails,IncludeOutput");
                
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
                        s.NextRunTime?.ToString("o") ?? "",
                        s.LastRunTime?.ToString("o") ?? "",
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
                    "NextRunTime", "LastRunTime", "IsEnabled", "MaxRetries", "RetryDelayMinutes", "TimeZone",
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
                    if (s.NextRunTime.HasValue) worksheet.Cell(row, 8).Value = s.NextRunTime.Value;
                    if (s.LastRunTime.HasValue) worksheet.Cell(row, 9).Value = s.LastRunTime.Value;
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

    [HttpPost("test-connection")]
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

    [HttpGet("missed/count")]
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
                s.NextRunTime.HasValue &&
                s.NextRunTime.Value < now &&
                s.NextRunTime.Value >= windowStart);

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

    [HttpGet("missed")]
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
                s.NextRunTime.HasValue &&
                s.NextRunTime.Value < now &&
                s.NextRunTime.Value >= windowStart);

            var missedList = missedSchedules
                .OrderBy(s => s.NextRunTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.ClientId,
                    s.NextRunTime,
                    s.Frequency,
                    s.CronExpression,
                    s.LastRunTime,
                    MinutesLate = s.NextRunTime.HasValue ? (now - s.NextRunTime.Value).TotalMinutes : 0
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

    [HttpPost("missed/{id}/trigger")]
    [Authorize(Policy = "Schedules.Execute")]
    public async Task<IActionResult> TriggerMissedSchedule(int id)
    {
        return await TriggerSchedule(id);
    }

    [HttpGet("calendar")]
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
                s.NextRunTime,
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
