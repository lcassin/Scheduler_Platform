namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    Task SendEmailAsync(IEnumerable<string> recipients, string subject, string body, bool isHtml = true);
    Task SendEmailWithAttachmentAsync(IEnumerable<string> recipients, string subject, string body, string? attachmentContent, string? attachmentFileName, bool isHtml = true);
    Task SendJobExecutionNotificationAsync(int jobExecutionId, bool isSuccess);
    Task SendOrchestrationFailureNotificationAsync(string orchestrationName, string requestId, string errorMessage, string? stackTrace, string? currentStep);
    Task SendSystemScheduleFailureNotificationAsync(string scheduleName, int scheduleId, string errorMessage, string? stackTrace);
    
    /// <summary>
    /// Sends a consolidated summary email at the end of an orchestration run if there were any failures.
    /// This sends a single email summarizing all step failures rather than one email per failed job.
    /// </summary>
    Task SendOrchestrationSummaryNotificationAsync(
        string orchestrationName,
        string requestId,
        DateTime startedAt,
        DateTime completedAt,
        int syncAccountsInserted,
        int syncAccountsUpdated,
        int jobsCreated,
        int jobsSkipped,
        int credentialsVerified,
        int credentialsFailed,
        int scrapesRequested,
        int scrapesFailed,
        int statusesChecked,
        int statusesFailed,
        List<string> errorMessages);
}
