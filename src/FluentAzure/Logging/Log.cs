using Microsoft.Extensions.Logging;

namespace FluentAzure.Logging;

/// <summary>
/// Source-generated log messages for the Azure sources. Messages name keys, vaults and counts, never values.
/// </summary>
internal static partial class Log
{
    // Key Vault source (1000-1099)
    [LoggerMessage(1000, LogLevel.Information, "KeyVaultSource initialized for vault: {VaultUrl}")]
    public static partial void KeyVaultInitialized(this ILogger logger, string vaultUrl);

    [LoggerMessage(1001, LogLevel.Information, "Loading secrets from Key Vault: {VaultUrl}")]
    public static partial void KeyVaultLoading(this ILogger logger, string vaultUrl);

    [LoggerMessage(1002, LogLevel.Information, "Reloading secrets from Key Vault: {VaultUrl}")]
    public static partial void KeyVaultReloading(this ILogger logger, string vaultUrl);

    [LoggerMessage(1003, LogLevel.Information, "Loaded {Count} secrets from Key Vault with {ErrorCount} errors")]
    public static partial void KeyVaultLoaded(this ILogger logger, int count, int errorCount);

    [LoggerMessage(1004, LogLevel.Error, "Key Vault load operation failed")]
    public static partial void KeyVaultLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(1005, LogLevel.Debug, "Skipping disabled, expired or not-yet-active secret '{SecretName}'")]
    public static partial void KeyVaultSecretSkipped(this ILogger logger, string secretName);

    [LoggerMessage(1006, LogLevel.Warning, "Failed to load secret '{SecretName}': {Error}")]
    public static partial void KeyVaultSecretLoadFailed(this ILogger logger, string secretName, string? error);

    [LoggerMessage(1007, LogLevel.Error, "Failed to get secret '{SecretName}' from Key Vault")]
    public static partial void KeyVaultGetSecretFailed(this ILogger logger, Exception exception, string secretName);

    [LoggerMessage(1008, LogLevel.Warning, "Retrying Key Vault operation (attempt {AttemptNumber}): {Exception}")]
    public static partial void KeyVaultRetrying(this ILogger logger, int attemptNumber, string? exception);

    [LoggerMessage(1009, LogLevel.Information, "Key Vault cache cleared")]
    public static partial void KeyVaultCacheCleared(this ILogger logger);

    [LoggerMessage(1010, LogLevel.Information, "KeyVaultSource disposed")]
    public static partial void KeyVaultDisposed(this ILogger logger);

    // Key Vault secret cache (1100-1199)
    [LoggerMessage(1100, LogLevel.Debug, "Cache entry for key '{Key}' has expired and was removed")]
    public static partial void CacheEntryExpired(this ILogger logger, string key);

    [LoggerMessage(1101, LogLevel.Debug, "Cache hit for key '{Key}'")]
    public static partial void CacheHit(this ILogger logger, string key);

    [LoggerMessage(1102, LogLevel.Debug, "Cached value for key '{Key}' with TTL {Ttl}")]
    public static partial void CacheEntryAdded(this ILogger logger, string key, TimeSpan ttl);

    [LoggerMessage(1103, LogLevel.Debug, "Removed cache entry for key '{Key}'")]
    public static partial void CacheEntryRemoved(this ILogger logger, string key);

    [LoggerMessage(1104, LogLevel.Debug, "Cleared {Count} cache entries")]
    public static partial void CacheCleared(this ILogger logger, int count);

    [LoggerMessage(1105, LogLevel.Debug, "Cleaned up {Count} expired cache entries")]
    public static partial void CacheExpiredEntriesRemoved(this ILogger logger, int count);

    // App Configuration source (1200-1299)
    [LoggerMessage(1200, LogLevel.Debug, "App Configuration sentinel unchanged; skipping reload")]
    public static partial void AppConfigurationSentinelUnchanged(this ILogger logger);

    [LoggerMessage(1201, LogLevel.Information, "Loaded {Count} values from Azure App Configuration")]
    public static partial void AppConfigurationLoaded(this ILogger logger, int count);

    [LoggerMessage(1202, LogLevel.Error, "Azure App Configuration load failed")]
    public static partial void AppConfigurationLoadFailed(this ILogger logger, Exception exception);
}
