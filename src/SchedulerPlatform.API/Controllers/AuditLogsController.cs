using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.API.Models;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for retrieving audit logs.
/// Provides endpoints for viewing audit history of schedules and other entities.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly SchedulerDbContext _dbContext;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(IUnitOfWork unitOfWork, SchedulerDbContext dbContext, ILogger<AuditLogsController> logger)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves audit logs for a specific schedule with optional filtering and pagination.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 50).</param>
    /// <param name="eventType">Optional filter by event type.</param>
    /// <param name="action">Optional filter by action.</param>
    /// <param name="startDate">Optional start date for filtering.</param>
    /// <param name="endDate">Optional end date for filtering.</param>
    /// <returns>A paginated list of audit logs for the schedule.</returns>
    /// <response code="200">Returns the paginated audit logs.</response>
    /// <response code="404">The schedule was not found.</response>
    /// <response code="500">An error occurred while retrieving audit logs.</response>
    [HttpGet("schedules/{scheduleId}")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> GetScheduleAuditLogs(
        int scheduleId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? eventType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound($"Schedule with ID {scheduleId} not found");
            }

            var query = _dbContext.Set<SchedulerPlatform.Core.Domain.Entities.AuditLog>()
                .Where(a => a.EntityType == "Schedule" && a.EntityId == scheduleId);

            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(a => a.EventType == eventType);
            }

            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.TimestampDateTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.TimestampDateTime <= endDate.Value);
            }

            var totalCount = await query.CountAsync();

            var auditLogs = await query
                .OrderByDescending(a => a.TimestampDateTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogResponse
                {
                    Id = a.Id,
                    EventType = a.EventType,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    Action = a.Action,
                    UserName = a.UserName,
                    ClientId = a.ClientId,
                    IpAddress = a.IpAddress,
                    UserAgent = a.UserAgent,
                    TimestampDateTime = a.TimestampDateTime,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    AdditionalData = a.AdditionalData
                })
                .ToListAsync();

            return Ok(new PagedResponse<AuditLogResponse>(auditLogs, totalCount, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Retrieves all audit logs with optional filtering and pagination. Requires Users.Manage.Read policy.
    /// </summary>
    /// <param name="entityType">Optional filter by entity type (e.g., "Schedule", "Client").</param>
    /// <param name="entityId">Optional filter by entity ID.</param>
    /// <param name="eventType">Optional filter by event type.</param>
    /// <param name="action">Optional filter by action.</param>
    /// <param name="userName">Optional filter by user name (partial match).</param>
    /// <param name="startDate">Optional start date for filtering.</param>
    /// <param name="endDate">Optional end date for filtering.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 50).</param>
    /// <returns>A paginated list of audit logs.</returns>
    /// <response code="200">Returns the paginated audit logs.</response>
    /// <response code="500">An error occurred while retrieving audit logs.</response>
    [HttpGet]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? action = null,
        [FromQuery] string? userName = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _dbContext.Set<SchedulerPlatform.Core.Domain.Entities.AuditLog>().AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(a => a.EntityType == entityType);
            }

            if (entityId.HasValue)
            {
                query = query.Where(a => a.EntityId == entityId);
            }

            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(a => a.EventType == eventType);
            }

            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (!string.IsNullOrEmpty(userName))
            {
                query = query.Where(a => a.UserName.Contains(userName));
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.TimestampDateTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.TimestampDateTime <= endDate.Value);
            }

            var totalCount = await query.CountAsync();

            var auditLogs = await query
                .OrderByDescending(a => a.TimestampDateTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogResponse
                {
                    Id = a.Id,
                    EventType = a.EventType,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    Action = a.Action,
                    UserName = a.UserName,
                    ClientId = a.ClientId,
                    IpAddress = a.IpAddress,
                    UserAgent = a.UserAgent,
                    TimestampDateTime = a.TimestampDateTime,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    AdditionalData = a.AdditionalData
                })
                .ToListAsync();

            return Ok(new PagedResponse<AuditLogResponse>(auditLogs, totalCount, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }
}
