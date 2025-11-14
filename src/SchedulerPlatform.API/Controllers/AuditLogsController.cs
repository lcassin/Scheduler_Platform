using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Controllers;

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

    [HttpGet("schedules/{scheduleId}")]
    public async Task<ActionResult<object>> GetScheduleAuditLogs(
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
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= endDate.Value);
            }

            var totalCount = await query.CountAsync();

            var auditLogs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.EventType,
                    a.EntityType,
                    a.EntityId,
                    a.Action,
                    a.UserName,
                    a.ClientId,
                    a.IpAddress,
                    a.UserAgent,
                    a.Timestamp,
                    a.OldValues,
                    a.NewValues,
                    a.AdditionalData
                })
                .ToListAsync();

            return Ok(new
            {
                items = auditLogs,
                totalCount = totalCount,
                pageNumber = pageNumber,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    [HttpGet]
    [Authorize(Policy = "Users.Manage")]
    public async Task<ActionResult<object>> GetAuditLogs(
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
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= endDate.Value);
            }

            var totalCount = await query.CountAsync();

            var auditLogs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.EventType,
                    a.EntityType,
                    a.EntityId,
                    a.Action,
                    a.UserName,
                    a.ClientId,
                    a.IpAddress,
                    a.UserAgent,
                    a.Timestamp,
                    a.OldValues,
                    a.NewValues,
                    a.AdditionalData
                })
                .ToListAsync();

            return Ok(new
            {
                items = auditLogs,
                totalCount = totalCount,
                pageNumber = pageNumber,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }
}
