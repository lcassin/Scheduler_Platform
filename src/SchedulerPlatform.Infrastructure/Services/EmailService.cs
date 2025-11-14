using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
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
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Scheduler Platform";
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
            sb.AppendLine($"<div class='detail'><span class='label'>Start Time:</span> {jobExecution.StartTime:yyyy-MM-dd HH:mm:ss UTC}</div>");
            sb.AppendLine($"<div class='detail'><span class='label'>End Time:</span> {jobExecution.EndTime:yyyy-MM-dd HH:mm:ss UTC}</div>");
            
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
}
