using FluentAzure;
using FluentAzure.Core;
using FluentAzure.Extensions;
using Microsoft.Extensions.DependencyInjection;

// FluentConfig() is available via GlobalUsings.cs

namespace FluentAzure.Examples;

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
/// Demonstrates the new cleaner FluentAzure API with ultra-clean FluentConfig().
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 FluentAzure Demo - Ultra Clean API Example");
        Console.WriteLine("==============================================");

        // Example 1: Basic usage with the new FluentConfig() method
        Console.WriteLine("\n1. Basic Configuration (Ultra Clean):");
        await BasicConfigurationExample();

        // Example 2: Advanced usage with validation and transformation
        Console.WriteLine("\n2. Advanced Configuration (Ultra Clean):");
        await AdvancedConfigurationExample();

        // Example 3: Dependency Injection integration
        Console.WriteLine("\n3. Dependency Injection Integration:");
        await DependencyInjectionExample();

        // Example 4: Traditional approach for comparison
        Console.WriteLine("\n4. Traditional Approach (for comparison):");
        await TraditionalApproachExample();

        Console.WriteLine("\n✅ All examples completed successfully!");
    }

    private static async Task BasicConfigurationExample()
    {
        // Set up some environment variables for testing
        Environment.SetEnvironmentVariable("App__Name", "MyAwesomeApp");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=test"
        );

        try
        {
            // Ultra clean API - just FluentConfig()!
            var buildResult = await FluentConfig() // Ultra clean!
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Debug", "true")
                .Optional("Version", "1.0.0")
                .BuildAsync();

            var result = buildResult.Bind<AppSettings>();

            if (result.IsSuccess)
            {
                var config = result.Value!;
                Console.WriteLine($"  App Name: {config.AppName}");
                Console.WriteLine($"  Version: {config.Version}");
                Console.WriteLine($"  Debug: {config.Debug}");
                Console.WriteLine($"  Database: {config.Database.ConnectionString}");
            }
            else
            {
                Console.WriteLine($"  Configuration failed: {string.Join(", ", result.Errors)}");
            }
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
    }

    private static async Task AdvancedConfigurationExample()
    {
        // Create a temporary JSON file for testing
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            tempFile,
            """
            {
                "Api": {
                    "BaseUrl": "https://api.example.com",
                    "TimeoutSeconds": 30
                },
                "Database": {
                    "MaxConnections": 100
                }
            }
            """
        );

        try
        {
            // Ultra clean API
            var buildResult = await FluentConfig() // Ultra clean!
                .FromJsonFile(tempFile)
                .FromEnvironment()
                .Required("Api:BaseUrl")
                .Required("Api:TimeoutSeconds")
                .Optional("Database:MaxConnections", "50")
                .Validate(
                    "Api:TimeoutSeconds",
                    timeout =>
                    {
                        if (int.TryParse(timeout, out var seconds) && seconds > 0 && seconds <= 300)
                        {
                            return Result<string>.Success(timeout);
                        }
                        return Result<string>.Error("API timeout must be between 1-300 seconds");
                    }
                )
                .Transform(
                    "Api:BaseUrl",
                    url =>
                    {
                        // Ensure URL ends with trailing slash
                        return Result<string>.Success(url.EndsWith("/") ? url : url + "/");
                    }
                )
                .BuildAsync();

            var result = buildResult.Bind<AppSettings>();

            if (result.IsSuccess)
            {
                var config = result.Value!;
                Console.WriteLine($"  API Base URL: {config.Api.BaseUrl}");
                Console.WriteLine($"  API Timeout: {config.Api.TimeoutSeconds}s");
                Console.WriteLine($"  Max Connections: {config.Database.MaxConnections}");
            }
            else
            {
                Console.WriteLine($"  Configuration failed: {string.Join(", ", result.Errors)}");
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static async Task DependencyInjectionExample()
    {
        var services = new ServiceCollection();

        // Clean DI integration with the new API
        services.AddFluentAzure<AppSettings>(builder =>
            builder
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Debug", "false")
                .Optional("Version", "1.0.0")
        );

        var serviceProvider = services.BuildServiceProvider();
        var config = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine($"  DI Config - App: {config.AppName}");
        Console.WriteLine($"  DI Config - Version: {config.Version}");
        Console.WriteLine($"  DI Config - Debug: {config.Debug}");
    }

    private static async Task TraditionalApproachExample()
    {
        // Set up some environment variables for testing
        Environment.SetEnvironmentVariable("App__Name", "TraditionalApp");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=traditional"
        );

        try
        {
            // Traditional approach for comparison
            var buildResult = await FluentAzure
                .FluentConfig() // Still clean, but requires FluentAzure prefix
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Debug", "true")
                .Optional("Version", "1.0.0")
                .BuildAsync();

            var result = buildResult.Bind<AppSettings>();

            if (result.IsSuccess)
            {
                var config = result.Value!;
                Console.WriteLine($"  Traditional - App Name: {config.AppName}");
                Console.WriteLine($"  Traditional - Version: {config.Version}");
                Console.WriteLine($"  Traditional - Debug: {config.Debug}");
                Console.WriteLine($"  Traditional - Database: {config.Database.ConnectionString}");
            }
            else
            {
                Console.WriteLine($"  Configuration failed: {string.Join(", ", result.Errors)}");
            }
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
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
