using System.Net;
using System.Text.Json;
using AzureFunctions.Example.Configuration;
using AzureFunctions.Example.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.Example.Functions;

/// <summary>
/// Health check function demonstrating configuration usage in Azure Functions.
/// </summary>
public class HealthCheckFunction
{
    private readonly FunctionConfiguration _config;
    private readonly IDatabaseService _databaseService;
    private readonly IServiceBusService _serviceBusService;
    private readonly IStorageService _storageService;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<HealthCheckFunction> _logger;

    public HealthCheckFunction(
        FunctionConfiguration config,
        IDatabaseService databaseService,
        IServiceBusService serviceBusService,
        IStorageService storageService,
        ITelemetryService telemetryService,
        ILogger<HealthCheckFunction> logger
    )
    {
        _config = config;
        _databaseService = databaseService;
        _serviceBusService = serviceBusService;
        _storageService = storageService;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req
    )
    {
        _logger.LogInformation("Health check requested");

        try
        {
            // Track telemetry if enabled
            if (_config.Telemetry.EnableTelemetry)
            {
                await _telemetryService.TrackEventAsync(
                    "HealthCheck",
                    new Dictionary<string, object>
                    {
                        ["RequestId"] = req.Url.ToString(),
                        ["Timestamp"] = DateTime.UtcNow,
                    }
                );
            }

            // Check all services
            var healthChecks = await PerformHealthChecksAsync();

            // Determine overall health
            var isHealthy = healthChecks.All(h => h.IsHealthy);
            var statusCode = isHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;

            // Create response
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var healthResponse = new
            {
                Status = isHealthy ? "Healthy" : "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")
                    ?? "Development",
                Configuration = new
                {
                    Database = new
                    {
                        Host = _config.Database.Host,
                        Port = _config.Database.Port,
                        Database = _config.Database.Database,
                    },
                    ServiceBus = new
                    {
                        Namespace = _config.ServiceBus.Namespace,
                        MaxConcurrentCalls = _config.ServiceBus.MaxConcurrentCalls,
                    },
                    Storage = new
                    {
                        AccountName = _config.Storage.AccountName,
                        MaxConcurrency = _config.Storage.MaxConcurrency,
                    },
                    Retry = new
                    {
                        MaxRetryCount = _config.Retry.MaxRetryCount,
                        TimeoutSeconds = _config.Retry.TimeoutSeconds,
                    },
                },
                Services = healthChecks,
            };

            await response.WriteAsJsonAsync(healthResponse);

            // Track metrics
            if (_config.Telemetry.EnableTelemetry)
            {
                await _telemetryService.TrackMetricAsync(
                    "HealthCheck.Duration",
                    (DateTime.UtcNow - DateTime.UtcNow.AddMinutes(-1)).TotalMilliseconds
                );
                await _telemetryService.TrackMetricAsync(
                    "HealthCheck.HealthyServices",
                    healthChecks.Count(h => h.IsHealthy)
                );
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            if (_config.Telemetry.EnableTelemetry)
            {
                await _telemetryService.TrackExceptionAsync(ex);
            }

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await errorResponse.WriteAsJsonAsync(
                new
                {
                    Status = "Error",
                    Message = "Health check failed",
                    Timestamp = DateTime.UtcNow,
                }
            );

            return errorResponse;
        }
    }

    private async Task<List<ServiceHealth>> PerformHealthChecksAsync()
    {
        var healthChecks = new List<ServiceHealth>();

        // Database health check
        try
        {
            var dbConnected = await _databaseService.IsConnectedAsync();
            var connectionCount = await _databaseService.GetConnectionCountAsync();

            healthChecks.Add(
                new ServiceHealth
                {
                    Service = "Database",
                    IsHealthy = dbConnected,
                    Details = new
                    {
                        Host = _config.Database.Host,
                        Port = _config.Database.Port,
                        ConnectionCount = connectionCount,
                        MaxConnections = _config.ServiceBus.MaxConcurrentCalls, // Using as example
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            healthChecks.Add(
                new ServiceHealth
                {
                    Service = "Database",
                    IsHealthy = false,
                    Error = ex.Message,
                }
            );
        }

        // Service Bus health check
        try
        {
            var sbConnected = await _serviceBusService.IsConnectedAsync();
            var queueLength = await _serviceBusService.GetQueueLengthAsync("health-check-queue");

            healthChecks.Add(
                new ServiceHealth
                {
                    Service = "ServiceBus",
                    IsHealthy = sbConnected,
                    Details = new
                    {
                        Namespace = _config.ServiceBus.Namespace,
                        MaxConcurrentCalls = _config.ServiceBus.MaxConcurrentCalls,
                        QueueLength = queueLength,
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Bus health check failed");
            healthChecks.Add(
                new ServiceHealth
                {
                    Service = "ServiceBus",
                    IsHealthy = false,
                    Error = ex.Message,
                }
            );
        }

        // Storage health check
        try
        {
            var storageConnected = await _storageService.IsConnectedAsync();
            var containerInfo = await _storageService.GetContainerInfoAsync(
                "health-check-container"
            );

            healthChecks.Add(
                new ServiceHealth
                {
                    Service = "Storage",
                    IsHealthy = storageConnected,
                    Details = new
                    {
                        AccountName = _config.Storage.AccountName,
                        MaxConcurrency = _config.Storage.MaxConcurrency,
                        ContainerInfo = containerInfo,
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health check failed");
            healthChecks.Add(
                new ServiceHealth
                {
                    Service = "Storage",
                    IsHealthy = false,
                    Error = ex.Message,
                }
            );
        }

        return healthChecks;
    }
}

/// <summary>
/// Represents the health status of a service.
/// </summary>
public class ServiceHealth
{
    public string Service { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public object? Details { get; set; }
    public string? Error { get; set; }
}
