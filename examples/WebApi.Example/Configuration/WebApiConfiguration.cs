using System.ComponentModel.DataAnnotations;

namespace WebApi.Example.Configuration;

/// <summary>
/// Main configuration class for Web API application.
/// Demonstrates enterprise-level configuration with validation.
/// </summary>
public class WebApiConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public ServiceBusConfig ServiceBus { get; set; } = new();
    public JwtConfig Jwt { get; set; } = new();
    public CorsConfig Cors { get; set; } = new();
    public TelemetryConfig Telemetry { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();
}

/// <summary>
/// Database configuration with Entity Framework settings.
/// </summary>
public class DatabaseConfig
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string Name => ParseConnectionString().Database;
    public string Server => ParseConnectionString().Server;
    public int Port => ParseConnectionString().Port;
    public string Provider => ParseConnectionString().Provider;

    [Range(1, 100)]
    public int MaxRetryCount { get; set; } = 3;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableSensitiveDataLogging { get; set; } = false;

    public bool EnableDetailedErrors { get; set; } = false;

    private (string Server, int Port, string Database, string Provider) ParseConnectionString()
    {
        var parts = ConnectionString
            .Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());

        return (
            Server: parts.GetValueOrDefault("server", "localhost"),
            Port: int.TryParse(parts.GetValueOrDefault("port", "1433"), out var port) ? port : 1433,
            Database: parts.GetValueOrDefault("database", "default"),
            Provider: parts.GetValueOrDefault("provider", "sqlserver")
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

    [Range(1, 100)]
    public int MaxBlobSizeMB { get; set; } = 100;

    public string DefaultContainer { get; set; } = "uploads";

    public bool EnableSoftDelete { get; set; } = true;

    [Range(1, 365)]
    public int SoftDeleteRetentionDays { get; set; } = 30;

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

    [Range(1, 100)]
    public int PrefetchCount { get; set; } = 0;

    public string DefaultQueue { get; set; } = "default";

    public string DefaultTopic { get; set; } = "notifications";

    public bool EnableDeadLetterQueue { get; set; } = true;

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
/// JWT authentication configuration.
/// </summary>
public class JwtConfig
{
    [Required]
    [MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 24)]
    public int ExpirationHours { get; set; } = 1;

    [Range(1, 60)]
    public int RefreshTokenExpirationMinutes { get; set; } = 30;

    public bool ValidateIssuer { get; set; } = true;

    public bool ValidateAudience { get; set; } = true;

    public bool ValidateLifetime { get; set; } = true;

    public bool ValidateIssuerSigningKey { get; set; } = true;
}

/// <summary>
/// CORS configuration.
/// </summary>
public class CorsConfig
{
    [Required]
    public string AllowedOrigins { get; set; } = string.Empty;

    public string AllowedMethods { get; set; } = "GET,POST,PUT,DELETE,OPTIONS";

    public string AllowedHeaders { get; set; } = "Content-Type,Authorization";

    public bool AllowCredentials { get; set; } = true;

    [Range(1, 86400)]
    public int MaxAgeSeconds { get; set; } = 3600;

    public bool EnableCors { get; set; } = true;
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

    public bool EnableRequestTracking { get; set; } = true;

    public bool EnableExceptionTracking { get; set; } = true;

    [Range(1, 1000)]
    public int MaxTelemetryItemsPerSecond { get; set; } = 100;
}

/// <summary>
/// Security configuration.
/// </summary>
public class SecurityConfig
{
    public bool RequireHttps { get; set; } = true;

    public bool EnableHsts { get; set; } = true;

    [Range(1, 365)]
    public int HstsMaxAgeDays { get; set; } = 365;

    public bool IncludeSubDomains { get; set; } = true;

    public bool Preload { get; set; } = false;

    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";

    public bool EnableXssProtection { get; set; } = true;

    public bool EnableContentTypeSniffing { get; set; } = false;

    public bool EnableFrameOptions { get; set; } = true;

    public string FrameOptions { get; set; } = "DENY";

    [Range(8, 50)]
    public int MinPasswordLength { get; set; } = 12;

    public bool RequireSpecialCharacters { get; set; } = true;

    public bool EnableAuditLogging { get; set; } = true;
}

/// <summary>
/// Rate limiting configuration.
/// </summary>
public class RateLimitConfig
{
    public bool EnableRateLimiting { get; set; } = true;

    [Range(1, 10000)]
    public int RequestsPerMinute { get; set; } = 100;

    [Range(1, 1000)]
    public int RequestsPerHour { get; set; } = 1000;

    [Range(1, 100)]
    public int BurstLimit { get; set; } = 10;

    public bool EnableIpBasedLimiting { get; set; } = true;

    public bool EnableUserBasedLimiting { get; set; } = true;

    public string RateLimitHeader { get; set; } = "X-RateLimit-Limit";

    public string RateLimitRemainingHeader { get; set; } = "X-RateLimit-Remaining";

    public string RateLimitResetHeader { get; set; } = "X-RateLimit-Reset";
}

/// <summary>
/// Caching configuration.
/// </summary>
public class CacheConfig
{
    public bool EnableCaching { get; set; } = true;

    [Range(1, 3600)]
    public int DefaultExpirationMinutes { get; set; } = 30;

    [Range(1, 86400)]
    public int MaxExpirationMinutes { get; set; } = 1440;

    public bool EnableDistributedCache { get; set; } = false;

    public string CacheConnectionString { get; set; } = string.Empty;

    public string CacheInstanceName { get; set; } = "WebApi";

    [Range(1, 100)]
    public int MaxCacheSizeMB { get; set; } = 100;

    public bool EnableCompression { get; set; } = true;
}
