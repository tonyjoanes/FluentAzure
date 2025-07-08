using System.ComponentModel.DataAnnotations;

namespace AzureFunctions.Example.Configuration;

/// <summary>
/// Main configuration class for Azure Functions application.
/// Demonstrates complex nested configuration with validation.
/// </summary>
public class FunctionConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public ServiceBusConfig ServiceBus { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public TelemetryConfig Telemetry { get; set; } = new();
    public RetryConfig Retry { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
}

/// <summary>
/// Database configuration with connection string parsing.
/// </summary>
public class DatabaseConfig
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string Host => ParseConnectionString().Host;
    public int Port => ParseConnectionString().Port;
    public string Database => ParseConnectionString().Database;
    public string Username => ParseConnectionString().Username;

    private (string Host, int Port, string Database, string Username) ParseConnectionString()
    {
        // Simple parsing for demonstration - in real apps use proper connection string builders
        var parts = ConnectionString
            .Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());

        return (
            Host: parts.GetValueOrDefault("server", "localhost"),
            Port: int.TryParse(parts.GetValueOrDefault("port", "1433"), out var port) ? port : 1433,
            Database: parts.GetValueOrDefault("database", "default"),
            Username: parts.GetValueOrDefault("user id", "unknown")
        );
    }
}

/// <summary>
/// Azure Service Bus configuration.
/// </summary>
public class ServiceBusConfig
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string Namespace => ParseConnectionString().Namespace;
    public string SharedAccessKeyName => ParseConnectionString().SharedAccessKeyName;

    [Range(1, 100)]
    public int MaxConcurrentCalls { get; set; } = 16;

    [Range(1, 300)]
    public int MaxAutoRenewDurationMinutes { get; set; } = 5;

    private (string Namespace, string SharedAccessKeyName) ParseConnectionString()
    {
        var parts = ConnectionString
            .Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());

        return (
            Namespace: parts
                .GetValueOrDefault("endpoint", "")
                .Replace("sb://", "")
                .Replace(".servicebus.windows.net", ""),
            SharedAccessKeyName: parts.GetValueOrDefault("sharedaccesskeyname", "unknown")
        );
    }
}

/// <summary>
/// Azure Storage configuration.
/// </summary>
public class StorageConfig
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string AccountName => ParseConnectionString().AccountName;
    public string AccountKey => ParseConnectionString().AccountKey;

    [Range(1, 100)]
    public int MaxConcurrency { get; set; } = 8;

    [Range(1, 1000)]
    public int MaxRetries { get; set; } = 3;

    private (string AccountName, string AccountKey) ParseConnectionString()
    {
        var parts = ConnectionString
            .Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());

        return (
            AccountName: parts.GetValueOrDefault("accountname", "unknown"),
            AccountKey: parts.GetValueOrDefault("accountkey", "unknown")
        );
    }
}

/// <summary>
/// Application Insights telemetry configuration.
/// </summary>
public class TelemetryConfig
{
    [Required]
    public string InstrumentationKey { get; set; } = string.Empty;

    public bool EnableTelemetry { get; set; } = true;

    [Range(1, 100)]
    public int SamplingPercentage { get; set; } = 100;

    public bool EnableDependencyTracking { get; set; } = true;

    public bool EnablePerformanceCounters { get; set; } = true;
}

/// <summary>
/// Retry policy configuration.
/// </summary>
public class RetryConfig
{
    [Range(1, 10)]
    public int MaxRetryCount { get; set; } = 3;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(100, 10000)]
    public int BaseDelayMilliseconds { get; set; } = 1000;

    public bool EnableExponentialBackoff { get; set; } = true;

    [Range(1.0, 5.0)]
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Security configuration.
/// </summary>
public class SecurityConfig
{
    public bool RequireHttps { get; set; } = true;

    [Range(1, 24)]
    public int TokenExpirationHours { get; set; } = 1;

    public bool EnableAuditLogging { get; set; } = true;

    [Range(8, 64)]
    public int MinPasswordLength { get; set; } = 12;

    public bool RequireSpecialCharacters { get; set; } = true;
}
