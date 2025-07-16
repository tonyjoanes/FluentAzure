using FluentAzure;
using FluentAzure.Core;

namespace FluentAzure.Examples;

/// <summary>
/// Simple examples showing why Option-based configuration is valuable
/// </summary>
public static class SimpleOptionBenefits
{
    public static async Task ShowWhyOptionsMatter()
    {
        Console.WriteLine("🎯 Why Option-Based Configuration Matters");
        Console.WriteLine("========================================");

        // Set up test scenario
        Environment.SetEnvironmentVariable("Api__Url", "invalid-url");
        Environment.SetEnvironmentVariable("Api__Timeout", "not-a-number");

        try
        {
            var configResult = await FluentConfig.Create().FromEnvironment().BuildAsync();

            Console.WriteLine("\n1️⃣ **No More Null Reference Exceptions**");
            Console.WriteLine("=======================================");

            configResult.Match(
                success =>
                {
                    // ❌ Traditional way - can crash
                    Console.WriteLine("🔴 Traditional (risky):");
                    var url = success.GetValueOrDefault("Api:Url", "");
                    try
                    {
                        var uri = new Uri(url); // 💥 Crashes with "invalid-url"
                        Console.WriteLine($"   URL: {uri}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   💥 Exception: {ex.Message}");
                    }

                    // ✅ Option way - never crashes
                    Console.WriteLine("🟢 Option-based (safe):");
                    var urlOption = Option<string>.FromNullable(url)
                        .Filter(u => Uri.IsWellFormedUriString(u, UriKind.Absolute));

                    urlOption.Match(
                        some: u => Console.WriteLine($"   Valid URL: {u}"),
                        none: () => Console.WriteLine("   Invalid URL - using default")
                    );
                },
                errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
            );

            Console.WriteLine("\n2️⃣ **Elegant Fallback Chains**");
            Console.WriteLine("=============================");

            configResult.Match(
                success =>
                {
                    // ❌ Traditional way - verbose
                    Console.WriteLine("🔴 Traditional (verbose):");
                    var primary = success.GetValueOrDefault("Api:Primary", "");
                    var backup = success.GetValueOrDefault("Api:Backup", "");
                    var defaultUrl = "https://default.api.com";

                    string final;
                    if (!string.IsNullOrEmpty(primary)) final = primary;
                    else if (!string.IsNullOrEmpty(backup)) final = backup;
                    else final = defaultUrl;
                    Console.WriteLine($"   Using: {final}");

                    // ✅ Option way - clean
                    Console.WriteLine("🟢 Option-based (clean):");
                    var finalOption = Option<string>.FromNullable(primary)
                        .Or(() => Option<string>.FromNullable(backup))
                        .Or(() => Option<string>.Some(defaultUrl));

                    finalOption.Match(
                        some: f => Console.WriteLine($"   Using: {f}"),
                        none: () => Console.WriteLine("   No URL available")
                    );
                },
                errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
            );

            Console.WriteLine("\n3️⃣ **Type-Safe Validation**");
            Console.WriteLine("===========================");

            configResult.Match(
                success =>
                {
                    var timeoutStr = success.GetValueOrDefault("Api:Timeout", "not-a-number");

                    // ❌ Traditional way - manual validation
                    Console.WriteLine("🔴 Traditional (manual):");
                    int timeout;
                    if (int.TryParse(timeoutStr, out var t) && t > 0 && t <= 300)
                    {
                        timeout = t;
                        Console.WriteLine($"   Timeout: {timeout}s");
                    }
                    else
                    {
                        timeout = 30; // default
                        Console.WriteLine($"   Invalid timeout, using default: {timeout}s");
                    }

                    // ✅ Option way - validation built-in
                    Console.WriteLine("🟢 Option-based (built-in validation):");
                    var timeoutOption = int.TryParse(timeoutStr, out var parsed) && parsed > 0 && parsed <= 300
                        ? Option<int>.Some(parsed)
                        : Option<int>.None();

                    timeoutOption.Match(
                        some: t => Console.WriteLine($"   Timeout: {t}s"),
                        none: () => Console.WriteLine("   Invalid timeout, using default: 30s")
                    );
                },
                errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
            );

            Console.WriteLine("\n4️⃣ **Functional Composition**");
            Console.WriteLine("=============================");

            configResult.Match(
                success =>
                {
                    var appName = success.GetValueOrDefault("App:Name", "MyApp");
                    var apiUrl = success.GetValueOrDefault("Api:Url", "");

                    // ❌ Traditional way - nested if statements
                    Console.WriteLine("🔴 Traditional (nested):");
                    string result;
                    if (!string.IsNullOrEmpty(appName))
                    {
                        if (!string.IsNullOrEmpty(apiUrl) && Uri.IsWellFormedUriString(apiUrl, UriKind.Absolute))
                        {
                            result = $"App: {appName} -> API: {apiUrl}";
                        }
                        else
                        {
                            result = $"App: {appName} -> No valid API";
                        }
                    }
                    else
                    {
                        result = "No app name";
                    }
                    Console.WriteLine($"   Result: {result}");

                    // ✅ Option way - functional composition
                    Console.WriteLine("🟢 Option-based (functional):");
                    var composedResult = Option<string>.FromNullable(appName)
                        .Map(name => $"App: {name}")
                        .Bind(name =>
                            Option<string>.FromNullable(apiUrl)
                                .Filter(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                                .Map(url => $"{name} -> API: {url}")
                                .Or(() => Option<string>.Some($"{name} -> No valid API"))
                        );

                    composedResult.Match(
                        some: r => Console.WriteLine($"   Result: {r}"),
                        none: () => Console.WriteLine("   No configuration")
                    );
                },
                errors => Console.WriteLine($"❌ Config failed: {string.Join(", ", errors)}")
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable("Api__Url", null);
            Environment.SetEnvironmentVariable("Api__Timeout", null);
        }
    }
}
