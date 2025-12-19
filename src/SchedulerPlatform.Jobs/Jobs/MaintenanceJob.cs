using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Jobs.Services;

namespace SchedulerPlatform.Jobs.Jobs;

/// <summary>
/// Quartz job that performs system maintenance tasks including:
/// - Archiving old records to archive tables
/// - Purging archives older than retention period
/// - Cleaning up old log files
/// This job should be configured as a system schedule that runs daily.
/// </summary>
[DisallowConcurrentExecution]
public class MaintenanceJob : IJob
{
    private readonly ILogger<MaintenanceJob> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SchedulerDbContext _dbContext;
    private readonly ISchedulerService _schedulerService;

    public MaintenanceJob(
        ILogger<MaintenanceJob> logger,
        IUnitOfWork unitOfWork,
        SchedulerDbContext dbContext,
        ISchedulerService schedulerService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _schedulerService = schedulerService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobDataMap = context.MergedJobDataMap;
        int scheduleId = jobDataMap.GetInt("ScheduleId");
        string? triggeredBy = jobDataMap.ContainsKey("TriggeredBy")
            ? jobDataMap.GetString("TriggeredBy")
            : null;

        var schedule = await _unitOfWork.Schedules.GetByIdAsync(scheduleId);
        if (schedule == null)
        {
            _logger.LogError("Schedule with ID {ScheduleId} not found", scheduleId);
            return;
        }

        if (!schedule.IsEnabled)
        {
            _logger.LogWarning("Schedule {ScheduleId} ({ScheduleName}) is disabled, skipping execution",
                scheduleId, schedule.Name);
            return;
        }

        var execNow = DateTime.UtcNow;
        var jobExecution = new JobExecution
        {
            ScheduleId = scheduleId,
            StartDateTime = execNow,
            Status = JobStatus.Running,
            TriggeredBy = triggeredBy ?? "Scheduler",
            CreatedDateTime = execNow,
            CreatedBy = "System",
            ModifiedDateTime = execNow,
            ModifiedBy = "System"
        };

        await _unitOfWork.JobExecutions.AddAsync(jobExecution);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            _logger.LogInformation("Starting maintenance job execution for schedule {ScheduleId}", scheduleId);

            var result = await RunMaintenanceAsync(context.CancellationToken);

            jobExecution.Status = result.Success ? JobStatus.Completed : JobStatus.Failed;
            jobExecution.EndDateTime = DateTime.UtcNow;
            jobExecution.DurationSeconds = (int)(DateTime.UtcNow - execNow).TotalSeconds;
            jobExecution.Output = FormatMaintenanceResult(result);
            
            if (!result.Success)
            {
                jobExecution.ErrorMessage = result.ErrorMessage;
            }

            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Maintenance job completed. Success: {Success}", result.Success);

            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update NextRunTime for schedule {ScheduleId}", scheduleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing maintenance job for schedule {ScheduleId}", scheduleId);

            jobExecution.Status = JobStatus.Failed;
            jobExecution.EndDateTime = DateTime.UtcNow;
            jobExecution.ErrorMessage = ex.Message;
            jobExecution.StackTrace = ex.StackTrace;

            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception updateEx)
            {
                _logger.LogWarning(updateEx, "Failed to update NextRunTime for schedule {ScheduleId}", scheduleId);
            }
        }
    }

    private async Task<MaintenanceResult> RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        var result = new MaintenanceResult();

        _logger.LogInformation("Starting maintenance process...");

        try
        {
            var config = await GetConfigurationAsync();

            if (!config.IsArchivalEnabled)
            {
                _logger.LogInformation("Data archival is disabled in configuration. Skipping archival steps.");
            }
            else
            {
                var archivalDateTime = DateTime.UtcNow;
                var archivedBy = "System Maintenance Job";

                var jobCutoffDate = archivalDateTime.AddMonths(-config.JobRetentionMonths);
                var executionCutoffDate = archivalDateTime.AddMonths(-config.JobExecutionRetentionMonths);
                var auditLogCutoffDate = archivalDateTime.AddDays(-config.AuditLogRetentionDays);

                result.AdrJobsArchived = await ArchiveAdrJobsAsync(jobCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
                result.AdrJobExecutionsArchived = await ArchiveAdrJobExecutionsAsync(executionCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
                result.AuditLogsArchived = await ArchiveAuditLogsAsync(auditLogCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
                result.ScheduleExecutionsArchived = await ArchiveJobExecutionsAsync(executionCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);

                var archivePurgeCutoffDate = archivalDateTime.AddYears(-config.ArchiveRetentionYears);
                result.AdrJobArchivesPurged = await PurgeAdrJobArchivesAsync(archivePurgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
                result.AdrJobExecutionArchivesPurged = await PurgeAdrJobExecutionArchivesAsync(archivePurgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
                result.AuditLogArchivesPurged = await PurgeAuditLogArchivesAsync(archivePurgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
                result.JobExecutionArchivesPurged = await PurgeJobExecutionArchivesAsync(archivePurgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
            }

            var logCleanupResult = await CleanupLogFilesAsync(config.LogRetentionDays, cancellationToken);
            result.LogFilesDeleted = logCleanupResult.LogFilesDeleted;
            result.LogFilesBytesFreed = logCleanupResult.LogFilesBytesFreed;

            _logger.LogInformation(
                "Maintenance completed. Archived: Jobs={Jobs}, Executions={Execs}, AuditLogs={Logs}, ScheduleExecs={SchedExecs}. " +
                "Purged: JobArchives={JobArch}, ExecArchives={ExecArch}, LogArchives={LogArch}, SchedExecArchives={SchedArch}. " +
                "Log files deleted: {LogFiles} ({LogBytes:N2} MB freed)",
                result.AdrJobsArchived, result.AdrJobExecutionsArchived, result.AuditLogsArchived, result.ScheduleExecutionsArchived,
                result.AdrJobArchivesPurged, result.AdrJobExecutionArchivesPurged, result.AuditLogArchivesPurged, result.JobExecutionArchivesPurged,
                result.LogFilesDeleted, result.LogFilesBytesFreed / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during maintenance process");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<AdrConfiguration> GetConfigurationAsync()
    {
        var config = await _dbContext.AdrConfigurations
            .Where(c => !c.IsDeleted)
            .FirstOrDefaultAsync();

        return config ?? new AdrConfiguration();
    }

    private async Task<int> ArchiveAdrJobsAsync(DateTime cutoffDate, DateTime archivalDateTime, string archivedBy, int batchSize, CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var jobsToArchive = await _dbContext.AdrJobs
                .Where(j => !j.IsDeleted && j.CreatedDateTime < cutoffDate)
                .OrderBy(j => j.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (jobsToArchive.Count == 0)
            {
                hasMore = false;
                continue;
            }

            foreach (var job in jobsToArchive)
            {
                var archive = new AdrJobArchive
                {
                    OriginalAdrJobId = job.Id,
                    AdrAccountId = job.AdrAccountId,
                    AdrAccountRuleId = job.AdrAccountRuleId,
                    VMAccountId = job.VMAccountId,
                    VMAccountNumber = job.VMAccountNumber,
                    VendorCode = job.VendorCode,
                    CredentialId = job.CredentialId,
                    PeriodType = job.PeriodType,
                    BillingPeriodStartDateTime = job.BillingPeriodStartDateTime,
                    BillingPeriodEndDateTime = job.BillingPeriodEndDateTime,
                    NextRunDateTime = job.NextRunDateTime,
                    NextRangeStartDateTime = job.NextRangeStartDateTime,
                    NextRangeEndDateTime = job.NextRangeEndDateTime,
                    Status = job.Status,
                    IsMissing = job.IsMissing,
                    AdrStatusId = job.AdrStatusId,
                    AdrStatusDescription = job.AdrStatusDescription,
                    AdrIndexId = job.AdrIndexId,
                    CredentialVerifiedDateTime = job.CredentialVerifiedDateTime,
                    ScrapingCompletedDateTime = job.ScrapingCompletedDateTime,
                    ErrorMessage = job.ErrorMessage,
                    RetryCount = job.RetryCount,
                    IsManualRequest = job.IsManualRequest,
                    ManualRequestReason = job.ManualRequestReason,
                    LastStatusCheckResponse = job.LastStatusCheckResponse,
                    LastStatusCheckDateTime = job.LastStatusCheckDateTime,
                    CreatedDateTime = job.CreatedDateTime,
                    CreatedBy = job.CreatedBy,
                    ModifiedDateTime = job.ModifiedDateTime,
                    ModifiedBy = job.ModifiedBy,
                    ArchivedDateTime = archivalDateTime,
                    ArchivedBy = archivedBy
                };

                _dbContext.AdrJobArchives.Add(archive);
                job.IsDeleted = true;
                job.ModifiedDateTime = archivalDateTime;
                job.ModifiedBy = archivedBy;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += jobsToArchive.Count;

            _logger.LogInformation("Archived {Count} AdrJob records (total: {Total})", jobsToArchive.Count, totalArchived);

            if (jobsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> ArchiveAdrJobExecutionsAsync(DateTime cutoffDate, DateTime archivalDateTime, string archivedBy, int batchSize, CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var executionsToArchive = await _dbContext.AdrJobExecutions
                .Where(e => !e.IsDeleted && e.CreatedDateTime < cutoffDate)
                .OrderBy(e => e.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (executionsToArchive.Count == 0)
            {
                hasMore = false;
                continue;
            }

            foreach (var exec in executionsToArchive)
            {
                var archive = new AdrJobExecutionArchive
                {
                    OriginalAdrJobExecutionId = exec.Id,
                    AdrJobId = exec.AdrJobId,
                    AdrRequestTypeId = exec.AdrRequestTypeId,
                    StartDateTime = exec.StartDateTime,
                    EndDateTime = exec.EndDateTime,
                    AdrStatusId = exec.AdrStatusId,
                    AdrStatusDescription = exec.AdrStatusDescription,
                    IsError = exec.IsError,
                    IsFinal = exec.IsFinal,
                    AdrIndexId = exec.AdrIndexId,
                    HttpStatusCode = exec.HttpStatusCode,
                    IsSuccess = exec.IsSuccess,
                    ErrorMessage = exec.ErrorMessage,
                    ApiResponse = exec.ApiResponse,
                    RequestPayload = exec.RequestPayload,
                    CreatedDateTime = exec.CreatedDateTime,
                    CreatedBy = exec.CreatedBy,
                    ModifiedDateTime = exec.ModifiedDateTime,
                    ModifiedBy = exec.ModifiedBy,
                    ArchivedDateTime = archivalDateTime,
                    ArchivedBy = archivedBy
                };

                _dbContext.AdrJobExecutionArchives.Add(archive);
                exec.IsDeleted = true;
                exec.ModifiedDateTime = archivalDateTime;
                exec.ModifiedBy = archivedBy;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += executionsToArchive.Count;

            _logger.LogInformation("Archived {Count} AdrJobExecution records (total: {Total})", executionsToArchive.Count, totalArchived);

            if (executionsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> ArchiveAuditLogsAsync(DateTime cutoffDate, DateTime archivalDateTime, string archivedBy, int batchSize, CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var logsToArchive = await _dbContext.AuditLogs
                .Where(l => !l.IsDeleted && l.CreatedDateTime < cutoffDate)
                .OrderBy(l => l.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (logsToArchive.Count == 0)
            {
                hasMore = false;
                continue;
            }

            foreach (var log in logsToArchive)
            {
                var archive = new AuditLogArchive
                {
                    OriginalAuditLogId = log.Id,
                    EventType = log.EventType,
                    EntityType = log.EntityType,
                    EntityId = log.EntityId,
                    Action = log.Action,
                    OldValues = log.OldValues,
                    NewValues = log.NewValues,
                    UserName = log.UserName,
                    ClientId = log.ClientId,
                    IpAddress = log.IpAddress,
                    UserAgent = log.UserAgent,
                    TimestampDateTime = log.TimestampDateTime,
                    AdditionalData = log.AdditionalData,
                    CreatedDateTime = log.CreatedDateTime,
                    CreatedBy = log.CreatedBy,
                    ModifiedDateTime = log.ModifiedDateTime,
                    ModifiedBy = log.ModifiedBy,
                    ArchivedDateTime = archivalDateTime,
                    ArchivedBy = archivedBy
                };

                _dbContext.AuditLogArchives.Add(archive);
                log.IsDeleted = true;
                log.ModifiedDateTime = archivalDateTime;
                log.ModifiedBy = archivedBy;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += logsToArchive.Count;

            _logger.LogInformation("Archived {Count} AuditLog records (total: {Total})", logsToArchive.Count, totalArchived);

            if (logsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> ArchiveJobExecutionsAsync(DateTime cutoffDate, DateTime archivalDateTime, string archivedBy, int batchSize, CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var executionsToArchive = await _dbContext.JobExecutions
                .Where(e => !e.IsDeleted && e.CreatedDateTime < cutoffDate)
                .OrderBy(e => e.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (executionsToArchive.Count == 0)
            {
                hasMore = false;
                continue;
            }

            foreach (var exec in executionsToArchive)
            {
                var archive = new JobExecutionArchive
                {
                    OriginalJobExecutionId = exec.Id,
                    ScheduleId = exec.ScheduleId,
                    StartDateTime = exec.StartDateTime,
                    EndDateTime = exec.EndDateTime,
                    Status = (int)exec.Status,
                    Output = exec.Output,
                    ErrorMessage = exec.ErrorMessage,
                    StackTrace = exec.StackTrace,
                    RetryCount = exec.RetryCount,
                    DurationSeconds = exec.DurationSeconds,
                    TriggeredBy = exec.TriggeredBy,
                    CancelledBy = exec.CancelledBy,
                    CreatedDateTime = exec.CreatedDateTime,
                    CreatedBy = exec.CreatedBy,
                    ModifiedDateTime = exec.ModifiedDateTime,
                    ModifiedBy = exec.ModifiedBy,
                    ArchivedDateTime = archivalDateTime,
                    ArchivedBy = archivedBy
                };

                _dbContext.JobExecutionArchives.Add(archive);
                exec.IsDeleted = true;
                exec.ModifiedDateTime = archivalDateTime;
                exec.ModifiedBy = archivedBy;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += executionsToArchive.Count;

            _logger.LogInformation("Archived {Count} JobExecution records (total: {Total})", executionsToArchive.Count, totalArchived);

            if (executionsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> PurgeAdrJobArchivesAsync(DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var archivesToPurge = await _dbContext.AdrJobArchives
                .Where(a => a.ArchivedDateTime < cutoffDate)
                .OrderBy(a => a.AdrJobArchiveId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            _dbContext.AdrJobArchives.RemoveRange(archivesToPurge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} AdrJobArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<int> PurgeAdrJobExecutionArchivesAsync(DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var archivesToPurge = await _dbContext.AdrJobExecutionArchives
                .Where(a => a.ArchivedDateTime < cutoffDate)
                .OrderBy(a => a.AdrJobExecutionArchiveId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            _dbContext.AdrJobExecutionArchives.RemoveRange(archivesToPurge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} AdrJobExecutionArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<int> PurgeAuditLogArchivesAsync(DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var archivesToPurge = await _dbContext.AuditLogArchives
                .Where(a => a.ArchivedDateTime < cutoffDate)
                .OrderBy(a => a.AuditLogArchiveId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            _dbContext.AuditLogArchives.RemoveRange(archivesToPurge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} AuditLogArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<int> PurgeJobExecutionArchivesAsync(DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var archivesToPurge = await _dbContext.JobExecutionArchives
                .Where(a => a.ArchivedDateTime < cutoffDate)
                .OrderBy(a => a.JobExecutionArchiveId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            _dbContext.JobExecutionArchives.RemoveRange(archivesToPurge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} JobExecutionArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private Task<MaintenanceResult> CleanupLogFilesAsync(int retentionDays, CancellationToken cancellationToken)
    {
        var result = new MaintenanceResult();

        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logPath))
            {
                _logger.LogInformation("Log directory does not exist at {Path}, skipping log cleanup", logPath);
                return Task.FromResult(result);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var logFiles = Directory.GetFiles(logPath, "*.log", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(logPath, "*.txt", SearchOption.AllDirectories));

            foreach (var filePath in logFiles)
            {
                try
                {
                    var file = new FileInfo(filePath);
                    if (file.LastWriteTimeUtc < cutoffDate)
                    {
                        var fileSize = file.Length;
                        file.Delete();
                        result.LogFilesDeleted++;
                        result.LogFilesBytesFreed += fileSize;
                        _logger.LogDebug("Deleted log file: {FileName}", file.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete log file: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Log cleanup completed. Deleted {Count} files, freed {Bytes:N2} MB",
                result.LogFilesDeleted, result.LogFilesBytesFreed / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during log file cleanup (non-fatal)");
        }

        return Task.FromResult(result);
    }

    private string FormatMaintenanceResult(MaintenanceResult result)
    {
        return $"Maintenance Job Results:\n" +
               $"========================\n" +
               $"Records Archived:\n" +
               $"  - ADR Jobs: {result.AdrJobsArchived}\n" +
               $"  - ADR Job Executions: {result.AdrJobExecutionsArchived}\n" +
               $"  - Audit Logs: {result.AuditLogsArchived}\n" +
               $"  - Schedule Executions: {result.ScheduleExecutionsArchived}\n" +
               $"\nArchives Purged:\n" +
               $"  - ADR Job Archives: {result.AdrJobArchivesPurged}\n" +
               $"  - ADR Job Execution Archives: {result.AdrJobExecutionArchivesPurged}\n" +
               $"  - Audit Log Archives: {result.AuditLogArchivesPurged}\n" +
               $"  - Job Execution Archives: {result.JobExecutionArchivesPurged}\n" +
               $"\nLog Cleanup:\n" +
               $"  - Files Deleted: {result.LogFilesDeleted}\n" +
               $"  - Space Freed: {result.LogFilesBytesFreed / (1024.0 * 1024.0):N2} MB\n" +
               $"\nStatus: {(result.Success ? "Success" : "Failed")}" +
               (result.ErrorMessage != null ? $"\nError: {result.ErrorMessage}" : "");
    }
}

public class MaintenanceResult
{
    public int AdrJobsArchived { get; set; }
    public int AdrJobExecutionsArchived { get; set; }
    public int AuditLogsArchived { get; set; }
    public int ScheduleExecutionsArchived { get; set; }
    public int AdrJobArchivesPurged { get; set; }
    public int AdrJobExecutionArchivesPurged { get; set; }
    public int AuditLogArchivesPurged { get; set; }
    public int JobExecutionArchivesPurged { get; set; }
    public int LogFilesDeleted { get; set; }
    public long LogFilesBytesFreed { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Success { get; set; } = true;
}
