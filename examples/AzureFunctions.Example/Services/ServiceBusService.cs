using Microsoft.Extensions.Logging;

namespace AzureFunctions.Example.Services;

public class ServiceBusService : IServiceBusService
{
    private readonly ILogger<ServiceBusService> _logger;
    private readonly string _connectionString;

    public ServiceBusService(ILogger<ServiceBusService> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task<bool> IsConnectedAsync()
    {
        _logger.LogInformation("Checking Service Bus connection...");
        // Simulate connection check
        await Task.Delay(50);
        return true;
    }

    public async Task<int> GetQueueLengthAsync(string queueName)
    {
        _logger.LogInformation("Getting queue length for: {QueueName}", queueName);
        // Simulate getting queue length
        await Task.Delay(100);
        return Random.Shared.Next(0, 100);
    }

    public async Task SendMessageAsync(string queueName, string message)
    {
        _logger.LogInformation("Sending message to queue: {QueueName}", queueName);
        // Simulate sending message to Service Bus
        await Task.Delay(100);
        _logger.LogInformation("Message sent successfully to queue: {QueueName}", queueName);
    }
}
