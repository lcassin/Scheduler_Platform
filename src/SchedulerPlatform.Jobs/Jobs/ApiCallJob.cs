using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Services;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SchedulerPlatform.Jobs.Jobs;

[DisallowConcurrentExecution]
public class ApiCallJob : IJob
{
    private readonly ILogger<ApiCallJob> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmailService _emailService;
    private readonly ISchedulerService _schedulerService;
    private readonly IHostEnvironment _environment;

    public ApiCallJob(ILogger<ApiCallJob> logger, IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory, IEmailService emailService, ISchedulerService schedulerService, IHostEnvironment environment)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _emailService = emailService;
        _schedulerService = schedulerService;
        _environment = environment;
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

        int timeoutSeconds = 300;

        try
        {
            var jobConfig = JsonSerializer.Deserialize<ApiCallJobConfig>(
                schedule.JobConfiguration ?? "{}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrEmpty(jobConfig?.Url))
            {
                throw new Exception("API URL is not configured");
            }

            var jobParameters = await _unitOfWork.JobParameters.FindAsync(jp => jp.ScheduleId == scheduleId);
            
            var httpClient = _httpClientFactory.CreateClient("ApiCallJob");
            timeoutSeconds = schedule.TimeoutMinutes.HasValue 
                ? schedule.TimeoutMinutes.Value * 60 
                : (jobConfig.TimeoutSeconds > 0 ? jobConfig.TimeoutSeconds : 300);
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            using var request = new HttpRequestMessage(new HttpMethod(jobConfig.Method ?? "GET"), jobConfig.Url);
            
            if (jobConfig.Headers != null)
            {
                foreach (var header in jobConfig.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }
            
            if (!string.IsNullOrEmpty(jobConfig.AuthorizationType) && !string.IsNullOrEmpty(jobConfig.AuthorizationValue))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    jobConfig.AuthorizationType, jobConfig.AuthorizationValue);
            }
            
            if ((jobConfig.Method == "POST" || jobConfig.Method == "PUT" || jobConfig.Method == "PATCH") 
                && !string.IsNullOrEmpty(jobConfig.RequestBody))
            {
                string requestBody = jobConfig.RequestBody;
                
                var parameterDict = new Dictionary<string, object>();
                
                foreach (var param in jobParameters)
                {
                    string paramValue = param.ParameterValue ?? string.Empty;
                    
                    if (param.IsDynamic && !string.IsNullOrEmpty(param.SourceQuery) && 
                        !string.IsNullOrEmpty(param.SourceConnectionString))
                    {
                        var sourceConnString = ValidateAndSecureConnectionString(param.SourceConnectionString);
                        
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
                    
                    parameterDict[param.ParameterName] = paramValue;
                    
                    requestBody = requestBody.Replace($"{{{param.ParameterName}}}", paramValue);
                }
                
                var content = new StringContent(requestBody, Encoding.UTF8, jobConfig.ContentType ?? "application/json");
                request.Content = content;
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation("Executing API call: {Method} {Url}", request.Method, request.RequestUri);
            
            using var response = await httpClient.SendAsync(request);
            
            stopwatch.Stop();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            response.EnsureSuccessStatusCode();
            
            jobExecution.Status = JobStatus.Completed;
            jobExecution.EndDateTime = DateTime.UtcNow;
            jobExecution.Output = $"Status: {(int)response.StatusCode} {response.StatusCode}\n\n{responseContent}";
            jobExecution.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;

            await _unitOfWork.JobExecutions.UpdateAsync(jobExecution);
            await _unitOfWork.SaveChangesAsync();

            await _emailService.SendJobExecutionNotificationAsync(jobExecution.Id, true);

            _logger.LogInformation("API call to {Url} completed successfully with status {Status}, Duration: {Duration}s",
                jobConfig.Url, response.StatusCode, stopwatch.Elapsed.TotalSeconds);
            
            try
            {
                await _schedulerService.UpdateNextRunTimeAsync(scheduleId, schedule.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update NextRunTime for schedule {ScheduleId} after successful execution", scheduleId);
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "API call timed out for schedule {ScheduleId}", scheduleId);
            
            jobExecution.Status = JobStatus.Cancelled;
            jobExecution.EndDateTime = DateTime.UtcNow;
            jobExecution.ErrorMessage = $"API call timed out after {timeoutSeconds} seconds";
            
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
            _logger.LogError(ex, "Error executing API call job for schedule {ScheduleId}", scheduleId);

            jobExecution.Status = JobStatus.Failed;
            jobExecution.EndDateTime = DateTime.UtcNow;
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

    private string ValidateAndSecureConnectionString(string connectionString)
    {
        if (_environment.IsProduction())
        {
            if (connectionString.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "SECURITY: Insecure SQL connection settings detected in production. " +
                    "Connection strings must use Encrypt=True and TrustServerCertificate=False with valid certificates.");
                throw new InvalidOperationException(
                    "Insecure SQL connection settings are not allowed in production. " +
                    "Use Encrypt=True and TrustServerCertificate=False with a valid certificate.");
            }
            
            if (!connectionString.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase))
            {
                connectionString += ";Encrypt=True;TrustServerCertificate=False";
                _logger.LogInformation("Added secure encryption settings to connection string");
            }
        }
        else
        {
            if (!connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase) &&
                !connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
            {
                connectionString += ";TrustServerCertificate=True";
            }
        }
        
        return connectionString;
    }
}

public class ApiCallJobConfig
{
    public string Url { get; set; } = string.Empty;
    public string? Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public string? AuthorizationType { get; set; }
    public string? AuthorizationValue { get; set; }
    public string? RequestBody { get; set; }
    public string? ContentType { get; set; } = "application/json";
    public int TimeoutSeconds { get; set; } = 300; // Default 5 minutes
}
