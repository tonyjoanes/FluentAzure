using FluentAzure.Extensions;
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

        // Basic usage - binds AppSettings and registers it as a singleton
        services.AddFluentAzure<AppSettings>(config => config
            .FromEnvironment()
            .FromJsonFile("appsettings.json")
            .Required("AppName")
            .Required("Database:ConnectionString")
            .Required("Api:ApiKey")
            .Optional("Debug", "false")
            .Optional("Database:TimeoutSeconds", "30")
            .Optional("Api:TimeoutSeconds", "60")
        );

        var serviceProvider = services.BuildServiceProvider();
        var appSettings = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine($"App: {appSettings.AppName} v{appSettings.Version}");
        Console.WriteLine($"Database: {appSettings.Database.ConnectionString}");
        Console.WriteLine($"API: {appSettings.Api.BaseUrl}");
    }

    /// <summary>
    /// Demonstrates usage with custom service lifetime.
    /// </summary>
    public static void WithCustomLifetime()
    {
        var services = new ServiceCollection();

        // Register with scoped lifetime instead of singleton
        services.AddFluentAzure<AppSettings>(
            config => config
                .FromEnvironment()
                .FromJsonFile("appsettings.json")
                .Required("AppName")
                .Required("Database:ConnectionString"),
            ServiceLifetime.Scoped
        );

        var serviceProvider = services.BuildServiceProvider();

        // Each scope will get its own instance
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var settings1 = scope1.ServiceProvider.GetRequiredService<AppSettings>();
        var settings2 = scope2.ServiceProvider.GetRequiredService<AppSettings>();

        // These are different instances due to scoped lifetime
        Console.WriteLine($"Instance 1: {settings1.GetHashCode()}");
        Console.WriteLine($"Instance 2: {settings2.GetHashCode()}");
    }

    /// <summary>
    /// Demonstrates usage with Key Vault integration.
    /// </summary>
    public static void WithKeyVault()
    {
        var services = new ServiceCollection();

        // Configure with Key Vault for sensitive data
        services.AddFluentAzure<AppSettings>(config => config
            .FromEnvironment()
            .FromJsonFile("appsettings.json")
            .FromKeyVault("https://your-keyvault.vault.azure.net/")
            .Required("AppName")
            .Required("Database:ConnectionString") // This could come from Key Vault
            .Required("Api:ApiKey") // This could come from Key Vault
            .Optional("Debug", "false")
        );

        var serviceProvider = services.BuildServiceProvider();
        var appSettings = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine("Configuration loaded with Key Vault integration");
    }

    /// <summary>
    /// Demonstrates usage with factory method for post-processing.
    /// </summary>
    public static void WithFactory()
    {
        var services = new ServiceCollection();

        // Use factory to modify configuration after binding
        services.AddFluentAzure<AppSettings>(
            config => config
                .FromEnvironment()
                .FromJsonFile("appsettings.json")
                .Required("AppName")
                .Required("Database:ConnectionString"),
            settings =>
            {
                // Post-process the configuration
                if (string.IsNullOrEmpty(settings.Version))
                {
                    settings.Version = "1.0.0";
                }

                // Add environment-specific modifications
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    settings.Debug = true;
                }

                return settings;
            }
        );

        var serviceProvider = services.BuildServiceProvider();
        var appSettings = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine($"Post-processed config: {appSettings.AppName} v{appSettings.Version}");
    }

    /// <summary>
    /// Demonstrates usage in a typical ASP.NET Core Program.cs scenario.
    /// </summary>
    public static void AspNetCoreExample()
    {
        // This is what you would typically put in Program.cs or Startup.cs
        var builder = WebApplication.CreateBuilder();

        // Add FluentAzure configuration
        builder.Services.AddFluentAzure<AppSettings>(config => config
            .FromEnvironment()
            .FromJsonFile("appsettings.json")
            .FromJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
            .FromKeyVault("https://your-keyvault.vault.azure.net/")
            .Required("AppName")
            .Required("Database:ConnectionString")
            .Required("Api:ApiKey")
            .Optional("Debug", "false")
            .Validate("Database:TimeoutSeconds", timeout =>
            {
                if (int.TryParse(timeout, out var seconds) && seconds > 0 && seconds <= 300)
                {
                    return Result<string>.Success(timeout);
                }
                return Result<string>.Error("Database timeout must be between 1-300 seconds");
            })
        );

        // Now you can inject AppSettings into your controllers/services
        builder.Services.AddControllers();
        builder.Services.AddScoped<MyService>();

        var app = builder.Build();
        app.Run();
    }

    /// <summary>
    /// Example service that uses the injected configuration.
    /// </summary>
    public class MyService
    {
        private readonly AppSettings _settings;

        public MyService(AppSettings settings)
        {
            _settings = settings;
        }

        public void DoSomething()
        {
            Console.WriteLine($"Using configuration: {_settings.AppName}");
            // Use the configuration...
        }
    }
}
