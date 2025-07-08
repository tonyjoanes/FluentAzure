using Azure.Messaging.ServiceBus;
using WebApi.Example.Configuration;

namespace WebApi.Example.Services;

/// <summary>
/// Service implementation for notification operations using Azure Service Bus.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly WebApiConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        WebApiConfiguration config,
        ILogger<NotificationService> logger)
    {
        _config = config;
        _logger = logger;
        _serviceBusClient = new ServiceBusClient(config.ServiceBus.ConnectionString);
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var message = new ServiceBusMessage
            {
                Subject = "Email",
                Body = BinaryData.FromObjectAsJson(new
                {
                    To = to,
                    Subject = subject,
                    Body = body,
                    Timestamp = DateTime.UtcNow
                })
            };

            var sender = _serviceBusClient.CreateSender(_config.ServiceBus.DefaultTopic);
            await sender.SendMessageAsync(message);

            _logger.LogInformation("Email notification sent to: {To}, Subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to: {To}", to);
            throw;
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            var serviceBusMessage = new ServiceBusMessage
            {
                Subject = "SMS",
                Body = BinaryData.FromObjectAsJson(new
                {
                    PhoneNumber = phoneNumber,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                })
            };

            var sender = _serviceBusClient.CreateSender(_config.ServiceBus.DefaultTopic);
            await sender.SendMessageAsync(serviceBusMessage);

            _logger.LogInformation("SMS notification sent to: {PhoneNumber}", phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS notification to: {PhoneNumber}", phoneNumber);
            throw;
        }
    }

    public async Task SendPushNotificationAsync(string userId, string title, string message)
    {
        try
        {
            var serviceBusMessage = new ServiceBusMessage
            {
                Subject = "Push",
                Body = BinaryData.FromObjectAsJson(new
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                })
            };

            var sender = _serviceBusClient.CreateSender(_config.ServiceBus.DefaultTopic);
            await sender.SendMessageAsync(serviceBusMessage);

            _logger.LogInformation("Push notification sent to user: {UserId}, Title: {Title}", userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsNotificationEnabledAsync(string userId, string notificationType)
    {
        // In a real application, this would check user preferences from a database
        // For this example, we'll return true for all notifications
        await Task.CompletedTask;
        return true;
    }

    public async Task SendWelcomeEmailAsync(string to, string userName)
    {
        var subject = "Welcome to Our Application";
        var body = $"Hello {userName}, welcome to our application! We're excited to have you on board.";

        await SendEmailAsync(to, subject, body);
    }
}
