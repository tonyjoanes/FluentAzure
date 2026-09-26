using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace FluentAzure.Analyzers.Tests;

/// <summary>
/// Runs an analyzer over a source snippet compiled against .NET 10 and the real FluentAzure assemblies.
/// Expected diagnostics are marked in the source with {|FAZ0001:...|} spans.
/// </summary>
internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static Task VerifyAsync(string source)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,

            // Must match the FluentAzure build referenced by this project (net10.0)
            ReferenceAssemblies = new ReferenceAssemblies(
                "net10.0",
                new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"),
                Path.Combine("ref", "net10.0")),
        };

        foreach (var assembly in new[]
        {
            typeof(FluentConfig).Assembly,
            typeof(Microsoft.Extensions.Configuration.ConfigurationRootExtensions).Assembly,
            typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly,
            typeof(Microsoft.Extensions.Configuration.ConfigurationBuilder).Assembly,
            typeof(Microsoft.Extensions.Logging.ILogger).Assembly,
            typeof(Azure.Core.TokenCredential).Assembly,
            typeof(Azure.Data.AppConfiguration.ConfigurationClient).Assembly,
            typeof(Azure.Security.KeyVault.Secrets.SecretClient).Assembly,
        })
        {
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        return test.RunAsync();
    }
}
