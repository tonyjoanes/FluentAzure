namespace WebApi.Example.Services;

/// <summary>
/// Service interface for notification operations.
/// </summary>
public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendSmsAsync(string phoneNumber, string message);
    Task SendPushNotificationAsync(string userId, string title, string message);
    Task<bool> IsNotificationEnabledAsync(string userId, string notificationType);
    Task SendWelcomeEmailAsync(string to, string userName);
}
