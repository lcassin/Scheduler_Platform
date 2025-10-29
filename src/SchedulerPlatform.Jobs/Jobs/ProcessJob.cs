using Microsoft.Extensions.Logging;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;
using System.Diagnostics;
using System.Text;

namespace SchedulerPlatform.Jobs.Jobs;

[DisallowConcurrentExecution]
public class ProcessJob : IJob
{
    private readonly ILogger<ProcessJob> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ISchedulerService _schedulerService;

    public ProcessJob(ILogger<ProcessJob> logger, IUnitOfWork unitOfWork, IEmailService emailService, ISchedulerService schedulerService)
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

        try
        {
            var jobConfig = System.Text.Json.JsonSerializer.Deserialize<ProcessJobConfig>(
                schedule.JobConfiguration ?? "{}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrEmpty(jobConfig?.ExecutablePath))
            {
                throw new Exception("Executable path is not configured");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = jobConfig.ExecutablePath,
                Arguments = jobConfig.Arguments ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = jobConfig.WorkingDirectory ?? Path.GetDirectoryName(jobConfig.ExecutablePath) ?? string.Empty
            };

            if (jobConfig.EnvironmentVariables != null)
            {
                foreach (var envVar in jobConfig.EnvironmentVariables)
                {
                    processInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using (var process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                _logger.LogInformation("Starting process: {FilePath} {Arguments}",
                    processInfo.FileName, processInfo.Arguments);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                int timeoutSeconds = schedule.TimeoutMinutes.HasValue 
                    ? schedule.TimeoutMinutes.Value * 60 
                    : (jobConfig.TimeoutSeconds > 0 ? jobConfig.TimeoutSeconds : 300);

                bool completed = process.WaitForExit(timeoutSeconds * 1000);

                if (!completed)
                {
                    _logger.LogWarning("Process timed out after {TimeoutSeconds}s, killing: {FilePath}", 
                        timeoutSeconds, processInfo.FileName);
                    process.Kill();
                    
                    jobExecution.Status = JobStatus.Cancelled;
                    jobExecution.EndTime = DateTime.UtcNow;
                    jobExecution.ErrorMessage = $"Process execution timed out after {timeoutSeconds} seconds";
                    
                    await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
                    await _unitOfWork.SaveChangesAsync();
                    
                    _logger.LogInformation("Process cancelled due to timeout: {FilePath}", processInfo.FileName);
                    
                    try
                    {
                        await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update NextRunTime for schedule {ScheduleId} after timeout", scheduleId);
                    }
                    
                    return;
                }

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Process exited with non-zero code: {process.ExitCode}");
                }
            }

            stopwatch.Stop();

            jobExecution.Status = JobStatus.Completed;
            jobExecution.EndTime = DateTime.UtcNow;
            jobExecution.Output = outputBuilder.ToString();
            jobExecution.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;

            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();

            await _emailService.SendJobExecutionNotificationAsync(jobExecution.Id, true);

            _logger.LogInformation("Process completed successfully: {FilePath}, Duration: {Duration}s",
                processInfo.FileName, stopwatch.Elapsed.TotalSeconds);
            
            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update NextRunTime for schedule {ScheduleId} after successful execution", scheduleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing process job for schedule {ScheduleId}", scheduleId);

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
                        .UsingJobData("RetryCount", (jobExecution.RetryCount + 1).ToString())
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
}

public class ProcessJobConfig
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public int TimeoutSeconds { get; set; } = 300; // Default 5 minutes
}
