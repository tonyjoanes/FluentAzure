using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAzure;
using FluentAzure.Binding;
using FluentAzure.Core;
using FluentAzure.Extensions;

namespace Demo;

/// <summary>
/// Simplified examples demonstrating the enhanced configuration binding system.
/// </summary>
public static class EnhancedBindingExamples
{
    /// <summary>
    /// Demonstrates basic binding with validation.
    /// </summary>
    public static async Task DemoBasicBinding()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 1: Basic Binding with Validation");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "myapp",
            ["Database:Username"] = "admin",
            ["Database:Password"] = "secret123",
            ["Api:BaseUrl"] = "https://api.example.com",
            ["Api:Timeout"] = "30",
            ["Api:RetryCount"] = "3",
            ["Logging:Level"] = "Information",
            ["Logging:EnableConsole"] = "true",
        };

        var result = (await FluentConfig.Create().FromInMemory(config).BuildAsync()).Bind(config =>
            ConfigurationBinder.Bind<AppConfiguration>(config)
        );

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Configuration bound successfully!");
                Console.WriteLine(
                    $"Database: {success.Database.Host}:{success.Database.Port}/{success.Database.Name}"
                );
                Console.WriteLine($"API: {success.Api.BaseUrl} (Timeout: {success.Api.Timeout}s)");
                Console.WriteLine(
                    $"Logging: {success.Logging.Level} (Console: {success.Logging.EnableConsole})"
                );
            },
            errors =>
            {
                Console.WriteLine("❌ Binding failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates binding with fallback handling.
    /// </summary>
    public static async Task DemoBindingWithFallback()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 2: Binding with Fallback");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Name"] = "My Application",
            ["Version"] = "1.0.0",
            ["Environment"] = "Production",
            ["MaxConnections"] = "100",
            ["EnableFeature"] = "true",
        };

        var result = (await FluentConfig.Create().FromInMemory(config).BuildAsync()).Bind(config =>
            ConfigurationBinder.Bind<AppConfiguration>(config)
        );

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Configuration bound successfully!");
                Console.WriteLine($"Database: {success.Database.Host}");
                Console.WriteLine($"API: {success.Api.BaseUrl}");
            },
            errors =>
            {
                Console.WriteLine("❌ Binding failed, using fallback:");
                var fallback = new AppConfiguration
                {
                    Database = new DatabaseConfig
                    {
                        Host = "default-host",
                        Port = 1433,
                        Name = "default-db",
                    },
                    Api = new ApiConfig { BaseUrl = "https://default-api.com", Timeout = 30 },
                    Logging = new LoggingConfig { Level = "Information", EnableConsole = true },
                };
                Console.WriteLine($"Database: {fallback.Database.Host}");
                Console.WriteLine($"API: {fallback.Api.BaseUrl}");
            }
        );
    }

    /// <summary>
    /// Demonstrates binding with validation.
    /// </summary>
    public static async Task DemoBindingWithValidation()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 3: Binding with Validation");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Email"] = "invalid-email",
            ["Age"] = "150",
            ["Url"] = "not-a-url",
            ["RequiredField"] = "", // Empty required field
        };

        var result = (await FluentConfig.Create().FromInMemory(config).BuildAsync()).Bind(config =>
            ConfigurationBinder.Bind<ValidatedConfig>(config)
        );

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Validation passed (unexpected)!");
            },
            errors =>
            {
                Console.WriteLine("❌ Validation failed (expected):");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates custom validation.
    /// </summary>
    public static async Task DemoCustomValidation()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 4: Custom Validation");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Username"] = "admin",
            ["Password"] = "weak",
            ["ConfirmPassword"] = "different",
        };

        var result = (await FluentConfig.Create().FromInMemory(config).BuildAsync())
            .Bind(config => ConfigurationBinder.Bind<LoginConfig>(config))
            .Bind(login =>
            {
                if (login.Password.Length < 8)
                {
                    return Result<LoginConfig>.Error("Password must be at least 8 characters long");
                }

                if (login.Password != login.ConfirmPassword)
                {
                    return Result<LoginConfig>.Error(
                        "Password and confirmation password do not match"
                    );
                }

                if (login.Username == login.Password)
                {
                    return Result<LoginConfig>.Error("Username and password cannot be the same");
                }

                return Result<LoginConfig>.Success(login);
            });

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Custom validation passed!");
            },
            errors =>
            {
                Console.WriteLine("❌ Custom validation failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates option-based binding.
    /// </summary>
    public static async Task DemoOptionBasedBinding()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 5: Option-based Binding");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "myapp",
        };

        var configOption = await FluentConfig.Create().FromInMemory(config).BuildOptionalAsync();

        // Use option-based binding
        var appConfig = configOption
            .Bind(config => config.BindOptional<AppConfiguration>())
            .GetValueOrDefault(
                new AppConfiguration
                {
                    Database = new DatabaseConfig
                    {
                        Host = "default",
                        Port = 1433,
                        Name = "default",
                    },
                    Api = new ApiConfig { BaseUrl = "https://default.com", Timeout = 30 },
                    Logging = new LoggingConfig { Level = "Information", EnableConsole = true },
                }
            );

        Console.WriteLine(
            $"Database: {appConfig.Database.Host}:{appConfig.Database.Port}/{appConfig.Database.Name}"
        );
        Console.WriteLine($"API: {appConfig.Api.BaseUrl}");
        Console.WriteLine($"Logging: {appConfig.Logging.Level}");
    }
}

// Example configuration classes

public class AppConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public ApiConfig Api { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class DatabaseConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ApiConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public int Timeout { get; set; }
    public int RetryCount { get; set; }
}

public class LoggingConfig
{
    public string Level { get; set; } = string.Empty;
    public bool EnableConsole { get; set; }
}

public class ValidatedConfig
{
    [Required]
    public string RequiredField { get; set; } = string.Empty;

    [Range(1, 120)]
    public int Age { get; set; }

    [Url]
    public string Url { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class LoginConfig
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
