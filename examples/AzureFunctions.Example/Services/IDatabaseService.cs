namespace AzureFunctions.Example.Services;

/// <summary>
/// Database service interface for demonstrating configuration usage.
/// </summary>
public interface IDatabaseService
{
    Task<bool> IsConnectedAsync();
    Task<int> GetConnectionCountAsync();
    Task<string> GetDatabaseInfoAsync();
}

/// <summary>
/// Service Bus service interface.
/// </summary>
public interface IServiceBusService
{
    Task<bool> IsConnectedAsync();
    Task<int> GetQueueLengthAsync(string queueName);
    Task SendMessageAsync(string queueName, string message);
}

/// <summary>
/// Storage service interface.
/// </summary>
public interface IStorageService
{
    Task<bool> IsConnectedAsync();
    Task<string> GetContainerInfoAsync(string containerName);
    Task UploadBlobAsync(string containerName, string blobName, string content);
}

/// <summary>
/// Telemetry service interface.
/// </summary>
public interface ITelemetryService
{
    Task TrackEventAsync(string eventName, Dictionary<string, object> properties);
    Task TrackMetricAsync(string metricName, double value);
    Task TrackExceptionAsync(Exception exception);
}
