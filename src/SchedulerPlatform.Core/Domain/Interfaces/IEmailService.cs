namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    Task SendEmailAsync(IEnumerable<string> recipients, string subject, string body, bool isHtml = true);
    Task SendJobExecutionNotificationAsync(int jobExecutionId, bool isSuccess);
}
