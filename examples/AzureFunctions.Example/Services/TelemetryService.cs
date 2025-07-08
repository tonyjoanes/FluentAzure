using Microsoft.Extensions.Logging;

namespace AzureFunctions.Example.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    public async Task TrackEventAsync(string eventName, Dictionary<string, object> properties)
    {
        _logger.LogInformation("Tracking event: {EventName} with {PropertyCount} properties", eventName, properties.Count);
        // Simulate event tracking
        await Task.Delay(50);
        _logger.LogInformation("Event tracked successfully: {EventName}", eventName);
    }

    public async Task TrackMetricAsync(string metricName, double value)
    {
        _logger.LogInformation("Tracking metric: {MetricName} = {Value}", metricName, value);
        // Simulate metric tracking
        await Task.Delay(50);
        _logger.LogInformation("Metric tracked successfully: {MetricName}", metricName);
    }

    public async Task TrackExceptionAsync(Exception exception)
    {
        _logger.LogInformation("Tracking exception: {ExceptionType}", exception.GetType().Name);
        // Simulate exception tracking
        await Task.Delay(50);
        _logger.LogInformation("Exception tracked successfully: {ExceptionType}", exception.GetType().Name);
    }
}
