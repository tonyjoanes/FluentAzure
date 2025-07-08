using AzureFunctions.Example.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.Example.Services;

/// <summary>
/// Database service implementation demonstrating configuration usage.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly DatabaseConfig _config;
    private readonly ILogger<DatabaseService> _logger;
    private readonly RetryConfig _retryConfig;

    public DatabaseService(FunctionConfiguration config, ILogger<DatabaseService> logger)
    {
        _config = config.Database;
        _retryConfig = config.Retry;
        _logger = logger;

        _logger.LogInformation(
            "Database service initialized for {Host}:{Port}",
            _config.Host,
            _config.Port
        );
    }

    public async Task<bool> IsConnectedAsync()
    {
        try
        {
            _logger.LogDebug(
                "Checking database connection to {Host}:{Port}",
                _config.Host,
                _config.Port
            );

            // Simulate database connection check
            await Task.Delay(100);

            var isConnected = !string.IsNullOrEmpty(_config.ConnectionString);
            _logger.LogInformation("Database connection status: {IsConnected}", isConnected);

            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check database connection");
            return false;
        }
    }

    public async Task<int> GetConnectionCountAsync()
    {
        try
        {
            _logger.LogDebug("Getting connection count for database {Database}", _config.Database);

            // Simulate getting connection count
            await Task.Delay(50);

            // In a real implementation, this would query the actual connection pool
            var connectionCount = Random.Shared.Next(1, 10);
            _logger.LogInformation("Current connection count: {ConnectionCount}", connectionCount);

            return connectionCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection count");
            return 0;
        }
    }

    public async Task<string> GetDatabaseInfoAsync()
    {
        try
        {
            _logger.LogDebug("Getting database info for {Database}", _config.Database);

            // Simulate database info retrieval
            await Task.Delay(200);

            var info = new
            {
                Host = _config.Host,
                Port = _config.Port,
                Database = _config.Database,
                Username = _config.Username,
                ConnectionString = _config.ConnectionString[
                    ..Math.Min(20, _config.ConnectionString.Length)
                ] + "...",
            };

            _logger.LogInformation("Database info retrieved successfully");
            return System.Text.Json.JsonSerializer.Serialize(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database info");
            return "Error retrieving database info";
        }
    }
}
