using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAzure.Binding;
using FluentAzure.Core;
using FluentAzure.Extensions;

namespace Demo;

/// <summary>
/// Comprehensive examples demonstrating the enhanced configuration binding system.
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

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).Bind<AppConfiguration>();

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
    /// Demonstrates record type binding.
    /// </summary>
    public static async Task DemoRecordBinding()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 2: Record Type Binding");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Name"] = "My Application",
            ["Version"] = "1.0.0",
            ["Environment"] = "Production",
            ["MaxConnections"] = "100",
            ["EnableFeature"] = "true",
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).BindRecord<AppSettings>();

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Record bound successfully!");
                Console.WriteLine($"App: {success.Name} v{success.Version}");
                Console.WriteLine($"Environment: {success.Environment}");
                Console.WriteLine($"Max Connections: {success.MaxConnections}");
                Console.WriteLine($"Feature Enabled: {success.EnableFeature}");
            },
            errors =>
            {
                Console.WriteLine("❌ Record binding failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates collection binding.
    /// </summary>
    public static async Task DemoCollectionBinding()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 3: Collection Binding");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Endpoints__0__Name"] = "Primary",
            ["Endpoints__0__Url"] = "https://primary.example.com",
            ["Endpoints__0__Timeout"] = "30",
            ["Endpoints__1__Name"] = "Secondary",
            ["Endpoints__1__Url"] = "https://secondary.example.com",
            ["Endpoints__1__Timeout"] = "60",
            ["Endpoints__2__Name"] = "Backup",
            ["Endpoints__2__Url"] = "https://backup.example.com",
            ["Endpoints__2__Timeout"] = "120",
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).BindList<Endpoint>(listKey: "Endpoints");

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Collection bound successfully!");
                Console.WriteLine($"Found {success.Count} endpoints:");
                foreach (var endpoint in success)
                {
                    Console.WriteLine(
                        $"  - {endpoint.Name}: {endpoint.Url} (Timeout: {endpoint.Timeout}s)"
                    );
                }
            },
            errors =>
            {
                Console.WriteLine("❌ Collection binding failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates dictionary binding.
    /// </summary>
    public static async Task DemoDictionaryBinding()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 4: Dictionary Binding");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Services__api__Url"] = "https://api.example.com",
            ["Services__api__Timeout"] = "30",
            ["Services__database__Url"] = "https://db.example.com",
            ["Services__database__Timeout"] = "60",
            ["Services__cache__Url"] = "https://cache.example.com",
            ["Services__cache__Timeout"] = "10",
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).BindDictionary<string, ServiceConfig>(dictionaryKey: "Services");

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Dictionary bound successfully!");
                Console.WriteLine($"Found {success.Count} services:");
                foreach (var service in success)
                {
                    Console.WriteLine(
                        $"  - {service.Key}: {service.Value.Url} (Timeout: {service.Value.Timeout}s)"
                    );
                }
            },
            errors =>
            {
                Console.WriteLine("❌ Dictionary binding failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates JSON binding with custom options.
    /// </summary>
    public static async Task DemoJsonBinding()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 5: JSON Binding with Custom Options");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["User:FirstName"] = "John",
            ["User:LastName"] = "Doe",
            ["User:Email"] = "john.doe@example.com",
            ["User:Age"] = "30",
            ["User:IsActive"] = "true",
            ["User:Preferences:Theme"] = "dark",
            ["User:Preferences:Language"] = "en-US",
            ["User:Preferences:Notifications:Email"] = "true",
            ["User:Preferences:Notifications:SMS"] = "false",
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).BindWithJsonOptions<UserProfile>(jsonOptions);

        result.Match(
            success =>
            {
                Console.WriteLine("✅ JSON binding successful!");
                Console.WriteLine($"User: {success.FirstName} {success.LastName}");
                Console.WriteLine($"Email: {success.Email}, Age: {success.Age}");
                Console.WriteLine($"Active: {success.IsActive}");
                Console.WriteLine($"Theme: {success.Preferences.Theme}");
                Console.WriteLine($"Language: {success.Preferences.Language}");
                Console.WriteLine(
                    $"Email Notifications: {success.Preferences.Notifications.Email}"
                );
                Console.WriteLine($"SMS Notifications: {success.Preferences.Notifications.SMS}");
            },
            errors =>
            {
                Console.WriteLine("❌ JSON binding failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        );
    }

    /// <summary>
    /// Demonstrates validation errors.
    /// </summary>
    public static async Task DemoValidationErrors()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 6: Validation Errors");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Email"] = "invalid-email",
            ["Age"] = "150",
            ["Url"] = "not-a-url",
            ["RequiredField"] = "", // Empty required field
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).Bind<ValidatedConfig>();

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
        Console.WriteLine("\n📋 Enhanced Binding Example 7: Custom Validation");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Username"] = "admin",
            ["Password"] = "weak",
            ["ConfirmPassword"] = "different",
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).BindWithValidation<LoginConfig>(login =>
        {
            if (login.Password.Length < 8)
            {
                return Result<string>.Error("Password must be at least 8 characters long");
            }

            if (login.Password != login.ConfirmPassword)
            {
                return Result<string>.Error("Password and confirmation password do not match");
            }

            if (login.Username == login.Password)
            {
                return Result<string>.Error("Username and password cannot be the same");
            }

            return Result<string>.Success("Validation passed");
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
    /// Demonstrates binding without validation.
    /// </summary>
    public static async Task DemoBindingWithoutValidation()
    {
        Console.WriteLine("\n📋 Enhanced Binding Example 8: Binding Without Validation");
        Console.WriteLine(new string('-', 50));

        var config = new Dictionary<string, string>
        {
            ["Name"] = "Test App",
            ["Version"] = "invalid-version",
            ["MaxConnections"] = "not-a-number",
        };

        var result = (
            await FluentAzure.Core.FluentAzure.Configuration().FromInMemory(config).BuildAsync()
        ).BindWithoutValidation<AppSettings>();

        result.Match(
            success =>
            {
                Console.WriteLine("✅ Binding without validation successful!");
                Console.WriteLine($"Name: {success.Name}");
                Console.WriteLine($"Version: {success.Version}");
                Console.WriteLine($"Max Connections: {success.MaxConnections}");
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

// Record type example
public record AppSettings(
    string Name,
    string Version,
    string Environment,
    int MaxConnections,
    bool EnableFeature
);

// Collection example
public class Endpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Timeout { get; set; }
}

// Dictionary example
public class ServiceConfig
{
    public string Url { get; set; } = string.Empty;
    public int Timeout { get; set; }
}

// Complex nested object example
public class UserProfile
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}

public class UserPreferences
{
    public string Theme { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public NotificationSettings Notifications { get; set; } = new();
}

public class NotificationSettings
{
    public bool Email { get; set; }
    public bool SMS { get; set; }
}

// Validation example
public class ValidatedConfig
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(1, 120)]
    public int Age { get; set; }

    [Url]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string RequiredField { get; set; } = string.Empty;
}

// Custom validation example
public class LoginConfig
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
