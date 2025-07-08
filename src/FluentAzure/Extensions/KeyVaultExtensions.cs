using Azure.Core;
using Azure.Identity;
using FluentAzure.Core;
using FluentAzure.Sources;
using Microsoft.Extensions.Logging;

namespace FluentAzure.Extensions;

/// <summary>
/// Extension methods for adding Azure Key Vault configuration sources.
/// </summary>
public static class KeyVaultExtensions
{
    /// <summary>
    /// Adds an Azure Key Vault configuration source with default settings.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVault(
        this ConfigurationBuilder builder,
        string vaultUrl,
        int priority = 200
    )
    {
        return builder.AddSource(new KeyVaultSource(vaultUrl, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with custom configuration.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="configure">Action to configure the Key Vault options.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVault(
        this ConfigurationBuilder builder,
        string vaultUrl,
        Action<KeyVaultConfiguration> configure,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration();
        configure(configuration);
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with custom configuration and logging.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="configure">Action to configure the Key Vault options.</param>
    /// <param name="logger">The logger for Key Vault operations.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVault(
        this ConfigurationBuilder builder,
        string vaultUrl,
        Action<KeyVaultConfiguration> configure,
        ILogger logger,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration();
        configure(configuration);
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority, logger));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with a specific credential.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="credential">The Azure credential to use for authentication.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVault(
        this ConfigurationBuilder builder,
        string vaultUrl,
        TokenCredential credential,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration { Credential = credential };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with managed identity authentication.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="managedIdentityClientId">The client ID of the managed identity to use.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVaultWithManagedIdentity(
        this ConfigurationBuilder builder,
        string vaultUrl,
        string? managedIdentityClientId = null,
        int priority = 200
    )
    {
        var credential =
            managedIdentityClientId != null
                ? new ManagedIdentityCredential(managedIdentityClientId)
                : new ManagedIdentityCredential();

        var configuration = new KeyVaultConfiguration { Credential = credential };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with service principal authentication.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="clientId">The client ID of the service principal.</param>
    /// <param name="clientSecret">The client secret of the service principal.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVaultWithServicePrincipal(
        this ConfigurationBuilder builder,
        string vaultUrl,
        string clientId,
        string clientSecret,
        string tenantId,
        int priority = 200
    )
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var configuration = new KeyVaultConfiguration { Credential = credential };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with a specific secret version.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="secretVersion">The specific version of secrets to retrieve.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVault(
        this ConfigurationBuilder builder,
        string vaultUrl,
        string secretVersion,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration { SecretVersion = secretVersion };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with a secret name prefix filter.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="secretNamePrefix">The prefix to filter secret names.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVaultWithPrefix(
        this ConfigurationBuilder builder,
        string vaultUrl,
        string secretNamePrefix,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration { SecretNamePrefix = secretNamePrefix };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with custom key mapping.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="keyMapper">Function to map Key Vault secret names to configuration keys.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVault(
        this ConfigurationBuilder builder,
        string vaultUrl,
        Func<string, string> keyMapper,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration { KeyMapper = keyMapper };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with custom caching settings.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="cacheDuration">How long to cache secrets.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVaultWithCaching(
        this ConfigurationBuilder builder,
        string vaultUrl,
        TimeSpan cacheDuration,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration { CacheDuration = cacheDuration };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an Azure Key Vault configuration source with custom retry settings.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts.</param>
    /// <param name="baseRetryDelay">Base delay for exponential backoff.</param>
    /// <param name="maxRetryDelay">Maximum delay between retries.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromKeyVaultWithRetry(
        this ConfigurationBuilder builder,
        string vaultUrl,
        int maxRetryAttempts,
        TimeSpan baseRetryDelay,
        TimeSpan maxRetryDelay,
        int priority = 200
    )
    {
        var configuration = new KeyVaultConfiguration
        {
            MaxRetryAttempts = maxRetryAttempts,
            BaseRetryDelay = baseRetryDelay,
            MaxRetryDelay = maxRetryDelay,
        };
        return builder.AddSource(new KeyVaultSource(vaultUrl, configuration, priority));
    }

    /// <summary>
    /// Adds an in-memory configuration source to the configuration pipeline.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="values">The in-memory configuration dictionary.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public static ConfigurationBuilder FromInMemory(
        this ConfigurationBuilder builder,
        Dictionary<string, string> values,
        int priority = 1000
    )
    {
        return builder.AddSource(new InMemorySource(values, priority));
    }
}
