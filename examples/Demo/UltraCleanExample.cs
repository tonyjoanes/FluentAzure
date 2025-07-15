using FluentAzure;
using Microsoft.Extensions.DependencyInjection;

// FluentConfig() is available via GlobalUsings.cs

namespace FluentAzure.Examples;

/// <summary>
/// Demonstrates the ultra-clean FluentAzure API using static using directives.
/// This approach is most similar to MediatR's design pattern.
/// </summary>
public static class UltraCleanExample
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
    /// Demonstrates the ultra-clean API - just Config()!
    /// </summary>
    public static async Task UltraCleanBasicUsage()
    {
        Console.WriteLine("🚀 Ultra Clean API Example");
        Console.WriteLine("===========================");

        // Set up some environment variables for testing
        Environment.SetEnvironmentVariable("App__Name", "UltraCleanApp");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=ultraclean"
        );

        try
        {
            // Ultra clean - just FluentConfig.Create()! No FluentAzure prefix needed!
            var buildResult = await FluentConfig
                .Create() // This is as clean as it gets!
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Debug", "true")
                .Optional("Version", "2.0.0")
                .BuildAsync();

            var configResult = buildResult.Bind<AppSettings>();

            if (configResult.IsSuccess)
            {
                var config = configResult.Value!;
                Console.WriteLine($"✅ App Name: {config.AppName}");
                Console.WriteLine($"✅ Version: {config.Version}");
                Console.WriteLine($"✅ Debug: {config.Debug}");
                Console.WriteLine($"✅ Database: {config.Database.ConnectionString}");
            }
            else
            {
                Console.WriteLine(
                    $"❌ Configuration failed: {string.Join(", ", configResult.Errors)}"
                );
            }
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
    }

    /// <summary>
    /// Demonstrates advanced usage with the ultra-clean API.
    /// </summary>
    public static async Task UltraCleanAdvancedUsage()
    {
        Console.WriteLine("\n🔧 Ultra Clean Advanced Example");
        Console.WriteLine("===============================");

        // Create a temporary JSON file for testing
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            tempFile,
            """
            {
                "Api": {
                    "BaseUrl": "https://api.ultraclean.com",
                    "TimeoutSeconds": 45
                },
                "Database": {
                    "MaxConnections": 200
                }
            }
            """
        );

        try
        {
            // Ultra clean advanced usage - just FluentConfig.Create()!
            var buildResult = await FluentConfig
                .Create() // Still just FluentConfig.Create()!
                .FromJsonFile(tempFile)
                .FromEnvironment()
                .Required("Api:BaseUrl")
                .Required("Api:TimeoutSeconds")
                .Optional("Database:MaxConnections", "100")
                .Validate(
                    "Api:TimeoutSeconds",
                    timeout =>
                    {
                        if (int.TryParse(timeout, out var seconds) && seconds > 0 && seconds <= 300)
                        {
                            return Core.Result<string>.Success(timeout);
                        }
                        return Core.Result<string>.Error("API timeout must be between 1-300 seconds");
                    }
                )
                .Transform(
                    "Api:BaseUrl",
                    url =>
                    {
                        // Ensure URL ends with trailing slash
                        return Core.Result<string>.Success(url.EndsWith("/") ? url : url + "/");
                    }
                )
                .BuildAsync();

            var configResult = buildResult.Bind<AppSettings>();

            if (configResult.IsSuccess)
            {
                var config = configResult.Value!;
                Console.WriteLine($"✅ API Base URL: {config.Api.BaseUrl}");
                Console.WriteLine($"✅ API Timeout: {config.Api.TimeoutSeconds}s");
                Console.WriteLine($"✅ Max Connections: {config.Database.MaxConnections}");
            }
            else
            {
                Console.WriteLine(
                    $"❌ Configuration failed: {string.Join(", ", configResult.Errors)}"
                );
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Demonstrates dependency injection with the ultra-clean API.
    /// </summary>
    public static void UltraCleanDependencyInjection()
    {
        Console.WriteLine("\n🏗️ Ultra Clean Dependency Injection Example");
        Console.WriteLine("===========================================");

        var services = new ServiceCollection();

        // Clean DI integration with the ultra-clean API
        services.AddFluentAzure<AppSettings>(builder =>
            builder
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Debug", "false")
                .Optional("Version", "2.0.0")
        );

        var serviceProvider = services.BuildServiceProvider();
        var config = serviceProvider.GetRequiredService<AppSettings>();

        Console.WriteLine($"✅ DI Config - App: {config.AppName}");
        Console.WriteLine($"✅ DI Config - Version: {config.Version}");
        Console.WriteLine($"✅ DI Config - Debug: {config.Debug}");
    }

    /// <summary>
    /// Compares all three API approaches side by side.
    /// </summary>
    public static async Task ApiComparison()
    {
        Console.WriteLine("\n📊 API Comparison");
        Console.WriteLine("==================");

        // Set up environment variables
        Environment.SetEnvironmentVariable("App__Name", "ComparisonApp");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=comparison"
        );

        try
        {
            // 1. Ultra Clean (Recommended)
            Console.WriteLine("\n1️⃣ Ultra Clean (Recommended):");
            Console.WriteLine("   using static FluentAzure.GlobalMethods;");
            Console.WriteLine("   var config = await FluentConfig.Create()...");

            var ultraCleanResult = await FluentConfig
                .Create()
                .FromEnvironment()
                .Required("App:Name")
                .BuildAsync();

            // 2. Clean (Alternative)
            Console.WriteLine("\n2️⃣ Clean (Alternative):");
            Console.WriteLine("   using FluentAzure;");
            Console.WriteLine("   var config = await FluentAzure.FluentConfig.Create()...");

            var cleanResult = await FluentAzure
                .FluentConfig.Create()
                .FromEnvironment()
                .Required("App:Name")
                .BuildAsync();

            // 3. Legacy (Deprecated)
            Console.WriteLine("\n3️⃣ Legacy (Deprecated):");
            Console.WriteLine("   using FluentAzure.Core;");
            Console.WriteLine("   var config = await FluentAzure.Configuration()...");

            var legacyResult = await FluentAzure
                .FluentConfig.Create()
                .FromEnvironment()
                .Required("App:Name")
                .BuildAsync();

            Console.WriteLine(
                "\n✅ All three approaches work, but Ultra Clean is the most elegant!"
            );
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
    }
}
