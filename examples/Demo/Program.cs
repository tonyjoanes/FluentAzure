using System;
using System.Threading.Tasks;
using FluentAzure;

// Simple demonstration of the FluentAzure Configuration Pipeline Builder
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("FluentAzure Configuration Pipeline Builder Demo");
        Console.WriteLine("==============================================");

        // Example 1: Simple Configuration from Environment Variables
        Console.WriteLine("\n1. Simple Environment Configuration:");
        try
        {
            var config = await FluentAzure
                .FluentAzure.Configuration()
                .FromEnvironment()
                .Required("PATH") // PATH should exist on all systems
                .Optional("MY_APP_SETTING", "default-value")
                .BuildAsync();

            config.Match(
                success =>
                {
                    Console.WriteLine($"✅ Configuration loaded successfully!");
                    Console.WriteLine($"   PATH exists: {success.ContainsKey("PATH")}");
                    Console.WriteLine(
                        $"   MY_APP_SETTING: {success.GetValueOrDefault("MY_APP_SETTING", "not found")}"
                    );
                },
                errors =>
                {
                    Console.WriteLine($"❌ Configuration failed:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"   - {error}");
                    }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }

        // Example 2: Configuration with JSON File (Optional)
        Console.WriteLine("\n2. JSON File Configuration (Optional):");
        try
        {
            var jsonConfig = await FluentAzure
                .FluentAzure.Configuration()
                .FromJsonFile("appsettings.json", optional: true)
                .FromEnvironment()
                .Optional("ConnectionString", "DefaultConnectionString")
                .BuildAsync();

            jsonConfig.Match(
                success =>
                {
                    Console.WriteLine($"✅ JSON + Environment configuration loaded!");
                    Console.WriteLine($"   Keys found: {success.Count}");
                    Console.WriteLine(
                        $"   ConnectionString: {success.GetValueOrDefault("ConnectionString", "not found")}"
                    );
                },
                errors =>
                {
                    Console.WriteLine($"❌ Configuration failed:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"   - {error}");
                    }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }

        // Example 3: Configuration with Validation
        Console.WriteLine("\n3. Configuration with Validation:");
        try
        {
            var validatedConfig = await FluentAzure
                .FluentAzure.Configuration()
                .FromEnvironment()
                .Optional("PORT", "8080")
                .Validate(config =>
                {
                    if (
                        config.TryGetValue("PORT", out var portStr)
                        && int.TryParse(portStr, out var port)
                        && (port < 1 || port > 65535)
                    )
                    {
                        return "PORT must be between 1 and 65535";
                    }
                    return null; // Validation passed
                })
                .BuildAsync();

            validatedConfig.Match(
                success =>
                {
                    Console.WriteLine($"✅ Validated configuration loaded!");
                    Console.WriteLine($"   PORT: {success.GetValueOrDefault("PORT", "not found")}");
                },
                errors =>
                {
                    Console.WriteLine($"❌ Validation failed:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"   - {error}");
                    }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }

        // Example 4: Strongly-Typed Configuration Binding
        Console.WriteLine("\n4. Strongly-Typed Configuration:");
        try
        {
            var appConfig = await FluentAzure
                .FluentAzure.Configuration()
                .FromEnvironment()
                .Optional("AppName", "FluentAzure Demo")
                .Optional("Debug", "true")
                .Optional("MaxConnections", "100")
                .BuildAsync<AppSettings>();

            appConfig.Match(
                success =>
                {
                    Console.WriteLine($"✅ Strongly-typed configuration loaded!");
                    Console.WriteLine($"   AppName: {success.AppName}");
                    Console.WriteLine($"   Debug: {success.Debug}");
                    Console.WriteLine($"   MaxConnections: {success.MaxConnections}");
                },
                errors =>
                {
                    Console.WriteLine($"❌ Binding failed:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"   - {error}");
                    }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }

        Console.WriteLine("\n🎉 Demo completed!");
    }
}

// Simple configuration class for demonstration
public class AppSettings
{
    public string AppName { get; set; } = "";
    public bool Debug { get; set; }
    public int MaxConnections { get; set; }
}
