using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobExecutionsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobExecutionsController> _logger;
    private readonly IScheduler _scheduler;

    public JobExecutionsController(IUnitOfWork unitOfWork, ILogger<JobExecutionsController> logger, IScheduler scheduler)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _scheduler = scheduler;
    }

    [HttpGet]
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

    [HttpGet("{id}")]
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

    [HttpGet("schedule/{scheduleId}/latest")]
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

    [HttpGet("schedule/{scheduleId}/failed")]
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

    [HttpGet("export")]
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

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new StringBuilder();
                csv.AppendLine("Id,ScheduleId,ScheduleName,StartDateTime,EndDateTime,DurationSeconds,Status,RetryCount,ErrorMessage");
                
                foreach (var e in executions)
                {
                    var duration = e.EndDateTime.HasValue 
                        ? (e.EndDateTime.Value - e.StartDateTime).TotalSeconds 
                        : (double?)null;
                    
                    csv.AppendLine(string.Join(",",
                        e.Id,
                        e.ScheduleId,
                        CsvEscape(e.Schedule.Name),
                        e.StartDateTime.ToString("o"),
                        e.EndDateTime?.ToString("o") ?? "",
                        duration?.ToString(CultureInfo.InvariantCulture) ?? "",
                        e.Status,
                        e.RetryCount,
                        CsvEscape(e.ErrorMessage)
                    ));
                }
                
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"executions_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }
            else
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("JobExecutions");
                
                var headers = new[] {
                    "Id", "ScheduleId", "ScheduleName", "StartDateTime", "EndDateTime", 
                    "Duration", "Status", "RetryCount", "ErrorMessage"
                };
                
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                int row = 2;
                foreach (var e in executions)
                {
                    worksheet.Cell(row, 1).Value = e.Id;
                    worksheet.Cell(row, 2).Value = e.ScheduleId;
                    worksheet.Cell(row, 3).Value = e.Schedule.Name;
                    worksheet.Cell(row, 4).Value = e.StartDateTime;
                    if (e.EndDateTime.HasValue) worksheet.Cell(row, 5).Value = e.EndDateTime.Value;
                    if (e.EndDateTime.HasValue)
                    {
                        var duration = e.EndDateTime.Value - e.StartDateTime;
                        worksheet.Cell(row, 6).Value = duration.ToString();
                    }
                    worksheet.Cell(row, 7).Value = e.Status.ToString();
                    worksheet.Cell(row, 8).Value = e.RetryCount;
                    worksheet.Cell(row, 9).Value = e.ErrorMessage;
                    row++;
                }
                
                worksheet.Columns().AdjustToContents();
                
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    $"executions_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }
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

    [HttpPost("{id}/cancel")]
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
}
