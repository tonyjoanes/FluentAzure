using FluentAzure;
using FluentAzure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAzure.Examples;

/// <summary>
/// Example demonstrating how to use FluentAzure with Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExample
{
    /// <summary>
    /// Example configuration class that will be bound from various sources.
    /// </summary>
    public class AppSettings
    {
        public string AppName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool Debug { get; set; }
        public DatabaseSettings Database { get; set; } = new();
        public ApiSettings Api { get; set; } = new();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; }
        public int MaxConnections { get; set; }
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Demonstrates basic usage of AddFluentAzure with DI.
    /// </summary>
    public static void BasicUsage()
    {
        var services = new ServiceCollection();

        services.AddFluentAzure<AppSettings>(builder =>
            builder
                .FromJsonFile("appsettings.json")
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Debug", "false")
        );

        var serviceProvider = services.BuildServiceProvider();
        var config = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine($"App: {config.AppName}");
        Console.WriteLine($"Version: {config.Version}");
        Console.WriteLine($"Debug: {config.Debug}");
    }

    /// <summary>
    /// Demonstrates advanced usage with Key Vault and validation.
    /// </summary>
    public static void AdvancedUsage()
    {
        var services = new ServiceCollection();

        services.AddFluentAzure<AppSettings>(
            builder =>
                builder
                    .FromJsonFile("appsettings.json")
                    .FromEnvironment()
                    .FromKeyVault("https://my-keyvault.vault.azure.net/")
                    .Required("App:Name")
                    .Required("Database:ConnectionString")
                    .Required("Api:ApiKey")
                    .Optional("Debug", "false")
                    .Validate("Database:TimeoutSeconds", timeout =>
                    {
                        if (int.TryParse(timeout, out var seconds) && seconds > 0)
                        {
                            return Result<string>.Success(timeout);
                        }
                        return Result<string>.Error("Timeout must be a positive integer");
                    }),
            config =>
            {
                // Post-processing: ensure API URL ends with trailing slash
                if (!config.Api.BaseUrl.EndsWith("/"))
                {
                    config.Api.BaseUrl += "/";
                }
                return config;
            }
        );

        var serviceProvider = services.BuildServiceProvider();
        var appConfig = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine($"App: {appConfig.AppName}");
        Console.WriteLine($"API Base URL: {appConfig.Api.BaseUrl}");
    }
}
