using FluentAzure;
using FluentAzure.Core;
using FluentAzure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Demo;

/// <summary>
/// Examples demonstrating how to use FluentAzure with Options for better error handling and type safety.
/// </summary>
public static class OptionBasedExamples
{
    /// <summary>
    /// Example 1: Basic Option-based configuration access
    /// </summary>
    public static async Task BasicOptionAccess()
    {
        Console.WriteLine("=== Basic Option-based Configuration Access ===");

        var config = await FluentConfig
            .Create()
            .FromJsonFile("appsettings.json")
            .FromEnvironment()
            .BuildOptionalAsync();

        // Safe configuration access with Options
        var connectionString = config
            .Bind(c => c.GetOptional("ConnectionStrings:DefaultConnection"))
            .GetValueOrDefault("Default connection string");

        var timeout = config
            .Bind(c => c.GetOptional<int>("Database:Timeout"))
            .GetValueOrDefault(30);

        var enableLogging = config
            .Bind(c => c.GetOptional<bool>("Logging:Enabled"))
            .GetValueOrDefault(true);

        Console.WriteLine($"Connection String: {connectionString}");
        Console.WriteLine($"Timeout: {timeout}");
        Console.WriteLine($"Enable Logging: {enableLogging}");
    }

    /// <summary>
    /// Example 2: Option-based configuration binding with validation
    /// </summary>
    public static async Task OptionBasedBinding()
    {
        Console.WriteLine("\n=== Option-based Configuration Binding ===");

        var configOption = await FluentConfig
            .Create()
            .FromJsonFile("appsettings.json")
            .FromEnvironment()
            .BuildOptionalAsync();

        // Bind with validation using Options
        var appConfig = configOption
            .Bind(config => config.BindOptional<AppConfig>())
            .Filter(config => !string.IsNullOrEmpty(config.ApiKey))
            .Filter(config => config.Timeout > 0)
            .GetValueOrDefault(
                new AppConfig
                {
                    Name = "Default App",
                    ApiKey = "default-key",
                    Timeout = 30,
                }
            );

        Console.WriteLine($"App Name: {appConfig.Name}");
        Console.WriteLine($"API Key: {appConfig.ApiKey}");
        Console.WriteLine($"Timeout: {appConfig.Timeout}");
    }

    /// <summary>
    /// Example 3: Advanced Option-based transformations
    /// </summary>
    public static async Task AdvancedTransformations()
    {
        Console.WriteLine("\n=== Advanced Option-based Transformations ===");

        var config = await FluentConfig
            .Create()
            .FromJsonFile("appsettings.json")
            .FromEnvironment()
            .TransformOptional(
                "ApiKey",
                value =>
                    value.StartsWith("sk-") ? Option<string>.Some(value) : Option<string>.None()
            )
            .TransformWithFallback(
                "Timeout",
                value =>
                    int.TryParse(value, out var timeout) && timeout > 0
                        ? Option<string>.Some(timeout.ToString())
                        : Option<string>.None(),
                "30"
            )
            .BuildOptionalAsync();

        config.Match(
            some => Console.WriteLine("Configuration loaded successfully"),
            () => Console.WriteLine("Configuration loading failed")
        );
    }

    /// <summary>
    /// Example 4: Option-based service registration
    /// </summary>
    public static void OptionBasedServiceRegistration()
    {
        Console.WriteLine("\n=== Option-based Service Registration ===");

        var services = new ServiceCollection();

        // Option-based registration with graceful degradation
        services.AddFluentAzureOptional<DatabaseConfig>(builder =>
            builder
                .FromJsonFile("appsettings.json")
                .FromEnvironment()
                .Required("ConnectionString")
                .Optional("Timeout", 30)
        );

        // Registration with fallback
        services.AddFluentAzureWithFallback<LoggingConfig>(
            builder => builder.FromJsonFile("appsettings.json").FromEnvironment(),
            new LoggingConfig { Level = "Information", EnableConsole = true }
        );

        // Conditional registration
        services.AddFluentAzureConditional<FeatureConfig>(
            builder => builder.FromJsonFile("appsettings.json").FromEnvironment(),
            config => config.Environment == "Production",
            new FeatureConfig { Environment = "Development" }
        );

        Console.WriteLine("Services registered with Option-based error handling");
    }

    /// <summary>
    /// Example 5: Functional composition with Options
    /// </summary>
    public static async Task FunctionalComposition()
    {
        Console.WriteLine("\n=== Functional Composition with Options ===");

        var configOption = await FluentConfig
            .Create()
            .FromJsonFile("appsettings.json")
            .FromEnvironment()
            .BuildOptionalAsync();

        // Functional composition with Options
        var result = configOption
            .Bind(config => config.GetOptional("Database:ConnectionString"))
            .Bind(connectionString =>
                connectionString.Contains("Server=")
                    ? Option<string>.Some(connectionString)
                    : Option<string>.None()
            )
            .Map(connectionString => new DatabaseConnection(connectionString))
            .Map(db => db.Validate())
            .GetValueOrDefault(new DatabaseConnection("Default connection"));

        Console.WriteLine($"Database connection: {result.ConnectionString}");
        Console.WriteLine($"Is valid: {result.IsValid}");
    }

    /// <summary>
    /// Example 6: Error handling with Options and Results
    /// </summary>
    public static async Task ErrorHandlingWithOptions()
    {
        Console.WriteLine("\n=== Error Handling with Options and Results ===");

        var configResult = await FluentConfig
            .Create()
            .FromJsonFile("appsettings.json")
            .FromEnvironment()
            .Required("ApiKey")
            .Required("BaseUrl")
            .BuildAsync();

        // Convert Result to Option for safe access
        var configOption = configResult.ToOption();

        // Handle missing configuration gracefully
        var apiConfig = configOption
            .Bind(config => config.BindOptional<ApiConfig>())
            .Match(
                some => some,
                () => new ApiConfig { BaseUrl = "https://api.default.com", Timeout = 30 }
            );

        // Validate configuration with custom error messages
        var validationResult = configOption
            .Bind(config => config.BindOptional<ApiConfig>())
            .ToResult("Configuration binding failed")
            .Bind(config =>
                !string.IsNullOrEmpty(config.BaseUrl) && config.BaseUrl.StartsWith("https://")
                    ? Result<ApiConfig>.Success(config)
                    : Result<ApiConfig>.Error($"Invalid API URL format: {config.BaseUrl}")
            );

        validationResult.Match(
            success => Console.WriteLine("Configuration validation passed"),
            errors => Console.WriteLine($"Validation failed: {string.Join(", ", errors)}")
        );
    }

    /// <summary>
    /// Example 7: Async operations with Options
    /// </summary>
    public static async Task AsyncOperationsWithOptions()
    {
        Console.WriteLine("\n=== Async Operations with Options ===");

        var configOption = await FluentConfig
            .Create()
            .FromJsonFile("appsettings.json")
            .FromEnvironment()
            .BuildOptionalAsync();

        // Async operations with Options
        var processedConfig = await configOption.MapAsync(async config =>
        {
            // Simulate async processing
            await Task.Delay(100);
            return config;
        });

        var finalResult = await processedConfig.MapAsync(async config =>
        {
            var connectionString = config.GetOptional("ConnectionString");
            if (connectionString.HasValue)
            {
                // Simulate async validation
                await Task.Delay(50);
                return Option<Dictionary<string, string>>.Some(config);
            }
            return Option<Dictionary<string, string>>.None();
        });

        finalResult.Match(
            some => Console.WriteLine("Configuration processed successfully"),
            () => Console.WriteLine("Configuration processing failed")
        );
    }
}

// Example configuration classes - using existing ones from other files
public class AppConfig
{
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int Timeout { get; set; }
}

public class FeatureConfig
{
    public string Environment { get; set; } = string.Empty;
}

public class DatabaseConnection
{
    public string ConnectionString { get; }
    public bool IsValid { get; }

    public DatabaseConnection(string connectionString)
    {
        ConnectionString = connectionString;
        IsValid = !string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server=");
    }

    public DatabaseConnection Validate()
    {
        return this; // In real scenario, would perform actual validation
    }
}
