using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Health check endpoints for monitoring application and orchestrator status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly SchedulerDbContext _context;
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;

    public HealthController(
        SchedulerDbContext context,
        ILogger<HealthController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Basic health check endpoint. Returns 200 if the API is running and can connect to the database.
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            // Check database connectivity
            var canConnect = await _context.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                return StatusCode(503, new HealthResponse
                {
                    Status = "Unhealthy",
                    Message = "Cannot connect to database",
                    Timestamp = DateTime.UtcNow
                });
            }

            return Ok(new HealthResponse
            {
                Status = "Healthy",
                Message = "API is running and database is connected",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new HealthResponse
            {
                Status = "Unhealthy",
                Message = $"Health check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Orchestrator health check endpoint. Returns healthy if the orchestrator has run successfully
    /// within the expected timeframe. The orchestrator is scheduled to run once daily at 1 AM,
    /// so this checks for a successful run within the last 26 hours (with buffer).
    /// </summary>
    /// <param name="maxHoursSinceLastRun">Maximum hours since last successful run (default: 26 for daily schedule with buffer)</param>
    /// <returns>Orchestrator health status with details about the last run</returns>
    [HttpGet("orchestrator")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrchestratorHealth([FromQuery] int maxHoursSinceLastRun = 26)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-maxHoursSinceLastRun);
            
            // Get the most recent orchestration run
            var lastRun = await _context.AdrOrchestrationRuns
                .OrderByDescending(r => r.RequestedDateTime)
                .FirstOrDefaultAsync();

            // Get the most recent successful/completed run
            var lastSuccessfulRun = await _context.AdrOrchestrationRuns
                .Where(r => r.Status == "Completed")
                .OrderByDescending(r => r.CompletedDateTime)
                .FirstOrDefaultAsync();

            // Check if there's a currently running orchestration
            var currentlyRunning = await _context.AdrOrchestrationRuns
                .Where(r => r.Status == "Running" || r.Status == "Queued")
                .OrderByDescending(r => r.RequestedDateTime)
                .FirstOrDefaultAsync();

            var response = new OrchestratorHealthResponse
            {
                Timestamp = DateTime.UtcNow,
                MaxHoursSinceLastRun = maxHoursSinceLastRun,
                CutoffTime = cutoffTime
            };

            // If there's a currently running orchestration, report as healthy (in progress)
            if (currentlyRunning != null)
            {
                response.Status = "Healthy";
                response.Message = $"Orchestrator is currently {currentlyRunning.Status.ToLower()}";
                response.IsCurrentlyRunning = true;
                response.CurrentRunRequestId = currentlyRunning.RequestId;
                response.CurrentRunStatus = currentlyRunning.Status;
                response.CurrentStep = currentlyRunning.CurrentStep;
                response.CurrentProgress = currentlyRunning.CurrentProgress;
                response.LastSuccessfulRunTime = lastSuccessfulRun?.CompletedDateTime;
                
                return Ok(response);
            }

            // Check if we have any successful runs
            if (lastSuccessfulRun == null)
            {
                response.Status = "Unhealthy";
                response.Message = "No successful orchestration runs found";
                response.LastRunTime = lastRun?.CompletedDateTime ?? lastRun?.RequestedDateTime;
                response.LastRunStatus = lastRun?.Status;
                
                _logger.LogWarning("Orchestrator health check: No successful runs found");
                return StatusCode(503, response);
            }

            // Check if the last successful run is within the expected timeframe
            var lastSuccessfulTime = lastSuccessfulRun.CompletedDateTime ?? lastSuccessfulRun.RequestedDateTime;
            response.LastSuccessfulRunTime = lastSuccessfulTime;
            response.LastRunTime = lastRun?.CompletedDateTime ?? lastRun?.RequestedDateTime;
            response.LastRunStatus = lastRun?.Status;
            response.LastRunRequestId = lastSuccessfulRun.RequestId;
            
            // Include stats from last successful run
            response.LastRunStats = new OrchestratorRunStats
            {
                AccountsSynced = lastSuccessfulRun.SyncAccountsTotal,
                JobsCreated = lastSuccessfulRun.JobsCreated,
                CredentialsVerified = lastSuccessfulRun.CredentialsVerified,
                ScrapingRequested = lastSuccessfulRun.ScrapingRequested,
                StatusesChecked = lastSuccessfulRun.StatusesChecked
            };

            if (lastSuccessfulTime >= cutoffTime)
            {
                var hoursSinceLastRun = (DateTime.UtcNow - lastSuccessfulTime).TotalHours;
                response.Status = "Healthy";
                response.Message = $"Orchestrator ran successfully {hoursSinceLastRun:F1} hours ago";
                response.HoursSinceLastSuccessfulRun = hoursSinceLastRun;
                
                return Ok(response);
            }
            else
            {
                var hoursSinceLastRun = (DateTime.UtcNow - lastSuccessfulTime).TotalHours;
                response.Status = "Unhealthy";
                response.Message = $"Orchestrator has not run successfully in {hoursSinceLastRun:F1} hours (threshold: {maxHoursSinceLastRun} hours)";
                response.HoursSinceLastSuccessfulRun = hoursSinceLastRun;
                
                // Check if the last run failed
                if (lastRun != null && lastRun.Status == "Failed")
                {
                    response.LastRunErrorMessage = lastRun.ErrorMessage;
                }
                
                _logger.LogWarning("Orchestrator health check: Last successful run was {Hours:F1} hours ago, threshold is {Threshold} hours",
                    hoursSinceLastRun, maxHoursSinceLastRun);
                
                return StatusCode(503, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestrator health check failed");
            return StatusCode(503, new OrchestratorHealthResponse
            {
                Status = "Unhealthy",
                Message = $"Health check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}

/// <summary>
/// Basic health check response
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Orchestrator health check response with detailed run information
/// </summary>
public class OrchestratorHealthResponse : HealthResponse
{
    public int MaxHoursSinceLastRun { get; set; }
    public DateTime CutoffTime { get; set; }
    public DateTime? LastSuccessfulRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunRequestId { get; set; }
    public string? LastRunErrorMessage { get; set; }
    public double? HoursSinceLastSuccessfulRun { get; set; }
    public bool IsCurrentlyRunning { get; set; }
    public string? CurrentRunRequestId { get; set; }
    public string? CurrentRunStatus { get; set; }
    public string? CurrentStep { get; set; }
    public string? CurrentProgress { get; set; }
    public OrchestratorRunStats? LastRunStats { get; set; }
}

/// <summary>
/// Statistics from the last orchestration run
/// </summary>
public class OrchestratorRunStats
{
    public int? AccountsSynced { get; set; }
    public int? JobsCreated { get; set; }
    public int? CredentialsVerified { get; set; }
    public int? ScrapingRequested { get; set; }
    public int? StatusesChecked { get; set; }
}
