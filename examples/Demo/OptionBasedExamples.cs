using FluentAzure;
using FluentAzure.Core;
using FluentAzure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAzure.Examples;

/// <summary>
/// Demonstrates how to use Option<T> monad to create more robust and type-safe FluentAzure configurations.
/// This approach provides better error handling, null safety, and functional programming patterns.
/// </summary>
public static class OptionBasedExamples
{
    /// <summary>
    /// Example configuration class for demonstration.
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
    /// Demonstrates basic Option-based configuration access.
    /// </summary>
    public static async Task BasicOptionUsage()
    {
        Console.WriteLine("🔧 Basic Option-Based Configuration");
        Console.WriteLine("===================================");

        // Set up environment variables for testing
        Environment.SetEnvironmentVariable("App__Name", "OptionApp");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=optiondb"
        );
        Environment.SetEnvironmentVariable("Api__TimeoutSeconds", "30");

        try
        {
            var configResult = await FluentConfig.Create().FromEnvironment().BuildAsync();

            // Method 1: Using GetOption<T>() - returns Option<T>
            var appNameOption = configResult.GetOption<string>("App:Name");
            var timeoutOption = configResult.GetOption<int>("Api:TimeoutSeconds");
            var missingOption = configResult.GetOption<string>("Missing:Key");

            // Handle options using Match
            appNameOption.Match(
                some: name => Console.WriteLine($"✅ App Name: {name}"),
                none: () => Console.WriteLine("❌ App Name not found")
            );

            timeoutOption.Match(
                some: timeout => Console.WriteLine($"✅ API Timeout: {timeout}s"),
                none: () => Console.WriteLine("❌ API Timeout not found")
            );

            missingOption.Match(
                some: value => Console.WriteLine($"✅ Missing Key: {value}"),
                none: () => Console.WriteLine("✅ Missing Key: None (as expected)")
            );

            // Method 2: Using GetValueOrDefault
            var appName = appNameOption.GetValueOrDefault("DefaultApp");
            var timeout = timeoutOption.GetValueOrDefault(60);
            var missing = missingOption.GetValueOrDefault("DefaultValue");

            Console.WriteLine(
                $"📊 With Defaults - App: {appName}, Timeout: {timeout}, Missing: {missing}"
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

    /// <summary>
    /// Demonstrates Option-based configuration with validation.
    /// </summary>
    public static async Task OptionWithValidation()
    {
        Console.WriteLine("\n🔍 Option-Based Configuration with Validation");
        Console.WriteLine("=============================================");

        // Set up environment variables
        Environment.SetEnvironmentVariable("Api__BaseUrl", "https://api.example.com");
        Environment.SetEnvironmentVariable("Api__TimeoutSeconds", "300"); // Invalid: too high
        Environment.SetEnvironmentVariable("Database__MaxConnections", "50");

        try
        {
            var configResult = await FluentConfig.Create().FromEnvironment().BuildAsync();

            // Option with validation
            var validUrlOption = configResult.GetOption<string>(
                "Api:BaseUrl",
                url => Uri.IsWellFormedUriString(url, UriKind.Absolute)
            );

            var validTimeoutOption = configResult.GetOption<int>(
                "Api:TimeoutSeconds",
                timeout => timeout > 0 && timeout <= 300
            );

            var validConnectionsOption = configResult.GetOption<int>(
                "Database:MaxConnections",
                connections => connections > 0 && connections <= 1000
            );

            // Handle validated options
            validUrlOption.Match(
                some: url => Console.WriteLine($"✅ Valid URL: {url}"),
                none: () => Console.WriteLine("❌ Invalid or missing URL")
            );

            validTimeoutOption.Match(
                some: timeout => Console.WriteLine($"✅ Valid Timeout: {timeout}s"),
                none: () => Console.WriteLine("❌ Invalid timeout (must be 1-300 seconds)")
            );

            validConnectionsOption.Match(
                some: connections => Console.WriteLine($"✅ Valid Connections: {connections}"),
                none: () => Console.WriteLine("❌ Invalid connections (must be 1-1000)")
            );
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("Api__BaseUrl", null);
            Environment.SetEnvironmentVariable("Api__TimeoutSeconds", null);
            Environment.SetEnvironmentVariable("Database__MaxConnections", null);
        }
    }

    /// <summary>
    /// Demonstrates functional composition with Option<T>.
    /// </summary>
    public static async Task FunctionalComposition()
    {
        Console.WriteLine("\n🔄 Functional Composition with Options");
        Console.WriteLine("=====================================");

        Environment.SetEnvironmentVariable("App__Name", "CompositionApp");
        Environment.SetEnvironmentVariable("Api__BaseUrl", "https://api.example.com");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=compdb"
        );

        try
        {
            var configResult = await FluentConfig.Create().FromEnvironment().BuildAsync();

            // Chain multiple options together
            var appInfo = configResult
                .GetOption<string>("App:Name")
                .Map(name => $"Application: {name}")
                .Bind(name =>
                    configResult
                        .GetOption<string>("Api:BaseUrl")
                        .Map(url => $"{name} -> API: {url}")
                )
                .Bind(info =>
                    configResult
                        .GetOption<string>("Database:ConnectionString")
                        .Map(connStr =>
                            $"{info} -> DB: {connStr.Substring(0, Math.Min(20, connStr.Length))}..."
                        )
                );

            appInfo.Match(
                some: info => Console.WriteLine($"✅ Composed Info: {info}"),
                none: () => Console.WriteLine("❌ Missing required configuration")
            );

            // Alternative: Using Or for fallbacks
            var primaryUrl = configResult.GetOption<string>("Api:PrimaryUrl");
            var fallbackUrl = configResult.GetOption<string>("Api:FallbackUrl");
            var defaultUrl = Option<string>.Some("https://default.api.com");

            var finalUrl = primaryUrl.Or(fallbackUrl).Or(defaultUrl);

            finalUrl.Match(
                some: url => Console.WriteLine($"✅ Final URL: {url}"),
                none: () => Console.WriteLine("❌ No URL available")
            );
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Api__BaseUrl", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
    }

    /// <summary>
    /// Demonstrates Option-based configuration in dependency injection.
    /// </summary>
    public static async Task OptionBasedDependencyInjection()
    {
        Console.WriteLine("\n🏗️ Option-Based Dependency Injection");
        Console.WriteLine("====================================");

        Environment.SetEnvironmentVariable("App__Name", "DIOptionApp");
        Environment.SetEnvironmentVariable("Api__TimeoutSeconds", "45");

        try
        {
            var services = new ServiceCollection();

            // Add FluentAzure configuration
            services.AddFluentAzure<AppSettings>(builder =>
                builder.FromEnvironment().Required("App:Name").Optional("Api:TimeoutSeconds", "30")
            );

            var serviceProvider = services.BuildServiceProvider();
            var config = serviceProvider.GetRequiredService<AppSettings>();

            // Create a service that uses Option-based configuration
            var optionService = new OptionBasedService(config);

            // Use the service
            optionService.ProcessConfiguration();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Api__TimeoutSeconds", null);
        }
    }

    /// <summary>
    /// Demonstrates error handling with Option<T> vs traditional approaches.
    /// </summary>
    public static async Task ErrorHandlingComparison()
    {
        Console.WriteLine("\n⚠️ Error Handling Comparison");
        Console.WriteLine("=============================");

        Environment.SetEnvironmentVariable("Api__BaseUrl", "invalid-url");
        Environment.SetEnvironmentVariable(
            "Database__ConnectionString",
            "Server=localhost;Database=test"
        );

        try
        {
            var configResult = await FluentConfig.Create().FromEnvironment().BuildAsync();

            // Traditional approach (prone to null reference exceptions)
            Console.WriteLine("\n🔴 Traditional Approach:");
            try
            {
                var config = configResult.Match(
                    success => success,
                    errors =>
                        throw new InvalidOperationException(
                            $"Configuration failed: {string.Join(", ", errors)}"
                        )
                );

                var url = config.GetValueOrDefault("Api:BaseUrl", "");
                if (!string.IsNullOrEmpty(url))
                {
                    // This could throw if URL is invalid
                    var uri = new Uri(url);
                    Console.WriteLine($"✅ URL is valid: {uri}");
                }
                else
                {
                    Console.WriteLine("❌ URL is missing");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Exception: {ex.Message}");
            }

            // Option-based approach (type-safe and functional)
            Console.WriteLine("\n🟢 Option-Based Approach:");
            var urlOption = configResult
                .GetOption<string>("Api:BaseUrl")
                .Filter(url => !string.IsNullOrEmpty(url))
                .Bind(url =>
                {
                    try
                    {
                        var uri = new Uri(url);
                        return Option<Uri>.Some(uri);
                    }
                    catch
                    {
                        return Option<Uri>.None();
                    }
                });

            urlOption.Match(
                some: uri => Console.WriteLine($"✅ Valid URI: {uri}"),
                none: () => Console.WriteLine("❌ Invalid or missing URL")
            );
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("Api__BaseUrl", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
    }
}

/// <summary>
/// Example service that demonstrates Option-based configuration usage.
/// </summary>
public class OptionBasedService
{
    private readonly AppSettings _config;

    public OptionBasedService(AppSettings config)
    {
        _config = config;
    }

    public void ProcessConfiguration()
    {
        // Convert configuration to options for safe processing
        var appNameOption = Option<string>.FromNullable(_config.AppName);
        var timeoutOption = Option<int>.FromNullable(_config.Api.TimeoutSeconds);

        // Process with options
        var result = appNameOption
            .Map(name => $"Processing {name}")
            .Bind(name =>
                timeoutOption
                    .Map(timeout => $"{name} with {timeout}s timeout")
                    .Or(() => Option<string>.Some($"{name} with default timeout"))
            );

        result.Match(
            some: message => Console.WriteLine($"✅ {message}"),
            none: () => Console.WriteLine("❌ No configuration to process")
        );
    }
}
