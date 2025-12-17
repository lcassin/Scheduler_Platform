using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Services;

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

                await RunArchivalAsync(stoppingToken);
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

    public async Task RunArchivalAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data archival process...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        var config = await GetConfigurationAsync(dbContext);
        if (!config.IsArchivalEnabled)
        {
            _logger.LogInformation("Data archival is disabled in configuration. Skipping.");
            return;
        }

        var archivalDateTime = DateTime.UtcNow;
        var archivedBy = "System Archival";

        var jobCutoffDate = archivalDateTime.AddMonths(-config.JobRetentionMonths);
        var executionCutoffDate = archivalDateTime.AddMonths(-config.JobExecutionRetentionMonths);
        var auditLogCutoffDate = archivalDateTime.AddDays(-config.AuditLogRetentionDays);

        _logger.LogInformation(
            "Archival cutoff dates - Jobs: {JobCutoff}, Executions: {ExecCutoff}, AuditLogs: {AuditCutoff}",
            jobCutoffDate, executionCutoffDate, auditLogCutoffDate);

        var totalJobsArchived = await ArchiveAdrJobsAsync(dbContext, jobCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
        var totalExecutionsArchived = await ArchiveAdrJobExecutionsAsync(dbContext, executionCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
        var totalAuditLogsArchived = await ArchiveAuditLogsAsync(dbContext, auditLogCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);
        var totalScheduleExecutionsArchived = await ArchiveJobExecutionsAsync(dbContext, executionCutoffDate, archivalDateTime, archivedBy, config.ArchivalBatchSize, cancellationToken);

        _logger.LogInformation(
            "Data archival completed. AdrJobs: {Jobs}, AdrExecutions: {Executions}, AuditLogs: {AuditLogs}, ScheduleExecutions: {ScheduleExecs}",
            totalJobsArchived, totalExecutionsArchived, totalAuditLogsArchived, totalScheduleExecutionsArchived);
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
