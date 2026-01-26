namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    Task SendEmailAsync(IEnumerable<string> recipients, string subject, string body, bool isHtml = true);
    Task SendEmailWithAttachmentAsync(IEnumerable<string> recipients, string subject, string body, string? attachmentContent, string? attachmentFileName, bool isHtml = true);
    Task SendJobExecutionNotificationAsync(int jobExecutionId, bool isSuccess);
    Task SendOrchestrationFailureNotificationAsync(string orchestrationName, string requestId, string errorMessage, string? stackTrace, string? currentStep);
    Task SendSystemScheduleFailureNotificationAsync(string scheduleName, int scheduleId, string errorMessage, string? stackTrace);
}
