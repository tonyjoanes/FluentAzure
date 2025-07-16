using System;
using System.IO;
using System.Threading.Tasks;
using FluentAzure;
using FluentAzure.Core;
using FluentAzure.Extensions;
using System.Collections.Generic; // Added for Dictionary

namespace Demo;

/// <summary>
/// Demonstrates hot reload functionality with file watching.
/// </summary>
public static class HotReloadExample
{
    /// <summary>
    /// Runs the hot reload example.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("🔥 Hot Reload Example");
        Console.WriteLine("=====================");

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
            // Build configuration with hot reload enabled
            var configResult = await FluentConfig
                .Create()
                .FromJsonFileWithHotReload(tempFile, debounceMs: 200)
                .OnConfigurationChanged((oldConfig, newConfig) =>
                {
                    Console.WriteLine("🔄 Configuration changed!");
                    Console.WriteLine($"  Old API URL: {oldConfig.GetValueOrDefault("Api__BaseUrl", "not set")}");
                    Console.WriteLine($"  New API URL: {newConfig.GetValueOrDefault("Api__BaseUrl", "not set")}");
                    Console.WriteLine($"  Old Timeout: {oldConfig.GetValueOrDefault("Api__TimeoutSeconds", "not set")}");
                    Console.WriteLine($"  New Timeout: {newConfig.GetValueOrDefault("Api__TimeoutSeconds", "not set")}");
                    Console.WriteLine();
                })
                .BuildAsync();

            configResult.Match(
                config =>
                {
                    Console.WriteLine("✅ Initial configuration loaded:");
                    Console.WriteLine($"  API Base URL: {config.GetValueOrDefault("Api__BaseUrl", "not set")}");
                    Console.WriteLine($"  API Timeout: {config.GetValueOrDefault("Api__TimeoutSeconds", "not set")}s");
                    Console.WriteLine($"  Max Connections: {config.GetValueOrDefault("Database__MaxConnections", "not set")}");
                    Console.WriteLine();
                    return config;
                },
                errors =>
                {
                    Console.WriteLine("❌ Configuration failed to load:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    return new Dictionary<string, string>();
                }
            );

            Console.WriteLine("👀 Watching for file changes...");
            Console.WriteLine("   Edit the file to see hot reload in action!");
            Console.WriteLine("   Press any key to exit.");
            Console.WriteLine();

            // Simulate file changes
            await SimulateFileChanges(tempFile);

            Console.ReadKey();
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static async Task SimulateFileChanges(string filePath)
    {
        // Change 1: Update API URL
        await Task.Delay(1000);
        await File.WriteAllTextAsync(
            filePath,
            """
            {
                "Api": {
                    "BaseUrl": "https://api.updated.com",
                    "TimeoutSeconds": 30
                },
                "Database": {
                    "MaxConnections": 100
                }
            }
            """
        );

        // Change 2: Update timeout
        await Task.Delay(1000);
        await File.WriteAllTextAsync(
            filePath,
            """
            {
                "Api": {
                    "BaseUrl": "https://api.updated.com",
                    "TimeoutSeconds": 60
                },
                "Database": {
                    "MaxConnections": 100
                }
            }
            """
        );

        // Change 3: Update multiple values
        await Task.Delay(1000);
        await File.WriteAllTextAsync(
            filePath,
            """
            {
                "Api": {
                    "BaseUrl": "https://api.final.com",
                    "TimeoutSeconds": 45
                },
                "Database": {
                    "MaxConnections": 200
                }
            }
            """
        );
    }
}
