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

            // 3. Legacy (No longer available)
            Console.WriteLine("\n3️⃣ Legacy (No longer available):");
            Console.WriteLine("   // This API has been removed:");
            Console.WriteLine("   // var config = await FluentAzure.Configuration()...");
            Console.WriteLine("   // Use FluentConfig.Create() instead");

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

    /// <summary>
    /// Demonstrates the proper way to use FluentAzure's built-in safety features.
    /// This is the recommended approach - no need for additional Option<T> wrapping.
    /// </summary>
    public static async Task ProperFluentAzureSafety()
    {
        Console.WriteLine("\n🛡️ Proper FluentAzure Safety (Recommended)");
        Console.WriteLine("==========================================");

        // Set up environment variables for testing
        Environment.SetEnvironmentVariable("App__Name", "SafeApp");
        Environment.SetEnvironmentVariable("Database__ConnectionString", "Server=localhost;Database=safe");
        Environment.SetEnvironmentVariable("Api__TimeoutSeconds", "30");

        try
        {
            // Method 1: Required values - fails fast if missing
            var requiredResult = await FluentConfig
                .Create()
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .BuildAsync();

            requiredResult.Match(
                success =>
                {
                    // These are guaranteed to exist and be safe
                    var appName = success["App:Name"];
                    var connectionString = success["Database:ConnectionString"];
                    Console.WriteLine($"✅ Required - App: {appName}");
                    Console.WriteLine($"✅ Required - DB: {connectionString.Substring(0, Math.Min(20, connectionString.Length))}...");
                },
                errors => Console.WriteLine($"❌ Required config failed: {string.Join(", ", errors)}")
            );

            // Method 2: Optional values with defaults - always safe
            var optionalResult = await FluentConfig
                .Create()
                .FromEnvironment()
                .Optional("App:Name", "DefaultApp")
                .Optional("Api:TimeoutSeconds", "60")
                .Optional("Debug", "false")
                .BuildAsync();

            optionalResult.Match(
                success =>
                {
                    // These are always safe with defaults
                    var appName = success["App:Name"];
                    var timeout = int.Parse(success["Api:TimeoutSeconds"]);
                    var debug = bool.Parse(success["Debug"]);
                    Console.WriteLine($"✅ Optional - App: {appName}");
                    Console.WriteLine($"✅ Optional - Timeout: {timeout}s");
                    Console.WriteLine($"✅ Optional - Debug: {debug}");
                },
                errors => Console.WriteLine($"❌ Optional config failed: {string.Join(", ", errors)}")
            );

            // Method 3: Strongly-typed binding - safest approach
            var bindingResult = await FluentConfig
                .Create()
                .FromEnvironment()
                .Required("App:Name")
                .Optional("Api:TimeoutSeconds", "30")
                .Optional("Debug", "false")
                .BuildAsync()
                .Bind<AppSettings>();

            bindingResult.Match(
                success =>
                {
                    // Fully type-safe, no null checks needed
                    Console.WriteLine($"✅ Typed - App: {success.AppName}");
                    Console.WriteLine($"✅ Typed - Timeout: {success.Api.TimeoutSeconds}s");
                    Console.WriteLine($"✅ Typed - Debug: {success.Debug}");
                },
                errors => Console.WriteLine($"❌ Binding failed: {string.Join(", ", errors)}")
            );

            // Method 4: Validation with built-in safety
            var validatedResult = await FluentConfig
                .Create()
                .FromEnvironment()
                .Required("App:Name")
                .Optional("Api:TimeoutSeconds", "30")
                .Validate("Api:TimeoutSeconds", timeout =>
                {
                    if (int.TryParse(timeout, out var seconds) && seconds > 0 && seconds <= 300)
                    {
                        return Core.Result<string>.Success(timeout);
                    }
                    return Core.Result<string>.Error("Timeout must be between 1-300 seconds");
                })
                .BuildAsync()
                .Bind<AppSettings>();

            validatedResult.Match(
                success => Console.WriteLine($"✅ Validated - App: {success.AppName}, Timeout: {success.Api.TimeoutSeconds}s"),
                errors => Console.WriteLine($"❌ Validation failed: {string.Join(", ", errors)}")
            );
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
            Environment.SetEnvironmentVariable("Api__TimeoutSeconds", null);
        }
    }
}
