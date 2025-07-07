using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using FluentAzure.Core;
using FluentAzure.Extensions;
using FluentAzure.Sources;
using Microsoft.Extensions.Logging;

namespace Demo;

/// <summary>
/// Demo program showcasing the enhanced Azure Key Vault configuration source.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Create a logger for demonstration
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information)
        );
        var logger = loggerFactory.CreateLogger<Program>();

        Console.WriteLine("🔐 FluentAzure - Enhanced Key Vault Configuration Source Demo");
        Console.WriteLine(new string('-', 40));

        // Example 1: Basic Key Vault usage
        await DemoBasicKeyVault(logger);

        // Example 2: Advanced Key Vault configuration
        await DemoAdvancedKeyVault(logger);

        // Example 3: Different authentication methods
        await DemoAuthenticationMethods(logger);

        // Example 4: Secret versioning and caching
        await DemoVersioningAndCaching(logger);

        // Example 5: Error handling and partial success
        await DemoErrorHandling(logger);

        // Example 6: Key mapping and filtering
        await DemoKeyMappingAndFiltering(logger);

        // Example 7: Enhanced Configuration Binding
        await DemoEnhancedBinding();

        Console.WriteLine("\n✅ Demo completed successfully!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    /// <summary>
    /// Demonstrates basic Key Vault usage with default settings.
    /// </summary>
    private static async Task DemoBasicKeyVault(ILogger logger)
    {
        Console.WriteLine("\n📋 Example 1: Basic Key Vault Usage");
        Console.WriteLine(new string('-', 40));

        try
        {
            // Replace with your actual Key Vault URL
            const string vaultUrl = "https://fluentazure.vault.azure.net/";

            var config = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromEnvironment()
                .FromKeyVault(vaultUrl)
                .BuildAsync();

            config.Match(
                success =>
                {
                    logger.LogInformation("✅ Configuration loaded successfully");
                    logger.LogInformation("Found {Count} configuration values", success.Count);

                    // Display some sample values (be careful not to log sensitive data in production)
                    foreach (var kvp in success.Take(3))
                    {
                        logger.LogInformation(
                            "Key: {Key}, Value: {Value}",
                            kvp.Key,
                            kvp.Value.Length > 20 ? kvp.Value[..20] + "..." : kvp.Value
                        );
                    }
                },
                errors =>
                {
                    logger.LogWarning("⚠️ Configuration loading failed with errors:");
                    foreach (var error in errors)
                    {
                        logger.LogWarning("  - {Error}", error);
                    }
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Basic Key Vault demo failed");
        }
    }

    /// <summary>
    /// Demonstrates advanced Key Vault configuration with custom settings.
    /// </summary>
    private static async Task DemoAdvancedKeyVault(ILogger logger)
    {
        Console.WriteLine("\n📋 Example 2: Advanced Key Vault Configuration");
        Console.WriteLine(new string('-', 40));

        try
        {
            const string vaultUrl = "https://fluentazure.vault.azure.net/";

            var config = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromEnvironment()
                .FromKeyVault(
                    vaultUrl,
                    options =>
                    {
                        options.CacheDuration = TimeSpan.FromMinutes(10);
                        options.MaxRetryAttempts = 5;
                        options.BaseRetryDelay = TimeSpan.FromSeconds(2);
                        options.MaxRetryDelay = TimeSpan.FromMinutes(1);
                        options.ContinueOnSecretFailure = true;
                        options.OperationTimeout = TimeSpan.FromSeconds(45);
                    },
                    logger
                )
                .BuildAsync();

            config.Match(
                success =>
                {
                    logger.LogInformation("✅ Advanced configuration loaded successfully");
                    logger.LogInformation("Configuration contains {Count} values", success.Count);
                },
                errors =>
                {
                    logger.LogWarning("⚠️ Advanced configuration had errors:");
                    foreach (var error in errors)
                    {
                        logger.LogWarning("  - {Error}", error);
                    }
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Advanced Key Vault demo failed");
        }
    }

    /// <summary>
    /// Demonstrates different authentication methods.
    /// </summary>
    private static async Task DemoAuthenticationMethods(ILogger logger)
    {
        Console.WriteLine("\n📋 Example 3: Authentication Methods");
        Console.WriteLine(new string('-', 40));

        const string vaultUrl = "https://fluentazure.vault.azure.net/";

        // Method 1: Default Azure Credential (recommended for most scenarios)
        logger.LogInformation("🔑 Using DefaultAzureCredential");
        await DemoWithCredential(vaultUrl, logger, "Default", () => new DefaultAzureCredential());

        // Method 2: Managed Identity
        logger.LogInformation("🔑 Using ManagedIdentityCredential");
        await DemoWithCredential(
            vaultUrl,
            logger,
            "Managed Identity",
            () => new ManagedIdentityCredential()
        );

        // Method 3: Service Principal (for production environments)
        logger.LogInformation("🔑 Using Service Principal");
        // Note: In production, these would come from secure configuration
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

        if (
            !string.IsNullOrEmpty(tenantId)
            && !string.IsNullOrEmpty(clientId)
            && !string.IsNullOrEmpty(clientSecret)
        )
        {
            await DemoWithCredential(
                vaultUrl,
                logger,
                "Service Principal",
                () => new ClientSecretCredential(tenantId, clientId, clientSecret)
            );
        }
        else
        {
            logger.LogInformation("⚠️ Service Principal credentials not configured");
        }
    }

    /// <summary>
    /// Helper method to demonstrate authentication with different credentials.
    /// </summary>
    private static async Task DemoWithCredential(
        string vaultUrl,
        ILogger logger,
        string credentialType,
        Func<TokenCredential> credentialFactory
    )
    {
        try
        {
            var credential = credentialFactory();
            var config = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromKeyVault(vaultUrl, credential)
                .BuildAsync();

            config.Match(
                success =>
                    logger.LogInformation(
                        "✅ {CredentialType} authentication successful",
                        credentialType
                    ),
                errors =>
                    logger.LogWarning(
                        "⚠️ {CredentialType} authentication failed: {Error}",
                        credentialType,
                        errors.FirstOrDefault()
                    )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ {CredentialType} authentication error", credentialType);
        }
    }

    /// <summary>
    /// Demonstrates secret versioning and caching capabilities.
    /// </summary>
    private static async Task DemoVersioningAndCaching(ILogger logger)
    {
        Console.WriteLine("\n📋 Example 4: Secret Versioning and Caching");
        Console.WriteLine(new string('-', 40));

        try
        {
            const string vaultUrl = "https://fluentazure.vault.azure.net/";

            // Create a Key Vault source with caching
            var kvSource = new KeyVaultSource(
                vaultUrl,
                new KeyVaultConfiguration
                {
                    CacheDuration = TimeSpan.FromMinutes(5),
                    SecretVersion = null, // Get latest version
                },
                logger: logger
            );

            // Load configuration
            var loadResult = await kvSource.LoadAsync();

            loadResult.Match(
                success =>
                {
                    logger.LogInformation("✅ Secrets loaded with caching enabled");

                    // Show cache statistics
                    var stats = kvSource.CacheStatistics;
                    logger.LogInformation(
                        "Cache statistics: {Stats}",
                        string.Join(", ", stats.Select(s => $"{s.Key}: {s.Value}"))
                    );
                },
                errors =>
                    logger.LogWarning(
                        "⚠️ Secret loading failed: {Errors}",
                        string.Join(", ", errors)
                    )
            );

            // Demonstrate getting a specific secret with version
            var specificSecret = await kvSource.GetSecretAsync("MySecret", "version123");
            if (specificSecret != null)
            {
                logger.LogInformation("✅ Retrieved specific secret version");
            }
            else
            {
                logger.LogInformation("ℹ️ Specific secret version not found");
            }

            // Demonstrate cache clearing
            kvSource.ClearCache();
            logger.LogInformation("🧹 Cache cleared");

            // Dispose of the source
            kvSource.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Versioning and caching demo failed");
        }
    }

    /// <summary>
    /// Demonstrates error handling and partial success scenarios.
    /// </summary>
    private static async Task DemoErrorHandling(ILogger logger)
    {
        Console.WriteLine("\n📋 Example 5: Error Handling and Partial Success");
        Console.WriteLine(new string('-', 40));

        try
        {
            const string vaultUrl = "https://fluentazure.vault.azure.net/";

            var config = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromKeyVault(
                    vaultUrl,
                    options =>
                    {
                        options.ContinueOnSecretFailure = true; // Allow partial success
                        options.MaxRetryAttempts = 2;
                        options.BaseRetryDelay = TimeSpan.FromSeconds(1);
                    }
                )
                .BuildAsync();

            config.Match(
                success =>
                {
                    logger.LogInformation("✅ Configuration loaded with partial success");
                    logger.LogInformation(
                        "Successfully loaded {Count} configuration values",
                        success.Count
                    );
                },
                errors =>
                {
                    logger.LogWarning("⚠️ Configuration loading failed completely");
                    foreach (var error in errors)
                    {
                        logger.LogWarning("  - {Error}", error);
                    }
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error handling demo failed");
        }
    }

    /// <summary>
    /// Demonstrates key mapping and filtering capabilities.
    /// </summary>
    private static async Task DemoKeyMappingAndFiltering(ILogger logger)
    {
        Console.WriteLine("\n📋 Example 6: Key Mapping and Filtering");
        Console.WriteLine(new string('-', 40));

        try
        {
            const string vaultUrl = "https://fluentazure.vault.azure.net/";

            // Example with prefix filtering
            var configWithPrefix = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromKeyVaultWithPrefix(vaultUrl, "MyApp-")
                .BuildAsync();

            configWithPrefix.Match(
                success =>
                    logger.LogInformation(
                        "✅ Configuration with prefix filter loaded: {Count} values",
                        success.Count
                    ),
                errors =>
                    logger.LogWarning(
                        "⚠️ Prefix filtering failed: {Errors}",
                        string.Join(", ", errors)
                    )
            );

            // Example with custom key mapping
            var configWithMapping = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromKeyVault(
                    vaultUrl,
                    secretName =>
                    {
                        // Custom mapping: Convert MyApp-Database-ConnectionString to MyApp:Database:ConnectionString
                        return secretName.Replace("-", ":");
                    }
                )
                .BuildAsync();

            configWithMapping.Match(
                success =>
                    logger.LogInformation(
                        "✅ Configuration with custom key mapping loaded: {Count} values",
                        success.Count
                    ),
                errors =>
                    logger.LogWarning(
                        "⚠️ Custom key mapping failed: {Errors}",
                        string.Join(", ", errors)
                    )
            );

            // Example with caching settings
            var configWithCaching = await FluentAzure
                .Core.FluentAzure.Configuration()
                .FromKeyVaultWithCaching(vaultUrl, TimeSpan.FromMinutes(15))
                .BuildAsync();

            configWithCaching.Match(
                success =>
                    logger.LogInformation(
                        "✅ Configuration with custom caching loaded: {Count} values",
                        success.Count
                    ),
                errors =>
                    logger.LogWarning(
                        "⚠️ Custom caching failed: {Errors}",
                        string.Join(", ", errors)
                    )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Key mapping and filtering demo failed");
        }
    }

    /// <summary>
    /// Demonstrates the enhanced configuration binding system.
    /// </summary>
    private static async Task DemoEnhancedBinding()
    {
        Console.WriteLine("\n🔗 Enhanced Configuration Binding Demo");
        Console.WriteLine(new string('=', 60));

        // Run all enhanced binding examples
        await EnhancedBindingExamples.DemoBasicBinding();
        await EnhancedBindingExamples.DemoRecordBinding();
        await EnhancedBindingExamples.DemoCollectionBinding();
        await EnhancedBindingExamples.DemoDictionaryBinding();
        await EnhancedBindingExamples.DemoJsonBinding();
        await EnhancedBindingExamples.DemoValidationErrors();
        await EnhancedBindingExamples.DemoCustomValidation();
        await EnhancedBindingExamples.DemoBindingWithoutValidation();
    }
}

/// <summary>
/// String extension for creating separators.
/// </summary>
public static class StringExtensions
{
    public static string Repeat(this string str, int count)
    {
        return new string(str.ToCharArray().SelectMany(c => Enumerable.Repeat(c, count)).ToArray());
    }
}
