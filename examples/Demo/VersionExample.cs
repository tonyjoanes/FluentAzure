using FluentAzure;

namespace FluentAzure.Examples;

/// <summary>
/// Demonstrates how to access version information programmatically.
/// </summary>
public static class VersionExample
{
    /// <summary>
    /// Shows different ways to access version information.
    /// </summary>
    public static void ShowVersionInfo()
    {
        Console.WriteLine("🔢 FluentAzure Version Information");
        Console.WriteLine("==================================");

        // Method 1: Using FluentConfig.CurrentVersion (recommended)
        Console.WriteLine($"✅ Current Version: {FluentConfig.CurrentVersion}");

        // Method 2: Accessing individual version components
        Console.WriteLine($"📊 Version Components:");
        Console.WriteLine($"   Major: {Version.Major}");
        Console.WriteLine($"   Minor: {Version.Minor}");
        Console.WriteLine($"   Patch: {Version.Patch}");
        Console.WriteLine($"   PreRelease: {Version.PreRelease ?? "None"}");

        // Method 3: Using computed properties
        Console.WriteLine($"📦 Package Info:");
        Console.WriteLine($"   Full Version: {Version.Full}");
        Console.WriteLine($"   Package Version: {Version.Package}");
        Console.WriteLine($"   Assembly Version: {Version.Assembly}");
        Console.WriteLine($"   File Version: {Version.File}");

        // Method 4: Version status
        Console.WriteLine($"🔍 Version Status:");
        Console.WriteLine($"   Is PreRelease: {Version.IsPreRelease}");
        Console.WriteLine($"   Is Stable: {Version.IsStable}");

        // Method 5: System.Version object
        var sysVersion = Version.AsVersion;
        Console.WriteLine($"⚙️ System.Version: {sysVersion}");

        // Example: Conditional logic based on version
        if (Version.IsPreRelease)
        {
            Console.WriteLine("⚠️  This is a pre-release version - not recommended for production");
        }
        else
        {
            Console.WriteLine("✅ This is a stable release - safe for production");
        }

        // Example: Version comparison
        var majorVersion = Version.Major;
        if (majorVersion == 0)
        {
            Console.WriteLine("🚧 This is a 0.x version - API may change");
        }
        else if (majorVersion >= 1)
        {
            Console.WriteLine("🔒 This is a 1.x+ version - API is stable");
        }
    }

    /// <summary>
    /// Shows how to use version information in configuration.
    /// </summary>
    public static async Task ShowVersionInConfiguration()
    {
        Console.WriteLine("\n🔧 Version in Configuration Example");
        Console.WriteLine("==================================");

        // You can include version info in your configuration
        var configResult = await FluentConfig
            .Create()
            .FromEnvironment()
            .Optional("App:Version", FluentConfig.CurrentVersion)
            .Optional("App:IsPreRelease", Version.IsPreRelease.ToString())
            .BuildAsync();

        configResult.Match(
            success =>
            {
                Console.WriteLine(
                    $"✅ App Version from Config: {success.GetValueOrDefault("App:Version", "Unknown")}"
                );
                Console.WriteLine(
                    $"✅ Is PreRelease from Config: {success.GetValueOrDefault("App:IsPreRelease", "Unknown")}"
                );
            },
            errors =>
            {
                Console.WriteLine($"❌ Configuration failed: {string.Join(", ", errors)}");
            }
        );
    }
}
