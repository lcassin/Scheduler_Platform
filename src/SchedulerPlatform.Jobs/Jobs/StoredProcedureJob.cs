using Microsoft.Extensions.Logging;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text.Json;

namespace SchedulerPlatform.Jobs.Jobs;

[DisallowConcurrentExecution]
public class StoredProcedureJob : IJob
{
    private readonly ILogger<StoredProcedureJob> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ISchedulerService _schedulerService;

    public StoredProcedureJob(ILogger<StoredProcedureJob> logger, IUnitOfWork unitOfWork, IEmailService emailService, ISchedulerService schedulerService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
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

        var jobExecution = new JobExecution
        {
            ScheduleId = scheduleId,
            StartTime = DateTime.UtcNow,
            Status = JobStatus.Running,
            TriggeredBy = triggeredBy ?? "Scheduler"
        };

        await _unitOfWork.JobExecutions.AddAsync(jobExecution);
        await _unitOfWork.SaveChangesAsync();

        int timeoutSeconds = 300;

        try
        {
            var jobConfig = JsonSerializer.Deserialize<StoredProcedureJobConfig>(
                schedule.JobConfiguration ?? "{}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrEmpty(jobConfig?.ConnectionString))
            {
                throw new Exception("Database connection string is not configured");
            }

            if (string.IsNullOrEmpty(jobConfig?.ProcedureName))
            {
                throw new Exception("Stored procedure name is not configured");
            }

            var jobParameters = await _unitOfWork.JobParameters.FindAsync(jp => jp.ScheduleId == scheduleId);
            
            var stopwatch = Stopwatch.StartNew();
            object? result = null;

            var connectionString = jobConfig.ConnectionString;
            if (!connectionString.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase) &&
                !connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
            {
                connectionString += ";TrustServerCertificate=True";
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                using (var command = new SqlCommand(jobConfig.ProcedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    timeoutSeconds = schedule.TimeoutMinutes.HasValue 
                        ? schedule.TimeoutMinutes.Value * 60 
                        : (jobConfig.TimeoutSeconds > 0 ? jobConfig.TimeoutSeconds : 300);
                    command.CommandTimeout = timeoutSeconds;
                    
                    foreach (var param in jobParameters)
                    {
                        string paramValue = param.ParameterValue ?? string.Empty;
                        
                        if (param.IsDynamic && !string.IsNullOrEmpty(param.SourceQuery) && 
                            !string.IsNullOrEmpty(param.SourceConnectionString))
                        {
                            var sourceConnString = param.SourceConnectionString;
                            
                            if (!sourceConnString.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase) &&
                                !sourceConnString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
                            {
                                sourceConnString += ";TrustServerCertificate=True";
                            }
                            
                            using (var sourceConn = new SqlConnection(sourceConnString))
                            {
                                await sourceConn.OpenAsync();
                                using (var sourceCmd = new SqlCommand(param.SourceQuery, sourceConn))
                                {
                                    var sourceResult = await sourceCmd.ExecuteScalarAsync();
                                    if (sourceResult != null)
                                    {
                                        paramValue = sourceResult.ToString() ?? string.Empty;
                                    }
                                }
                            }
                        }
                        
                        SqlParameter sqlParam = new SqlParameter($"@{param.ParameterName}", paramValue);
                        SetParameterType(sqlParam, param.ParameterType);
                        command.Parameters.Add(sqlParam);
                    }
                    
                    if (jobConfig.ReturnValue)
                    {
                        result = await command.ExecuteScalarAsync();
                    }
                    else
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }

            stopwatch.Stop();

            jobExecution.Status = JobStatus.Completed;
            jobExecution.EndTime = DateTime.UtcNow;
            jobExecution.Output = result?.ToString() ?? "Stored procedure executed successfully.";
            jobExecution.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;

            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();

            await _emailService.SendJobExecutionNotificationAsync(jobExecution.Id, true);

            _logger.LogInformation("Stored procedure {ProcedureName} completed successfully, Duration: {Duration}s",
                jobConfig.ProcedureName, stopwatch.Elapsed.TotalSeconds);
            
            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update NextRunTime for schedule {ScheduleId} after successful execution", scheduleId);
            }
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            _logger.LogWarning(ex, "Stored procedure timed out for schedule {ScheduleId}", scheduleId);
            
            jobExecution.Status = JobStatus.Cancelled;
            jobExecution.EndTime = DateTime.UtcNow;
            jobExecution.ErrorMessage = $"Stored procedure execution timed out after {timeoutSeconds} seconds";
            
            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();
            
            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception updateEx)
            {
                _logger.LogWarning(updateEx, "Failed to update NextRunTime for schedule {ScheduleId} after timeout", scheduleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing stored procedure job for schedule {ScheduleId}", scheduleId);

            jobExecution.Status = JobStatus.Failed;
            jobExecution.EndTime = DateTime.UtcNow;
            jobExecution.ErrorMessage = ex.Message;
            jobExecution.StackTrace = ex.StackTrace;
            
            if (schedule.MaxRetries > 0 && jobExecution.RetryCount < schedule.MaxRetries)
            {
                int retryDelayMinutes = schedule.RetryDelayMinutes > 0 
                    ? (int)Math.Pow(2, jobExecution.RetryCount) * schedule.RetryDelayMinutes 
                    : 5; // Default exponential backoff

                DateTimeOffset retryTime = DateTimeOffset.UtcNow.AddMinutes(retryDelayMinutes);
                
                _logger.LogInformation(
                    "Scheduling retry {RetryCount} of {MaxRetries} for job {ScheduleId} at {RetryTime}", 
                    jobExecution.RetryCount + 1, schedule.MaxRetries, scheduleId, retryTime);

                try
                {
                    ITrigger retryTrigger = TriggerBuilder.Create()
                        .ForJob(context.JobDetail.Key)
                        .WithIdentity($"Retry_{scheduleId}_{jobExecution.RetryCount + 1}")
                        .StartAt(retryTime)
                        .UsingJobData("RetryCount", jobExecution.RetryCount + 1)
                        .UsingJobData("TriggeredBy", "RetryMechanism")
                        .Build();

                    await context.Scheduler.ScheduleJob(retryTrigger);
                    
                    _logger.LogInformation(
                        "Successfully scheduled retry {RetryCount} for schedule {ScheduleId}",
                        jobExecution.RetryCount + 1, scheduleId);
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx,
                        "Failed to schedule retry for schedule {ScheduleId} (Name: {ScheduleName}). " +
                        "JobKey: {JobKey}, TriggerIdentity: Retry_{ScheduleId}_{RetryCount}, " +
                        "RetryTime: {RetryTime}. Job execution will be marked as failed without retry.",
                        scheduleId, schedule.Name, context.JobDetail.Key, scheduleId, 
                        jobExecution.RetryCount + 1, retryTime);
                    
                    jobExecution.ErrorMessage += $"\n\nAdditional Error: Failed to schedule retry - {retryEx.Message}";
                    
                    if (retryEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) || 
                        retryEx.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                        retryEx.Message.Contains("ObjectAlreadyExistsException", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Retry trigger already exists for schedule {ScheduleId}. " +
                            "This may indicate stale Quartz data or a previously failed retry attempt. " +
                            "Consider cleaning up orphaned triggers in the Quartz database.",
                            scheduleId);
                    }
                }
            }

            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();

            await _emailService.SendJobExecutionNotificationAsync(jobExecution.Id, false);
            
            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception updateEx)
            {
                _logger.LogWarning(updateEx, "Failed to update NextRunTime for schedule {ScheduleId} after failed execution", scheduleId);
            }
        }
    }
    
    private void SetParameterType(SqlParameter parameter, string parameterType)
    {
        switch (parameterType.ToLowerInvariant())
        {
            case "int":
                parameter.SqlDbType = SqlDbType.Int;
                if (int.TryParse(parameter.Value.ToString(), out int intVal))
                    parameter.Value = intVal;
                break;
            case "datetime":
                parameter.SqlDbType = SqlDbType.DateTime;
                if (DateTime.TryParse(parameter.Value.ToString(), out DateTime dateVal))
                    parameter.Value = dateVal;
                break;
            case "bit":
                parameter.SqlDbType = SqlDbType.Bit;
                if (bool.TryParse(parameter.Value.ToString(), out bool boolVal))
                    parameter.Value = boolVal;
                break;
            case "decimal":
                parameter.SqlDbType = SqlDbType.Decimal;
                if (decimal.TryParse(parameter.Value.ToString(), out decimal decimalVal))
                    parameter.Value = decimalVal;
                break;
            default: // Default to NVARCHAR
                parameter.SqlDbType = SqlDbType.NVarChar;
                break;
        }
    }
}

public class StoredProcedureJobConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300; // Default 5 minutes
    public bool ReturnValue { get; set; } = false;
}
