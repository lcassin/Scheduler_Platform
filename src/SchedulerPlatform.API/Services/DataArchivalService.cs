using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Services;

/// <summary>
/// Result of a maintenance/archival operation.
/// </summary>
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

/// <summary>
/// Background service that archives old data based on configured retention periods.
/// Runs daily to move records older than the retention period to archive tables.
/// </summary>
public class DataArchivalService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataArchivalService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public DataArchivalService(
        IServiceProvider serviceProvider,
        ILogger<DataArchivalService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataArchivalService started. Will run daily at 2 AM UTC.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(2);
                if (now.Hour < 2)
                {
                    nextRun = now.Date.AddHours(2);
                }

                var delay = nextRun - now;
                _logger.LogInformation("Next archival run scheduled for {NextRun} (in {Delay})", nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                await RunMaintenanceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("DataArchivalService is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DataArchivalService. Will retry in 1 hour.");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Runs the complete maintenance process including archival, archive purge, and log cleanup.
    /// This method can be called by the background service or via API endpoint.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the maintenance operation.</returns>
    public async Task<MaintenanceResult> RunMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var result = new MaintenanceResult();
        
        _logger.LogInformation("Starting maintenance process...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var config = await GetConfigurationAsync(dbContext);

            if (!config.IsArchivalEnabled)
            {
                _logger.LogInformation("Data archival is disabled in configuration. Skipping archival steps.");
            }
            else
            {
                // Step 1: Archive old records
                var archivalResult = await RunArchivalAsync(dbContext, config, cancellationToken);
                result.AdrJobsArchived = archivalResult.AdrJobsArchived;
                result.AdrJobExecutionsArchived = archivalResult.AdrJobExecutionsArchived;
                result.AuditLogsArchived = archivalResult.AuditLogsArchived;
                result.ScheduleExecutionsArchived = archivalResult.ScheduleExecutionsArchived;

                // Step 2: Purge old archives (older than retention years)
                var purgeResult = await PurgeOldArchivesAsync(dbContext, config, cancellationToken);
                result.AdrJobArchivesPurged = purgeResult.AdrJobArchivesPurged;
                result.AdrJobExecutionArchivesPurged = purgeResult.AdrJobExecutionArchivesPurged;
                result.AuditLogArchivesPurged = purgeResult.AuditLogArchivesPurged;
                result.JobExecutionArchivesPurged = purgeResult.JobExecutionArchivesPurged;
            }

            // Step 3: Clean up old log files (for non-Azure environments)
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

    /// <summary>
    /// Runs the archival process to move old records to archive tables.
    /// </summary>
    private async Task<MaintenanceResult> RunArchivalAsync(SchedulerDbContext dbContext, AdrConfiguration config, CancellationToken cancellationToken)
    {
        var result = new MaintenanceResult();
        var archivalDateTime = DateTime.UtcNow;
        var archivedBy = "System Archival";

        var jobCutoffDate = archivalDateTime.AddMonths(-config.JobRetentionMonths);
        var executionCutoffDate = archivalDateTime.AddMonths(-config.JobExecutionRetentionMonths);
        var auditLogCutoffDate = archivalDateTime.AddDays(-config.AuditLogRetentionDays);

        _logger.LogInformation(
            "Archival cutoff dates - Jobs: {JobCutoff}, Executions: {ExecCutoff}, AuditLogs: {AuditCutoff}",
            jobCutoffDate, executionCutoffDate, auditLogCutoffDate);

        result.AdrJobsArchived = await ArchiveAdrJobsAsync(dbContext, jobCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
        result.AdrJobExecutionsArchived = await ArchiveAdrJobExecutionsAsync(dbContext, executionCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
        result.AuditLogsArchived = await ArchiveAuditLogsAsync(dbContext, auditLogCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
        result.ScheduleExecutionsArchived = await ArchiveJobExecutionsAsync(dbContext, executionCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);

        _logger.LogInformation(
            "Data archival completed. AdrJobs: {Jobs}, AdrExecutions: {Executions}, AuditLogs: {AuditLogs}, ScheduleExecutions: {ScheduleExecs}",
            result.AdrJobsArchived, result.AdrJobExecutionsArchived, result.AuditLogsArchived, result.ScheduleExecutionsArchived);

        return result;
    }

    /// <summary>
    /// Purges archive records older than the configured retention period (default 7 years).
    /// </summary>
    private async Task<MaintenanceResult> PurgeOldArchivesAsync(SchedulerDbContext dbContext, AdrConfiguration config, CancellationToken cancellationToken)
    {
        var result = new MaintenanceResult();
        var purgeCutoffDate = DateTime.UtcNow.AddYears(-config.ArchiveRetentionYears);

        _logger.LogInformation("Purging archives older than {CutoffDate} ({Years} years retention)", purgeCutoffDate, config.ArchiveRetentionYears);

        // Purge AdrJobArchive
        result.AdrJobArchivesPurged = await PurgeAdrJobArchivesAsync(dbContext, purgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
        
        // Purge AdrJobExecutionArchive
        result.AdrJobExecutionArchivesPurged = await PurgeAdrJobExecutionArchivesAsync(dbContext, purgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
        
        // Purge AuditLogArchive
        result.AuditLogArchivesPurged = await PurgeAuditLogArchivesAsync(dbContext, purgeCutoffDate, config.ArchivalBatchSize, cancellationToken);
        
        // Purge JobExecutionArchive
        result.JobExecutionArchivesPurged = await PurgeJobExecutionArchivesAsync(dbContext, purgeCutoffDate, config.ArchivalBatchSize, cancellationToken);

        _logger.LogInformation(
            "Archive purge completed. AdrJobArchives: {Jobs}, AdrJobExecutionArchives: {Execs}, AuditLogArchives: {Logs}, JobExecutionArchives: {SchedExecs}",
            result.AdrJobArchivesPurged, result.AdrJobExecutionArchivesPurged, result.AuditLogArchivesPurged, result.JobExecutionArchivesPurged);

        return result;
    }

    /// <summary>
    /// Cleans up old log files from the logs directory.
    /// Only applies to file-based logs in non-Azure environments.
    /// </summary>
    private Task<MaintenanceResult> CleanupLogFilesAsync(int retentionDays, CancellationToken cancellationToken)
    {
        var result = new MaintenanceResult();
        var cutoffDate = DateTime.Now.AddDays(-retentionDays);

        _logger.LogInformation("Cleaning up log files older than {CutoffDate} ({Days} days retention)", cutoffDate, retentionDays);

        try
        {
            // Get the application base directory and look for logs folder
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var logDirectories = new[]
            {
                Path.Combine(baseDirectory, "logs"),
                Path.Combine(baseDirectory, "..", "logs"),
                Path.Combine(baseDirectory, "..", "..", "logs")
            };

            foreach (var logDir in logDirectories)
            {
                if (!Directory.Exists(logDir))
                {
                    continue;
                }

                _logger.LogInformation("Scanning log directory: {LogDir}", logDir);

                var logFiles = Directory.GetFiles(logDir, "*.txt")
                    .Concat(Directory.GetFiles(logDir, "*.log"))
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.LastWriteTime < cutoffDate)
                    .ToList();

                foreach (var file in logFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var fileSize = file.Length;
                        file.Delete();
                        result.LogFilesDeleted++;
                        result.LogFilesBytesFreed += fileSize;
                        _logger.LogDebug("Deleted log file: {FileName} ({Size} bytes)", file.Name, fileSize);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete log file: {FileName}", file.Name);
                    }
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

    private async Task<int> PurgeAdrJobArchivesAsync(SchedulerDbContext dbContext, DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
                var archivesToPurge = await dbContext.AdrJobArchives
                    .Where(a => a.ArchivedDateTime < cutoffDate)
                    .OrderBy(a => a.AdrJobArchiveId)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            dbContext.AdrJobArchives.RemoveRange(archivesToPurge);
            await dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} AdrJobArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<int> PurgeAdrJobExecutionArchivesAsync(SchedulerDbContext dbContext, DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
                var archivesToPurge = await dbContext.AdrJobExecutionArchives
                    .Where(a => a.ArchivedDateTime < cutoffDate)
                    .OrderBy(a => a.AdrJobExecutionArchiveId)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            dbContext.AdrJobExecutionArchives.RemoveRange(archivesToPurge);
            await dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} AdrJobExecutionArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<int> PurgeAuditLogArchivesAsync(SchedulerDbContext dbContext, DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
                var archivesToPurge = await dbContext.AuditLogArchives
                    .Where(a => a.ArchivedDateTime < cutoffDate)
                    .OrderBy(a => a.AuditLogArchiveId)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            dbContext.AuditLogArchives.RemoveRange(archivesToPurge);
            await dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} AuditLogArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<int> PurgeJobExecutionArchivesAsync(SchedulerDbContext dbContext, DateTime cutoffDate, int batchSize, CancellationToken cancellationToken)
    {
        var totalPurged = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
                var archivesToPurge = await dbContext.JobExecutionArchives
                    .Where(a => a.ArchivedDateTime < cutoffDate)
                    .OrderBy(a => a.JobExecutionArchiveId)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

            if (archivesToPurge.Count == 0)
            {
                hasMore = false;
                continue;
            }

            dbContext.JobExecutionArchives.RemoveRange(archivesToPurge);
            await dbContext.SaveChangesAsync(cancellationToken);
            totalPurged += archivesToPurge.Count;

            _logger.LogInformation("Purged {Count} JobExecutionArchive records (total: {Total})", archivesToPurge.Count, totalPurged);

            if (archivesToPurge.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalPurged;
    }

    private async Task<AdrConfiguration> GetConfigurationAsync(SchedulerDbContext dbContext)
    {
        var config = await dbContext.AdrConfigurations
            .Where(c => !c.IsDeleted)
            .FirstOrDefaultAsync();

        return config ?? new AdrConfiguration();
    }

    private async Task<int> ArchiveAdrJobsAsync(
        SchedulerDbContext dbContext,
        DateTime cutoffDate,
        DateTime archivalDateTime,
        string archivedBy,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var jobsToArchive = await dbContext.AdrJobs
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
                    PrimaryVendorCode = job.PrimaryVendorCode,
                    MasterVendorCode = job.MasterVendorCode,
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

                dbContext.AdrJobArchives.Add(archive);
                dbContext.AdrJobs.Remove(job);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += jobsToArchive.Count;

            _logger.LogInformation("Archived {Count} AdrJob records (total: {Total})", jobsToArchive.Count, totalArchived);

            if (jobsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> ArchiveAdrJobExecutionsAsync(
        SchedulerDbContext dbContext,
        DateTime cutoffDate,
        DateTime archivalDateTime,
        string archivedBy,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var executionsToArchive = await dbContext.AdrJobExecutions
                .Where(e => !e.IsDeleted && e.CreatedDateTime < cutoffDate)
                .OrderBy(e => e.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (executionsToArchive.Count == 0)
            {
                hasMore = false;
                continue;
            }

            foreach (var execution in executionsToArchive)
            {
                var archive = new AdrJobExecutionArchive
                {
                    OriginalAdrJobExecutionId = execution.Id,
                    AdrJobId = execution.AdrJobId,
                    AdrRequestTypeId = execution.AdrRequestTypeId,
                    StartDateTime = execution.StartDateTime,
                    EndDateTime = execution.EndDateTime,
                    AdrStatusId = execution.AdrStatusId,
                    AdrStatusDescription = execution.AdrStatusDescription,
                    IsError = execution.IsError,
                    IsFinal = execution.IsFinal,
                    AdrIndexId = execution.AdrIndexId,
                    HttpStatusCode = execution.HttpStatusCode,
                    IsSuccess = execution.IsSuccess,
                    ErrorMessage = execution.ErrorMessage,
                    ApiResponse = execution.ApiResponse,
                    RequestPayload = execution.RequestPayload,
                    CreatedDateTime = execution.CreatedDateTime,
                    CreatedBy = execution.CreatedBy,
                    ModifiedDateTime = execution.ModifiedDateTime,
                    ModifiedBy = execution.ModifiedBy,
                    ArchivedDateTime = archivalDateTime,
                    ArchivedBy = archivedBy
                };

                dbContext.AdrJobExecutionArchives.Add(archive);
                dbContext.AdrJobExecutions.Remove(execution);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += executionsToArchive.Count;

            _logger.LogInformation("Archived {Count} AdrJobExecution records (total: {Total})", executionsToArchive.Count, totalArchived);

            if (executionsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> ArchiveAuditLogsAsync(
        SchedulerDbContext dbContext,
        DateTime cutoffDate,
        DateTime archivalDateTime,
        string archivedBy,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var logsToArchive = await dbContext.AuditLogs
                .Where(l => !l.IsDeleted && l.TimestampDateTime < cutoffDate)
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

                dbContext.AuditLogArchives.Add(archive);
                dbContext.AuditLogs.Remove(log);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += logsToArchive.Count;

            _logger.LogInformation("Archived {Count} AuditLog records (total: {Total})", logsToArchive.Count, totalArchived);

            if (logsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }

    private async Task<int> ArchiveJobExecutionsAsync(
        SchedulerDbContext dbContext,
        DateTime cutoffDate,
        DateTime archivalDateTime,
        string archivedBy,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var totalArchived = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var executionsToArchive = await dbContext.JobExecutions
                .Where(e => !e.IsDeleted && e.CreatedDateTime < cutoffDate)
                .OrderBy(e => e.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (executionsToArchive.Count == 0)
            {
                hasMore = false;
                continue;
            }

            foreach (var execution in executionsToArchive)
            {
                var archive = new JobExecutionArchive
                {
                    OriginalJobExecutionId = execution.Id,
                    ScheduleId = execution.ScheduleId,
                    StartDateTime = execution.StartDateTime,
                    EndDateTime = execution.EndDateTime,
                    Status = (int)execution.Status,
                    Output = execution.Output,
                    ErrorMessage = execution.ErrorMessage,
                    StackTrace = execution.StackTrace,
                    RetryCount = execution.RetryCount,
                    DurationSeconds = execution.DurationSeconds,
                    TriggeredBy = execution.TriggeredBy,
                    CancelledBy = execution.CancelledBy,
                    CreatedDateTime = execution.CreatedDateTime,
                    CreatedBy = execution.CreatedBy,
                    ModifiedDateTime = execution.ModifiedDateTime,
                    ModifiedBy = execution.ModifiedBy,
                    ArchivedDateTime = archivalDateTime,
                    ArchivedBy = archivedBy
                };

                dbContext.JobExecutionArchives.Add(archive);
                dbContext.JobExecutions.Remove(execution);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            totalArchived += executionsToArchive.Count;

            _logger.LogInformation("Archived {Count} JobExecution (Schedule) records (total: {Total})", executionsToArchive.Count, totalArchived);

            if (executionsToArchive.Count < batchSize)
            {
                hasMore = false;
            }
        }

        return totalArchived;
    }
}
