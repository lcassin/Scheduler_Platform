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
            [FromQuery] int hours = 24,
            [FromQuery] string? timezone = null)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddHours(-hours);
            
                // Calculate "today" based on the user's timezone
                // If no timezone provided, default to UTC
                DateTime today;
                if (!string.IsNullOrEmpty(timezone))
                {
                    try
                    {
                        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                        var localToday = localNow.Date;
                        // Convert local midnight back to UTC
                        today = TimeZoneInfo.ConvertTimeToUtc(localToday, tz);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        _logger.LogWarning("Invalid timezone '{Timezone}', falling back to UTC", timezone);
                        today = DateTime.UtcNow.Date;
                    }
                }
                else
                {
                    today = DateTime.UtcNow.Date;
                }
            
            var totalSchedules = await _unitOfWork.Schedules.GetTotalSchedulesCountAsync(clientId);
            var enabledSchedules = await _unitOfWork.Schedules.GetEnabledSchedulesCountAsync(clientId);
            var disabledSchedules = await _unitOfWork.Schedules.GetDisabledSchedulesCountAsync(clientId);

            var runningExecutions = await _unitOfWork.JobExecutions.GetRunningCountAsync(startDate, clientId);
            var completedToday = await _unitOfWork.JobExecutions.GetCompletedTodayCountAsync(today, clientId);
            var failedToday = await _unitOfWork.JobExecutions.GetFailedTodayCountAsync(today, clientId);
            var avgDuration = await _unitOfWork.JobExecutions.GetAverageDurationAsync(startDate, clientId);
            var totalExecutions = await _unitOfWork.JobExecutions.GetTotalExecutionsCountAsync(startDate, clientId);
            
            var executionsForPeak = await _unitOfWork.JobExecutions.GetExecutionsForPeakCalculationAsync(startDate, clientId);
            var peakConcurrent = CalculatePeakConcurrentExecutions(executionsForPeak);

            var overview = new DashboardOverviewResponse
            {
                TotalSchedules = totalSchedules,
                EnabledSchedules = enabledSchedules,
                DisabledSchedules = disabledSchedules,
                RunningExecutions = runningExecutions,
                CompletedToday = completedToday,
                FailedToday = failedToday,
                PeakConcurrentExecutions = peakConcurrent,
                AverageDurationSeconds = avgDuration,
                TotalExecutionsInWindow = totalExecutions
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
            
            var statusCounts = await _unitOfWork.JobExecutions.GetStatusBreakdownAsync(startDate, clientId);
            var totalCount = statusCounts.Values.Sum();

            var breakdown = statusCounts
                .Select(kvp => new StatusBreakdownItem
                {
                    Status = kvp.Key,
                    Count = kvp.Value,
                    Percentage = totalCount > 0 ? (double)kvp.Value / totalCount * 100 : 0
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
            
            var trendsData = await _unitOfWork.JobExecutions.GetExecutionTrendsAsync(startDate, clientId, statuses);

            var trends = trendsData.Select(t => new ExecutionTrendItem
            {
                Hour = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc),
                AverageDurationSeconds = t.AvgDuration,
                ExecutionCount = t.ExecutionCount,
                ConcurrentCount = t.ConcurrentCount
            }).ToList();

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
            
            var topLongestData = await _unitOfWork.JobExecutions.GetTopLongestAsync(startDate, clientId, statuses, limit);

            var topLongest = topLongestData.Select(t => new TopLongestExecutionItem
            {
                ScheduleName = t.ScheduleName,
                DurationSeconds = t.DurationSeconds,
                StartTime = t.StartTime,
                EndTime = t.EndTime
            }).ToList();

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
        [FromQuery] int? clientId = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            var schedulesQuery = await _unitOfWork.Schedules.FindAsync(s => !s.IsDeleted);
            
            if (clientId.HasValue)
            {
                schedulesQuery = schedulesQuery.Where(s => s.ClientId == clientId.Value);
            }

            var schedulesList = schedulesQuery.Take(limit).ToList();
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
