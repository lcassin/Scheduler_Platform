using System.Net;
using System.Net.Mail;

namespace SchedulerPlatform.ScheduleSync.Services;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly bool _enableSsl;
    private readonly string _fromAddress;
    private readonly string _toAddress;
    private readonly bool _enabled;

    public EmailService(string smtpHost, int smtpPort, bool enableSsl, string fromAddress, string toAddress, bool enabled = true)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _enableSsl = enableSsl;
        _fromAddress = fromAddress;
        _toAddress = toAddress;
        _enabled = enabled;
    }

    public async Task SendSuccessEmailAsync(
        string subject,
        string body,
        string? logFilePath = null)
    {
        if (!_enabled)
        {
            Console.WriteLine("[Email] Email notifications disabled, skipping success email");
            return;
        }

        try
        {
            await SendEmailAsync(subject, body, logFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send success email: {ex.Message}");
        }
    }

    public async Task SendErrorEmailAsync(
        string subject,
        string body,
        string? logFilePath = null)
    {
        if (!_enabled)
        {
            Console.WriteLine("[Email] Email notifications disabled, skipping error email");
            return;
        }

        try
        {
            await SendEmailAsync(subject, body, logFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send error email: {ex.Message}");
        }
    }

    private async Task SendEmailAsync(string subject, string body, string? logFilePath)
    {
        using var message = new MailMessage(_fromAddress, _toAddress)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
        {
            message.Attachments.Add(new Attachment(logFilePath));
        }

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = _enableSsl,
            UseDefaultCredentials = true
        };

        await client.SendMailAsync(message);
        Console.WriteLine($"[Email] Email sent successfully to {_toAddress}");
    }

    public static string FormatSuccessEmail(
        DateTime startTime,
        DateTime endTime,
        SyncResult? syncResult,
        ClientSyncResult? clientSyncResult,
        ScheduleGenerationResult? scheduleResult,
        List<string> warnings)
    {
        var duration = endTime - startTime;
        var body = new System.Text.StringBuilder();

        body.AppendLine("ScheduleSync Process Completed Successfully");
        body.AppendLine("==========================================");
        body.AppendLine();
        body.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss} UTC");
        body.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        body.AppendLine($"Total Duration: {duration.TotalMinutes:F2} minutes");
        body.AppendLine();

        if (syncResult != null)
        {
            body.AppendLine("Accounts Sync Results:");
            body.AppendLine($"  Duration: {(syncResult.EndTime - syncResult.StartTime).TotalMinutes:F2} minutes");
            body.AppendLine($"  Processed: {syncResult.ProcessedCount} / {syncResult.ExpectedTotal}");
            body.AppendLine($"  Added: {syncResult.Added}");
            body.AppendLine($"  Updated: {syncResult.Updated}");
            body.AppendLine($"  Reactivated: {syncResult.Reactivated}");
            body.AppendLine($"  Deleted: {syncResult.Deleted}");
            body.AppendLine();
        }

        if (clientSyncResult != null)
        {
            body.AppendLine("Client Sync Results:");
            body.AppendLine($"  Duration: {(clientSyncResult.EndTime - clientSyncResult.StartTime).TotalSeconds:F1} seconds");
            body.AppendLine($"  Added: {clientSyncResult.Added}");
            body.AppendLine($"  Updated: {clientSyncResult.Updated}");
            body.AppendLine($"  Reactivated: {clientSyncResult.Reactivated}");
            body.AppendLine($"  Deleted: {clientSyncResult.Deleted}");
            body.AppendLine();
        }

        if (scheduleResult != null)
        {
            body.AppendLine("Schedule Generation Results:");
            body.AppendLine($"  Duration: {(scheduleResult.EndTime - scheduleResult.StartTime).TotalMinutes:F2} minutes");
            body.AppendLine($"  Created: {scheduleResult.Created}");
            body.AppendLine($"  Updated: {scheduleResult.Updated}");
            body.AppendLine($"  Errors: {scheduleResult.Errors}");
            body.AppendLine();
        }

        if (warnings.Any())
        {
            body.AppendLine("Warnings:");
            foreach (var warning in warnings)
            {
                body.AppendLine($"  - {warning}");
            }
            body.AppendLine();
        }

        body.AppendLine("See attached log file for detailed information.");

        return body.ToString();
    }

    public static string FormatErrorEmail(
        string phase,
        Exception exception,
        DateTime startTime,
        DateTime errorTime)
    {
        var duration = errorTime - startTime;
        var body = new System.Text.StringBuilder();

        body.AppendLine("ScheduleSync Process Failed");
        body.AppendLine("===========================");
        body.AppendLine();
        body.AppendLine($"Failed Phase: {phase}");
        body.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss} UTC");
        body.AppendLine($"Error Time: {errorTime:yyyy-MM-dd HH:mm:ss} UTC");
        body.AppendLine($"Duration Before Failure: {duration.TotalMinutes:F2} minutes");
        body.AppendLine();
        body.AppendLine("Error Details:");
        body.AppendLine($"  Exception Type: {exception.GetType().FullName}");
        body.AppendLine($"  Message: {exception.Message}");
        body.AppendLine();
        body.AppendLine("Stack Trace:");
        body.AppendLine(exception.StackTrace);

        if (exception.InnerException != null)
        {
            body.AppendLine();
            body.AppendLine("Inner Exception:");
            body.AppendLine($"  Type: {exception.InnerException.GetType().FullName}");
            body.AppendLine($"  Message: {exception.InnerException.Message}");
            body.AppendLine();
            body.AppendLine("Inner Stack Trace:");
            body.AppendLine(exception.InnerException.StackTrace);
        }

        body.AppendLine();
        body.AppendLine("See attached log file for detailed information.");

        return body.ToString();
    }
}
