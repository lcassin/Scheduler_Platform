using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using SchedulerPlatform.API.Models;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IUnitOfWork unitOfWork, ILogger<DashboardController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewResponse>> GetOverview(
        [FromQuery] int? clientId = null,
        [FromQuery] int hours = 24)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddHours(-hours);
            var schedules = await _unitOfWork.Schedules.GetAllAsync();
            
            if (clientId.HasValue)
            {
                schedules = schedules.Where(s => s.ClientId == clientId.Value);
            }

            var schedulesList = schedules.ToList();
            var executions = await _unitOfWork.JobExecutions.GetByFiltersAsync(null, null, startDate, null);
            
            if (clientId.HasValue)
            {
                var clientScheduleIds = schedulesList.Select(s => s.Id).ToHashSet();
                executions = executions.Where(e => clientScheduleIds.Contains(e.ScheduleId));
            }

            var executionsList = executions.ToList();
            var today = DateTime.UtcNow.Date;
            
            var peakConcurrent = CalculatePeakConcurrentExecutions(executionsList);

            var overview = new DashboardOverviewResponse
            {
                TotalSchedules = schedulesList.Count,
                EnabledSchedules = schedulesList.Count(s => s.IsEnabled),
                DisabledSchedules = schedulesList.Count(s => !s.IsEnabled),
                RunningExecutions = executionsList.Count(e => e.Status == JobStatus.Running || e.Status == JobStatus.Retrying),
                CompletedToday = executionsList.Count(e => e.StartTime >= today && e.Status == JobStatus.Completed),
                FailedToday = executionsList.Count(e => e.StartTime >= today && e.Status == JobStatus.Failed),
                PeakConcurrentExecutions = peakConcurrent,
                AverageDurationSeconds = executionsList.Where(e => e.DurationSeconds.HasValue).Average(e => (double?)e.DurationSeconds) ?? 0,
                TotalExecutionsInWindow = executionsList.Count
            };

            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard overview");
            return StatusCode(500, "Error retrieving dashboard overview");
        }
    }

    [HttpGet("status-breakdown")]
    public async Task<ActionResult<List<StatusBreakdownItem>>> GetStatusBreakdown(
        [FromQuery] int hours = 24,
        [FromQuery] int? clientId = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddHours(-hours);
            var executions = await _unitOfWork.JobExecutions.GetByFiltersAsync(null, null, startDate, null);

            if (clientId.HasValue)
            {
                var schedules = await _unitOfWork.Schedules.GetAllAsync();
                var clientScheduleIds = schedules
                    .Where(s => s.ClientId == clientId.Value)
                    .Select(s => s.Id)
                    .ToHashSet();
                executions = executions.Where(e => clientScheduleIds.Contains(e.ScheduleId));
            }

            var executionsList = executions.ToList();
            var totalCount = executionsList.Count;

            var breakdown = executionsList
                .GroupBy(e => e.Status)
                .Select(g => new StatusBreakdownItem
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Percentage = totalCount > 0 ? (double)g.Count() / totalCount * 100 : 0
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return Ok(breakdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status breakdown");
            return StatusCode(500, "Error retrieving status breakdown");
        }
    }

    [HttpGet("execution-trends")]
    public async Task<ActionResult<List<ExecutionTrendItem>>> GetExecutionTrends(
        [FromQuery] int hours = 24,
        [FromQuery] int? clientId = null,
        [FromQuery] JobStatus[] statuses = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddHours(-hours);
            
            var executions = await _unitOfWork.JobExecutions.GetByFiltersAsync(
                null, null, startDate, null);

            if (statuses != null && statuses.Length > 0)
            {
                executions = executions.Where(e => statuses.Contains(e.Status));
            }

            if (clientId.HasValue)
            {
                var schedules = await _unitOfWork.Schedules.GetAllAsync();
                var clientScheduleIds = schedules
                    .Where(s => s.ClientId == clientId.Value)
                    .Select(s => s.Id)
                    .ToHashSet();
                executions = executions.Where(e => clientScheduleIds.Contains(e.ScheduleId));
            }

            var executionsList = executions.ToList();
            var trends = executionsList
                .GroupBy(e => new DateTime(e.StartTime.Year, e.StartTime.Month, e.StartTime.Day, e.StartTime.Hour, 0, 0))
                .Select(g => new ExecutionTrendItem
                {
                    Hour = g.Key,
                    AverageDurationSeconds = g.Where(e => e.DurationSeconds.HasValue).Average(e => (double?)e.DurationSeconds) ?? 0,
                    ExecutionCount = g.Count(),
                    ConcurrentCount = g.Count(e => e.Status == JobStatus.Running || e.Status == JobStatus.Retrying)
                })
                .OrderBy(x => x.Hour)
                .ToList();

            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution trends");
            return StatusCode(500, "Error retrieving execution trends");
        }
    }

    [HttpGet("top-longest")]
    public async Task<ActionResult<List<TopLongestExecutionItem>>> GetTopLongestExecutions(
        [FromQuery] int limit = 10,
        [FromQuery] int hours = 24,
        [FromQuery] int? clientId = null,
        [FromQuery] JobStatus[] statuses = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddHours(-hours);
            var executions = await _unitOfWork.JobExecutions.GetByFiltersAsync(
                null, null, startDate, null);

            if (statuses != null && statuses.Length > 0)
            {
                executions = executions.Where(e => statuses.Contains(e.Status));
            }

            if (clientId.HasValue)
            {
                var schedules = await _unitOfWork.Schedules.GetAllAsync();
                var clientScheduleIds = schedules
                    .Where(s => s.ClientId == clientId.Value)
                    .Select(s => s.Id)
                    .ToHashSet();
                executions = executions.Where(e => clientScheduleIds.Contains(e.ScheduleId));
            }

            var topLongest = executions
                .Where(e => e.DurationSeconds.HasValue)
                .OrderByDescending(e => e.DurationSeconds)
                .Take(limit)
                .Select(e => new TopLongestExecutionItem
                {
                    ScheduleName = e.Schedule?.Name ?? "Unknown",
                    DurationSeconds = e.DurationSeconds!.Value,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime
                })
                .ToList();

            return Ok(topLongest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top longest executions");
            return StatusCode(500, "Error retrieving top longest executions");
        }
    }

    [HttpGet("invalid-schedules")]
    public async Task<ActionResult<List<InvalidScheduleInfo>>> GetInvalidSchedules(
        [FromQuery] int? clientId = null)
    {
        try
        {
            var schedules = await _unitOfWork.Schedules.GetAllAsync();
            
            schedules = schedules.Where(s => !s.IsDeleted);
            
            if (clientId.HasValue)
            {
                schedules = schedules.Where(s => s.ClientId == clientId.Value);
            }

            var schedulesList = schedules.ToList();
            var invalidSchedules = new List<InvalidScheduleInfo>();
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            foreach (var schedule in schedulesList)
            {
                var validationErrors = new List<string>();

                if (!string.IsNullOrWhiteSpace(schedule.CronExpression))
                {
                    try
                    {
                        var testTrigger = Quartz.TriggerBuilder.Create()
                            .WithCronSchedule(schedule.CronExpression)
                            .Build();
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Invalid CRON expression: {ex.Message}");
                    }
                }
                else
                {
                    validationErrors.Add("CRON expression is missing");
                }

                if (!string.IsNullOrWhiteSpace(schedule.JobConfiguration))
                {
                    try
                    {
                        var configErrors = ValidateJobConfiguration(schedule);
                        validationErrors.AddRange(configErrors);
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Invalid job configuration: {ex.Message}");
                    }
                }
                else
                {
                    validationErrors.Add("Job configuration is missing");
                }

                var recentExecutions = await _unitOfWork.JobExecutions.GetByFiltersAsync(
                    schedule.Id, null, sevenDaysAgo, null);
                
                var schedulingFailure = recentExecutions
                    .Where(e => e.ErrorMessage != null && 
                           e.ErrorMessage.Contains("Failed to schedule", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.StartTime)
                    .FirstOrDefault();

                if (schedulingFailure != null)
                {
                    validationErrors.Add("Recent scheduling failure detected");
                }

                if (validationErrors.Any())
                {
                    invalidSchedules.Add(new InvalidScheduleInfo
                    {
                        ScheduleId = schedule.Id,
                        Name = schedule.Name,
                        IsEnabled = schedule.IsEnabled,
                        ValidationErrors = validationErrors,
                        LastFailureTime = schedulingFailure?.StartTime,
                        LastErrorMessage = schedulingFailure?.ErrorMessage
                    });
                }
            }

            return Ok(invalidSchedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invalid schedules");
            return StatusCode(500, "Error retrieving invalid schedules");
        }
    }

    private List<string> ValidateJobConfiguration(Core.Domain.Entities.Schedule schedule)
    {
        var errors = new List<string>();

        try
        {
            switch (schedule.JobType)
            {
                case Core.Domain.Enums.JobType.StoredProcedure:
                    var spConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        schedule.JobConfiguration ?? "{}",
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (spConfig == null || !spConfig.ContainsKey("ConnectionString") || 
                        string.IsNullOrWhiteSpace(spConfig["ConnectionString"]?.ToString()))
                    {
                        errors.Add("ConnectionString is required for Stored Procedure jobs");
                    }
                    if (spConfig == null || !spConfig.ContainsKey("ProcedureName") || 
                        string.IsNullOrWhiteSpace(spConfig["ProcedureName"]?.ToString()))
                    {
                        errors.Add("ProcedureName is required for Stored Procedure jobs");
                    }
                    break;

                case Core.Domain.Enums.JobType.Process:
                    var processConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        schedule.JobConfiguration ?? "{}",
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (processConfig == null || !processConfig.ContainsKey("ExecutablePath") || 
                        string.IsNullOrWhiteSpace(processConfig["ExecutablePath"]?.ToString()))
                    {
                        errors.Add("ExecutablePath is required for Process jobs");
                    }
                    break;

                case Core.Domain.Enums.JobType.ApiCall:
                    var apiConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        schedule.JobConfiguration ?? "{}",
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (apiConfig == null || !apiConfig.ContainsKey("Url") || 
                        string.IsNullOrWhiteSpace(apiConfig["Url"]?.ToString()))
                    {
                        errors.Add("Url is required for API Call jobs");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse job configuration: {ex.Message}");
        }

        return errors;
    }

    private int CalculatePeakConcurrentExecutions(List<Core.Domain.Entities.JobExecution> executions)
    {
        if (!executions.Any())
            return 0;

        var events = new List<(DateTime Time, int Delta)>();

        foreach (var execution in executions)
        {
            events.Add((execution.StartTime, 1));
            
            if (execution.EndTime.HasValue)
            {
                events.Add((execution.EndTime.Value, -1));
            }
        }

        events = events.OrderBy(e => e.Time).ThenBy(e => e.Delta).ToList();

        int currentConcurrent = 0;
        int peakConcurrent = 0;

        foreach (var evt in events)
        {
            currentConcurrent += evt.Delta;
            peakConcurrent = Math.Max(peakConcurrent, currentConcurrent);
        }

        return peakConcurrent;
    }
}
