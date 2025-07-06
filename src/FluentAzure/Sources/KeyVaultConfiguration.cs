using Azure.Core;
using Azure.Identity;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration options for the Key Vault configuration source.
/// </summary>
public class KeyVaultConfiguration
{
    /// <summary>
    /// Gets or sets the Azure credential to use for authentication.
    /// Defaults to DefaultAzureCredential if not specified.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Defaults to 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay for exponential backoff retry.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the cache duration for secrets.
    /// Defaults to 5 minutes. Set to TimeSpan.Zero to disable caching.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to continue loading other secrets when one fails.
    /// Defaults to true for partial success scenarios.
    /// </summary>
    public bool ContinueOnSecretFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the function to transform Key Vault secret names to configuration keys.
    /// Defaults to replacing '--' with ':' for hierarchical configuration.
    /// </summary>
    public Func<string, string> KeyMapper { get; set; } = defaultKey => defaultKey.Replace("--", ":");

    /// <summary>
    /// Gets or sets the secret version to retrieve. If null, retrieves the latest version.
    /// </summary>
    public string? SecretVersion { get; set; }

    /// <summary>
    /// Gets or sets the prefix filter for secret names. Only secrets starting with this prefix will be loaded.
    /// If null or empty, all secrets are loaded.
    /// </summary>
    public string? SecretNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets whether to reload secrets that fail to load during initial load.
    /// Defaults to true.
    /// </summary>
    public bool ReloadFailedSecrets { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for Key Vault operations.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
