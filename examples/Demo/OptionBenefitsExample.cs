using FluentAzure;
using FluentAzure.Core;

namespace FluentAzure.Examples;

/// <summary>
/// Demonstrates the key benefits of Option-based configuration handling
/// with practical, real-world examples.
/// </summary>
public static class OptionBenefitsExample
{
    public static async Task ShowBenefits()
    {
        Console.WriteLine("🎯 Option-Based Configuration Benefits");
        Console.WriteLine("=====================================");

        // Set up test data
        Environment.SetEnvironmentVariable("Api__BaseUrl", "https://api.example.com");
        Environment.SetEnvironmentVariable("Api__TimeoutSeconds", "invalid-number");
        Environment.SetEnvironmentVariable("Database__ConnectionString", "");

        try
        {
            var configResult = await FluentConfig.Create().FromEnvironment().BuildAsync();

            Console.WriteLine("\n1️⃣ **Null Safety & No Exceptions**");
            Console.WriteLine("=================================");
            await DemonstrateNullSafety(configResult);

            Console.WriteLine("\n2️⃣ **Graceful Fallbacks**");
            Console.WriteLine("=========================");
            await DemonstrateFallbacks(configResult);

            Console.WriteLine("\n3️⃣ **Type-Safe Validation**");
            Console.WriteLine("===========================");
            await DemonstrateTypeSafeValidation(configResult);

            Console.WriteLine("\n4️⃣ **Functional Composition**");
            Console.WriteLine("=============================");
            await DemonstrateFunctionalComposition(configResult);

            Console.WriteLine("\n5️⃣ **Clean Error Handling**");
            Console.WriteLine("===========================");
            await DemonstrateCleanErrorHandling(configResult);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("Api__BaseUrl", null);
            Environment.SetEnvironmentVariable("Api__TimeoutSeconds", null);
            Environment.SetEnvironmentVariable("Database__ConnectionString", null);
        }
    }

    /// <summary>
    /// Shows how Options prevent null reference exceptions
    /// </summary>
    private static async Task DemonstrateNullSafety(Result<Dictionary<string, string>> configResult)
    {
        configResult.Match(
            success =>
            {
                // ❌ Traditional approach - can throw exceptions
                Console.WriteLine("🔴 Traditional (risky):");
                try
                {
                    success.TryGetValue("Api:BaseUrl", out var url);
                    var uri = new Uri(url ?? ""); // Could throw if URL is invalid
                    Console.WriteLine($"   URL: {uri}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   💥 Exception: {ex.Message}");
                }

                // ✅ Option approach - never throws
                Console.WriteLine("🟢 Option-based (safe):");
                success.TryGetValue("Api:BaseUrl", out var urlValue);
                var urlOption = Option<string>.FromNullable(urlValue);
                urlOption.Match(
                    some: url => Console.WriteLine($"   URL: {url}"),
                    none: () => Console.WriteLine("   No URL configured")
                );
            },
            errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
        );
    }

    /// <summary>
    /// Shows how Options provide elegant fallback chains
    /// </summary>
    private static async Task DemonstrateFallbacks(Result<Dictionary<string, string>> configResult)
    {
        configResult.Match(
            success =>
            {
                // ❌ Traditional approach - verbose and error-prone
                Console.WriteLine("🔴 Traditional (verbose):");
                success.TryGetValue("Api:PrimaryUrl", out var primaryUrl);
                primaryUrl ??= "";
                success.TryGetValue("Api:FallbackUrl", out var fallbackUrl);
                fallbackUrl ??= "";
                var defaultUrl = "https://default.api.com";

                string finalUrl;
                if (!string.IsNullOrEmpty(primaryUrl))
                {
                    finalUrl = primaryUrl;
                }
                else if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    finalUrl = fallbackUrl;
                }
                else
                {
                    finalUrl = defaultUrl;
                }
                Console.WriteLine($"   Final URL: {finalUrl}");

                // ✅ Option approach - clean and functional
                Console.WriteLine("🟢 Option-based (clean):");
                var finalUrlOption = Option<string>.FromNullable(primaryUrl)
                    .Or(() => Option<string>.FromNullable(fallbackUrl))
                    .Or(() => Option<string>.Some("https://default.api.com"));

                finalUrlOption.Match(
                    some: url => Console.WriteLine($"   Final URL: {url}"),
                    none: () => Console.WriteLine("   No URL available")
                );
            },
            errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
        );
    }

    /// <summary>
    /// Shows how Options provide type-safe validation
    /// </summary>
    private static async Task DemonstrateTypeSafeValidation(Result<Dictionary<string, string>> configResult)
    {
        configResult.Match(
            success =>
            {
                success.TryGetValue("Api:TimeoutSeconds", out var timeoutStr);
                timeoutStr ??= "";

                // ❌ Traditional approach - manual validation everywhere
                Console.WriteLine("🔴 Traditional (manual validation):");
                if (int.TryParse(timeoutStr, out var timeout) && timeout > 0 && timeout <= 300)
                {
                    Console.WriteLine($"   Valid timeout: {timeout}s");
                }
                else
                {
                    Console.WriteLine("   Invalid timeout - using default");
                    timeout = 30;
                }

                // ✅ Option approach - validation built into the type
                Console.WriteLine("🟢 Option-based (type-safe validation):");
                var timeoutOption = int.TryParse(timeoutStr, out var t) && t > 0 && t <= 300
                    ? Option<int>.Some(t)
                    : Option<int>.None();

                timeoutOption.Match(
                    some: t => Console.WriteLine($"   Valid timeout: {t}s"),
                    none: () => Console.WriteLine("   Invalid timeout - using default (30s)")
                );
            },
            errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
        );
    }

    /// <summary>
    /// Shows how Options enable functional composition
    /// </summary>
    private static async Task DemonstrateFunctionalComposition(Result<Dictionary<string, string>> configResult)
    {
        configResult.Match(
            success =>
            {
                success.TryGetValue("App:Name", out var appName);
                appName ??= "MyApp";
                success.TryGetValue("Api:BaseUrl", out var apiUrl);
                apiUrl ??= "";
                success.TryGetValue("Api:TimeoutSeconds", out var timeoutStr);
                timeoutStr ??= "30";

                // ❌ Traditional approach - nested if statements
                Console.WriteLine("🔴 Traditional (nested logic):");
                string result;
                if (!string.IsNullOrEmpty(appName))
                {
                    if (!string.IsNullOrEmpty(apiUrl))
                    {
                        if (int.TryParse(timeoutStr, out var timeout) && timeout > 0)
                        {
                            result = $"App: {appName} -> API: {apiUrl} (timeout: {timeout}s)";
                        }
                        else
                        {
                            result = $"App: {appName} -> API: {apiUrl} (default timeout)";
                        }
                    }
                    else
                    {
                        result = $"App: {appName} -> No API configured";
                    }
                }
                else
                {
                    result = "No app name configured";
                }
                Console.WriteLine($"   Result: {result}");

                // ✅ Option approach - functional composition
                Console.WriteLine("🟢 Option-based (functional composition):");
                var composedResult = Option<string>.FromNullable(appName)
                    .Map(name => $"App: {name}")
                    .Bind(name =>
                        Option<string>.FromNullable(apiUrl)
                            .Map(url => $"{name} -> API: {url}")
                            .Or(() => Option<string>.Some($"{name} -> No API configured"))
                    )
                    .Bind(info =>
                        int.TryParse(timeoutStr, out var t) && t > 0
                            ? Option<string>.Some($"{info} (timeout: {t}s)")
                            : Option<string>.Some($"{info} (default timeout)")
                    );

                composedResult.Match(
                    some: r => Console.WriteLine($"   Result: {r}"),
                    none: () => Console.WriteLine("   No configuration available")
                );
            },
            errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
        );
    }

    /// <summary>
    /// Shows how Options provide clean error handling
    /// </summary>
    private static async Task DemonstrateCleanErrorHandling(Result<Dictionary<string, string>> configResult)
    {
        configResult.Match(
            success =>
            {
                success.TryGetValue("Database:ConnectionString", out var connectionString);
                connectionString ??= "";

                // ❌ Traditional approach - try-catch everywhere
                Console.WriteLine("🔴 Traditional (try-catch everywhere):");
                try
                {
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new InvalidOperationException("Connection string is required");
                    }

                    // Simulate database connection attempt
                    if (connectionString.Contains("invalid"))
                    {
                        throw new Exception("Invalid connection string format");
                    }

                    Console.WriteLine("   Database connection successful");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   💥 Error: {ex.Message}");
                }

                // ✅ Option approach - clean error handling
                Console.WriteLine("🟢 Option-based (clean error handling):");
                var dbConnectionOption = Option<string>.FromNullable(connectionString)
                    .Filter(cs => !string.IsNullOrEmpty(cs))
                    .Filter(cs => !cs.Contains("invalid"))
                    .Map(cs => "Database connection successful");

                dbConnectionOption.Match(
                    some: result => Console.WriteLine($"   {result}"),
                    none: () => Console.WriteLine("   Database connection failed - using fallback")
                );
            },
            errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
        );
    }
}
