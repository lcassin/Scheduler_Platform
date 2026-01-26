using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace SchedulerPlatform.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IJobExecutionRepository _jobExecutionRepository;
    private readonly IScheduleRepository _scheduleRepository;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IJobExecutionRepository jobExecutionRepository,
        IScheduleRepository scheduleRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _jobExecutionRepository = jobExecutionRepository;
        _scheduleRepository = scheduleRepository;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        await SendEmailAsync(new[] { to }, subject, body, isHtml);
    }

    public async Task SendEmailAsync(IEnumerable<string> recipients, string subject, string body, bool isHtml = true)
    {
        await SendEmailWithAttachmentAsync(recipients, subject, body, null, null, isHtml);
    }

    public async Task SendEmailWithAttachmentAsync(IEnumerable<string> recipients, string subject, string body, string? attachmentContent, string? attachmentFileName, bool isHtml = true)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "ADR Scheduler";
            var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
            {
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            foreach (var recipient in recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    mailMessage.To.Add(recipient.Trim());
                }
            }

            if (!string.IsNullOrEmpty(attachmentContent) && !string.IsNullOrEmpty(attachmentFileName))
            {
                var attachmentBytes = Encoding.UTF8.GetBytes(attachmentContent);
                var attachmentStream = new MemoryStream(attachmentBytes);
                var attachment = new Attachment(attachmentStream, attachmentFileName, MediaTypeNames.Text.Plain);
                mailMessage.Attachments.Add(attachment);
            }

            if (mailMessage.To.Count > 0)
            {
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", recipients));
            }
            else
            {
                _logger.LogWarning("No valid recipients found for email notification");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", recipients));
            throw;
        }
    }

    public async Task SendJobExecutionNotificationAsync(int jobExecutionId, bool isSuccess)
    {
        try
        {
            var jobExecution = await _jobExecutionRepository.GetByIdAsync(jobExecutionId);
            if (jobExecution == null)
            {
                _logger.LogWarning("Job execution {JobExecutionId} not found", jobExecutionId);
                return;
            }

            var schedule = await _scheduleRepository.GetByIdWithNotificationSettingsAsync(jobExecution.ScheduleId);
            if (schedule?.NotificationSetting == null)
            {
                _logger.LogDebug("No notification settings configured for schedule {ScheduleId}", jobExecution.ScheduleId);
                return;
            }

            var notificationSettings = schedule.NotificationSetting;
            
            if (isSuccess && !notificationSettings.EnableSuccessNotifications)
            {
                _logger.LogDebug("Success notifications disabled for schedule {ScheduleId}", schedule.Id);
                return;
            }

            if (!isSuccess && !notificationSettings.EnableFailureNotifications)
            {
                _logger.LogDebug("Failure notifications disabled for schedule {ScheduleId}", schedule.Id);
                return;
            }

            var recipients = isSuccess 
                ? ParseEmailRecipients(notificationSettings.SuccessEmailRecipients) 
                : ParseEmailRecipients(notificationSettings.FailureEmailRecipients ?? _configuration["Email:DefaultFailureRecipient"]);

            if (!recipients.Any())
            {
                _logger.LogWarning("No email recipients configured for {Status} notification on schedule {ScheduleId}", 
                    isSuccess ? "success" : "failure", schedule.Id);
                return;
            }

            var subject = isSuccess 
                ? notificationSettings.SuccessEmailSubject ?? $"Schedule '{schedule.Name}' Completed Successfully"
                : notificationSettings.FailureEmailSubject ?? $"Schedule '{schedule.Name}' Failed";

            var body = BuildNotificationEmailBody(schedule, jobExecution, isSuccess, notificationSettings);

            await SendEmailAsync(recipients, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send job execution notification for {JobExecutionId}", jobExecutionId);
        }
    }

    private List<string> ParseEmailRecipients(string? recipients)
    {
        if (string.IsNullOrWhiteSpace(recipients))
            return new List<string>();

        return recipients
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();
    }

    private string BuildNotificationEmailBody(Schedule schedule, JobExecution jobExecution, bool isSuccess, NotificationSetting settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
        sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine(".header { background: " + (isSuccess ? "#4CAF50" : "#f44336") + "; color: white; padding: 20px; border-radius: 5px 5px 0 0; }");
        sb.AppendLine(".content { background: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }");
        sb.AppendLine(".detail { margin: 10px 0; }");
        sb.AppendLine(".label { font-weight: bold; }");
        sb.AppendLine(".output { background: #fff; padding: 10px; border-left: 3px solid #ddd; margin: 10px 0; font-family: monospace; white-space: pre-wrap; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine($"<div class='header'><h2>{(isSuccess ? "✓ Success" : "✗ Failure")}: {schedule.Name}</h2></div>");
        sb.AppendLine("<div class='content'>");
        
        if (settings.IncludeExecutionDetails)
        {
            sb.AppendLine($"<div class='detail'><span class='label'>Schedule:</span> {schedule.Name}</div>");
            sb.AppendLine($"<div class='detail'><span class='label'>Description:</span> {schedule.Description}</div>");
            sb.AppendLine($"<div class='detail'><span class='label'>Job Type:</span> {schedule.JobType}</div>");
            sb.AppendLine($"<div class='detail'><span class='label'>Start Time:</span> {jobExecution.StartDateTime:yyyy-MM-dd HH:mm:ss UTC}</div>");
            sb.AppendLine($"<div class='detail'><span class='label'>End Time:</span> {jobExecution.EndDateTime:yyyy-MM-dd HH:mm:ss UTC}</div>");
            
            if (jobExecution.DurationSeconds.HasValue)
            {
                sb.AppendLine($"<div class='detail'><span class='label'>Duration:</span> {jobExecution.DurationSeconds.Value} seconds</div>");
            }
            
            sb.AppendLine($"<div class='detail'><span class='label'>Status:</span> {jobExecution.Status}</div>");
            
            if (jobExecution.RetryCount > 0)
            {
                sb.AppendLine($"<div class='detail'><span class='label'>Retry Count:</span> {jobExecution.RetryCount}</div>");
            }
        }

        if (!isSuccess && !string.IsNullOrWhiteSpace(jobExecution.ErrorMessage))
        {
            sb.AppendLine($"<div class='detail'><span class='label'>Error:</span></div>");
            sb.AppendLine($"<div class='output'>{System.Net.WebUtility.HtmlEncode(jobExecution.ErrorMessage)}</div>");
        }

        if (settings.IncludeOutput && !string.IsNullOrWhiteSpace(jobExecution.Output))
        {
            sb.AppendLine($"<div class='detail'><span class='label'>Output:</span></div>");
            sb.AppendLine($"<div class='output'>{System.Net.WebUtility.HtmlEncode(jobExecution.Output)}</div>");
        }

        sb.AppendLine("</div></div></body></html>");
        return sb.ToString();
    }

    public async Task SendOrchestrationFailureNotificationAsync(string orchestrationName, string requestId, string errorMessage, string? stackTrace, string? currentStep)
    {
        try
        {
            var recipients = ParseEmailRecipients(_configuration["ErrorNotifications:Recipients"]);
            if (!recipients.Any())
            {
                _logger.LogWarning("No error notification recipients configured for orchestration failure");
                return;
            }

            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
            var subject = $"[{environment}] ADR Orchestration Failed: {orchestrationName}";

            var body = BuildOrchestrationFailureEmailBody(orchestrationName, requestId, errorMessage, currentStep, environment);

            string? attachmentContent = null;
            string? attachmentFileName = null;
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                attachmentContent = $"ADR Orchestration Failure - Stack Trace\n" +
                    $"{'='.ToString().PadRight(80, '=')}\n" +
                    $"Orchestration: {orchestrationName}\n" +
                    $"Request ID: {requestId}\n" +
                    $"Current Step: {currentStep ?? "Unknown"}\n" +
                    $"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                    $"{'='.ToString().PadRight(80, '=')}\n\n" +
                    $"Error Message:\n{errorMessage}\n\n" +
                    $"Stack Trace:\n{stackTrace}";
                attachmentFileName = $"orchestration_error_{requestId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            }

            await SendEmailWithAttachmentAsync(recipients, subject, body, attachmentContent, attachmentFileName);
            _logger.LogInformation("Orchestration failure notification sent for {OrchestrationName} (RequestId: {RequestId})", orchestrationName, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send orchestration failure notification for {OrchestrationName}", orchestrationName);
        }
    }

    public async Task SendSystemScheduleFailureNotificationAsync(string scheduleName, int scheduleId, string errorMessage, string? stackTrace)
    {
        try
        {
            var recipients = ParseEmailRecipients(_configuration["ErrorNotifications:Recipients"]);
            if (!recipients.Any())
            {
                _logger.LogWarning("No error notification recipients configured for system schedule failure");
                return;
            }

            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
            var subject = $"[{environment}] System Schedule Failed: {scheduleName}";

            var body = BuildSystemScheduleFailureEmailBody(scheduleName, scheduleId, errorMessage, environment);

            string? attachmentContent = null;
            string? attachmentFileName = null;
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                attachmentContent = $"System Schedule Failure - Stack Trace\n" +
                    $"{'='.ToString().PadRight(80, '=')}\n" +
                    $"Schedule: {scheduleName}\n" +
                    $"Schedule ID: {scheduleId}\n" +
                    $"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                    $"{'='.ToString().PadRight(80, '=')}\n\n" +
                    $"Error Message:\n{errorMessage}\n\n" +
                    $"Stack Trace:\n{stackTrace}";
                attachmentFileName = $"schedule_error_{scheduleId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            }

            await SendEmailWithAttachmentAsync(recipients, subject, body, attachmentContent, attachmentFileName);
            _logger.LogInformation("System schedule failure notification sent for {ScheduleName} (Id: {ScheduleId})", scheduleName, scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send system schedule failure notification for {ScheduleName}", scheduleName);
        }
    }

    private static string BuildOrchestrationFailureEmailBody(string orchestrationName, string requestId, string errorMessage, string? currentStep, string environment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
        sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine(".header { background: #d32f2f; color: white; padding: 20px; border-radius: 5px 5px 0 0; }");
        sb.AppendLine(".content { background: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }");
        sb.AppendLine(".detail { margin: 10px 0; }");
        sb.AppendLine(".label { font-weight: bold; }");
        sb.AppendLine(".error-box { background: #fff; padding: 15px; border-left: 4px solid #d32f2f; margin: 15px 0; font-family: monospace; white-space: pre-wrap; word-wrap: break-word; }");
        sb.AppendLine(".info-box { background: #fff3e0; padding: 10px; border-radius: 4px; margin: 10px 0; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine($"<div class='header'><h2>ADR Orchestration Failed</h2><p style='margin: 5px 0 0 0;'>{environment}</p></div>");
        sb.AppendLine("<div class='content'>");
        sb.AppendLine($"<div class='detail'><span class='label'>Orchestration:</span> {System.Net.WebUtility.HtmlEncode(orchestrationName)}</div>");
        sb.AppendLine($"<div class='detail'><span class='label'>Request ID:</span> {System.Net.WebUtility.HtmlEncode(requestId)}</div>");
        sb.AppendLine($"<div class='detail'><span class='label'>Timestamp:</span> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
        if (!string.IsNullOrWhiteSpace(currentStep))
        {
            sb.AppendLine($"<div class='detail'><span class='label'>Failed During Step:</span> {System.Net.WebUtility.HtmlEncode(currentStep)}</div>");
        }
        sb.AppendLine("<div class='detail'><span class='label'>Error Message:</span></div>");
        sb.AppendLine($"<div class='error-box'>{System.Net.WebUtility.HtmlEncode(errorMessage)}</div>");
        sb.AppendLine("<p style='color: #666; font-size: 12px;'>Full stack trace is attached as a text file if available.</p>");
        sb.AppendLine("</div></div></body></html>");
        return sb.ToString();
    }

    private static string BuildSystemScheduleFailureEmailBody(string scheduleName, int scheduleId, string errorMessage, string environment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
        sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine(".header { background: #d32f2f; color: white; padding: 20px; border-radius: 5px 5px 0 0; }");
        sb.AppendLine(".content { background: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }");
        sb.AppendLine(".detail { margin: 10px 0; }");
        sb.AppendLine(".label { font-weight: bold; }");
        sb.AppendLine(".error-box { background: #fff; padding: 15px; border-left: 4px solid #d32f2f; margin: 15px 0; font-family: monospace; white-space: pre-wrap; word-wrap: break-word; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine($"<div class='header'><h2>System Schedule Failed</h2><p style='margin: 5px 0 0 0;'>{environment}</p></div>");
        sb.AppendLine("<div class='content'>");
        sb.AppendLine($"<div class='detail'><span class='label'>Schedule:</span> {System.Net.WebUtility.HtmlEncode(scheduleName)}</div>");
        sb.AppendLine($"<div class='detail'><span class='label'>Schedule ID:</span> {scheduleId}</div>");
        sb.AppendLine($"<div class='detail'><span class='label'>Timestamp:</span> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("<div class='detail'><span class='label'>Error Message:</span></div>");
        sb.AppendLine($"<div class='error-box'>{System.Net.WebUtility.HtmlEncode(errorMessage)}</div>");
        sb.AppendLine("<p style='color: #666; font-size: 12px;'>Full stack trace is attached as a text file if available.</p>");
        sb.AppendLine("</div></div></body></html>");
        return sb.ToString();
    }
}
