using System.Net;
using System.Text;
using System.Text.Json;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Middleware;

/// <summary>
/// Global exception handler middleware that catches unhandled exceptions,
/// logs them, sends email notifications for 500 errors, and returns appropriate error responses.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred while processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var errorId = Guid.NewGuid().ToString("N")[..8];
        var timestamp = DateTime.UtcNow;

        var errorResponse = new
        {
            error = "An internal server error occurred.",
            errorId = errorId,
            timestamp = timestamp,
            path = context.Request.Path.ToString(),
            method = context.Request.Method
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);

        _ = Task.Run(async () =>
        {
            try
            {
                await SendErrorNotificationEmailAsync(exception, context.Request.Method, context.Request.Path.ToString(), errorId, timestamp);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send error notification email for error {ErrorId}", errorId);
            }
        });
    }

    private async Task SendErrorNotificationEmailAsync(Exception exception, string method, string path, string errorId, DateTime timestamp)
    {
        var recipients = _configuration["ErrorNotifications:Recipients"];
        if (string.IsNullOrWhiteSpace(recipients))
        {
            _logger.LogWarning("No error notification recipients configured. Set ErrorNotifications:Recipients in appsettings.json");
            return;
        }

        var enabled = _configuration.GetValue<bool>("ErrorNotifications:Enabled", true);
        if (!enabled)
        {
            _logger.LogDebug("Error notifications are disabled");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var recipientList = recipients
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        if (!recipientList.Any())
        {
            _logger.LogWarning("No valid error notification recipients found");
            return;
        }

        var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
        var appName = "ADR Scheduler API";

        var subject = $"[{environment}] {appName} - 500 Error: {exception.GetType().Name}";

        var body = BuildErrorEmailBody(exception, method, path, errorId, timestamp, environment);

        var stackTrace = BuildFullStackTrace(exception);
        var attachmentFileName = $"stacktrace_{errorId}_{timestamp:yyyyMMdd_HHmmss}.txt";

        await emailService.SendEmailWithAttachmentAsync(recipientList, subject, body, stackTrace, attachmentFileName);

        _logger.LogInformation("Error notification email sent for error {ErrorId} to {Recipients}", errorId, string.Join(", ", recipientList));
    }

    private static string BuildErrorEmailBody(Exception exception, string method, string path, string errorId, DateTime timestamp, string environment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }");
        sb.AppendLine(".container { max-width: 700px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine(".header { background: #d32f2f; color: white; padding: 20px; border-radius: 5px 5px 0 0; }");
        sb.AppendLine(".header h2 { margin: 0; }");
        sb.AppendLine(".content { background: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; border: 1px solid #ddd; border-top: none; }");
        sb.AppendLine(".detail { margin: 12px 0; }");
        sb.AppendLine(".label { font-weight: bold; color: #555; }");
        sb.AppendLine(".value { margin-left: 10px; }");
        sb.AppendLine(".error-box { background: #fff; padding: 15px; border-left: 4px solid #d32f2f; margin: 15px 0; font-family: monospace; white-space: pre-wrap; word-wrap: break-word; overflow-x: auto; }");
        sb.AppendLine(".info-box { background: #e3f2fd; padding: 10px; border-radius: 4px; margin: 10px 0; }");
        sb.AppendLine(".badge { display: inline-block; padding: 4px 8px; border-radius: 4px; font-size: 12px; font-weight: bold; }");
        sb.AppendLine(".badge-error { background: #ffebee; color: #c62828; }");
        sb.AppendLine(".badge-env { background: #fff3e0; color: #e65100; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<h2>500 Internal Server Error</h2>");
        sb.AppendLine($"<p style='margin: 5px 0 0 0; opacity: 0.9;'>ADR Scheduler API - {environment}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class='content'>");

        sb.AppendLine("<div class='info-box'>");
        sb.AppendLine($"<span class='badge badge-error'>Error ID: {errorId}</span> ");
        sb.AppendLine($"<span class='badge badge-env'>Environment: {environment}</span>");
        sb.AppendLine("</div>");

        sb.AppendLine($"<div class='detail'><span class='label'>Timestamp (UTC):</span><span class='value'>{timestamp:yyyy-MM-dd HH:mm:ss}</span></div>");
        sb.AppendLine($"<div class='detail'><span class='label'>Request:</span><span class='value'>{method} {WebUtility.HtmlEncode(path)}</span></div>");
        sb.AppendLine($"<div class='detail'><span class='label'>Exception Type:</span><span class='value'>{WebUtility.HtmlEncode(exception.GetType().FullName)}</span></div>");

        sb.AppendLine("<div class='detail'><span class='label'>Error Message:</span></div>");
        sb.AppendLine($"<div class='error-box'>{WebUtility.HtmlEncode(exception.Message)}</div>");

        if (exception.InnerException != null)
        {
            sb.AppendLine("<div class='detail'><span class='label'>Inner Exception:</span></div>");
            sb.AppendLine($"<div class='error-box'>[{WebUtility.HtmlEncode(exception.InnerException.GetType().Name)}] {WebUtility.HtmlEncode(exception.InnerException.Message)}</div>");
        }

        sb.AppendLine("<div class='detail'><span class='label'>Stack Trace Preview:</span></div>");
        var stackPreview = exception.StackTrace?.Split('\n').Take(5).ToArray() ?? Array.Empty<string>();
        sb.AppendLine($"<div class='error-box'>{WebUtility.HtmlEncode(string.Join("\n", stackPreview))}\n...</div>");

        sb.AppendLine("<p style='color: #666; font-size: 12px; margin-top: 20px;'>Full stack trace is attached as a text file.</p>");

        sb.AppendLine("</div></div></body></html>");
        return sb.ToString();
    }

    private static string BuildFullStackTrace(Exception exception)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine("ADR SCHEDULER API - ERROR REPORT");
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        var current = exception;
        var depth = 0;

        while (current != null && depth < 10)
        {
            sb.AppendLine("-".PadRight(80, '-'));
            if (depth == 0)
            {
                sb.AppendLine("PRIMARY EXCEPTION:");
            }
            else
            {
                sb.AppendLine($"INNER EXCEPTION (Level {depth}):");
            }
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine();
            sb.AppendLine($"Type: {current.GetType().FullName}");
            sb.AppendLine($"Message: {current.Message}");
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(current.StackTrace ?? "(No stack trace available)");
            sb.AppendLine();

            if (current is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                sb.AppendLine("SQL Error Details:");
                sb.AppendLine($"  Number: {sqlEx.Number}");
                sb.AppendLine($"  State: {sqlEx.State}");
                sb.AppendLine($"  Class: {sqlEx.Class}");
                sb.AppendLine($"  Server: {sqlEx.Server}");
                sb.AppendLine($"  Procedure: {sqlEx.Procedure}");
                sb.AppendLine($"  LineNumber: {sqlEx.LineNumber}");
                sb.AppendLine();
                sb.AppendLine("SQL Errors Collection:");
                foreach (Microsoft.Data.SqlClient.SqlError error in sqlEx.Errors)
                {
                    sb.AppendLine($"  - Error {error.Number}: {error.Message} (Line: {error.LineNumber}, State: {error.State})");
                }
                sb.AppendLine();
            }

            current = current.InnerException;
            depth++;
        }

        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine("END OF ERROR REPORT");
        sb.AppendLine("=".PadRight(80, '='));

        return sb.ToString();
    }
}
