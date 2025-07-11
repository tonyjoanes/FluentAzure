using FluentAzure;
using FluentAzure.Extensions;

namespace FluentAzure.Examples;

/// <summary>
/// Demonstrates the TryConvert functionality for type-safe configuration access.
/// </summary>
public static class TypeConversionExample
{
    /// <summary>
    /// Shows how TryConvert works with different types.
    /// </summary>
    public static void DemonstrateTypeConversion()
    {
        Console.WriteLine("🔄 Type Conversion Examples");
        Console.WriteLine("===========================");

        // Test various type conversions
        TestConversion<int>("42", "int");
        TestConversion<int>("invalid", "int");
        TestConversion<bool>("true", "bool");
        TestConversion<bool>("false", "bool");
        TestConversion<bool>("invalid", "bool");
        TestConversion<double>("3.14", "double");
        TestConversion<double>("invalid", "double");
        TestConversion<DateTime>("2023-12-25", "DateTime");
        TestConversion<DateTime>("invalid", "DateTime");
        TestConversion<Uri>("https://example.com", "Uri");
        TestConversion<Uri>("invalid", "Uri");
        TestConversion<TimeSpan>("00:30:00", "TimeSpan");
        TestConversion<TimeSpan>("invalid", "TimeSpan");
        TestConversion<Guid>("12345678-1234-1234-1234-123456789012", "Guid");
        TestConversion<Guid>("invalid", "Guid");
    }

    private static void TestConversion<T>(string value, string typeName)
    {
        var result = TypeExtensions.TryConvert<T>(value);
        result.Match(
            success => Console.WriteLine($"✅ {typeName}: '{value}' -> {success}"),
            error => Console.WriteLine($"❌ {typeName}: '{value}' -> {error}")
        );
    }

    /// <summary>
    /// Shows how TryConvert integrates with configuration access.
    /// </summary>
    public static async Task ConfigurationTypeConversion()
    {
        Console.WriteLine("\n🔧 Configuration Type Conversion");
        Console.WriteLine("================================");

        // Set up environment variables
        Environment.SetEnvironmentVariable("App__Timeout", "30");
        Environment.SetEnvironmentVariable("App__Debug", "true");
        Environment.SetEnvironmentVariable("App__Version", "1.2.3");

        try
        {
            var configResult = await FluentConfig
                .Create()
                .FromEnvironment()
                .BuildAsync();

            configResult.Match(
                success =>
                {
                    // Use GetOption<T> which internally uses TryConvert
                    var timeoutOption = success.GetOption<int>("App:Timeout");
                    var debugOption = success.GetOption<bool>("App:Debug");
                    var versionOption = success.GetOption<string>("App:Version");
                    var missingOption = success.GetOption<int>("App:Missing");

                    timeoutOption.Match(
                        some: timeout => Console.WriteLine($"✅ Timeout: {timeout}s"),
                        none: () => Console.WriteLine("❌ Timeout not found or invalid")
                    );

                    debugOption.Match(
                        some: debug => Console.WriteLine($"✅ Debug: {debug}"),
                        none: () => Console.WriteLine("❌ Debug not found or invalid")
                    );

                    versionOption.Match(
                        some: version => Console.WriteLine($"✅ Version: {version}"),
                        none: () => Console.WriteLine("❌ Version not found")
                    );

                    missingOption.Match(
                        some: value => Console.WriteLine($"✅ Missing: {value}"),
                        none: () => Console.WriteLine("✅ Missing: None (as expected)")
                    );
                },
                errors => Console.WriteLine($"❌ Configuration failed: {string.Join(", ", errors)}")
            );
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("App__Timeout", null);
            Environment.SetEnvironmentVariable("App__Debug", null);
            Environment.SetEnvironmentVariable("App__Version", null);
        }
    }
}
