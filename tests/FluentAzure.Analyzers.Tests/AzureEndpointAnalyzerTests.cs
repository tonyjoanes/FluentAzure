namespace FluentAzure.Analyzers.Tests;

public class AzureEndpointAnalyzerTests
{
    [Fact]
    public Task HttpsEndpoints_AreNotReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            using FluentAzure.Extensions;
            class C
            {
                void M(string fromConfig)
                {
                    FluentAzure.FluentConfig.Create()
                        .FromKeyVault("https://my-vault.vault.azure.net/")
                        .FromKeyVaultWithManagedIdentity("https://other.vault.azure.net/")
                        .FromKeyVault(fromConfig)
                        .FromAppConfiguration("https://my-config.azconfig.io");
                }
            }
            """);

    [Fact]
    public Task HttpOrMalformedEndpoints_AreReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            using FluentAzure.Extensions;
            class C
            {
                void M()
                {
                    FluentAzure.FluentConfig.Create()
                        .FromKeyVault({|FAZ0002:"http://my-vault.vault.azure.net/"|})
                        .FromKeyVaultWithManagedIdentity({|FAZ0002:"my-vault.vault.azure.net"|})
                        .FromAppConfiguration({|FAZ0002:"http://my-config.azconfig.io"|});
                    _ = new FluentAzure.Sources.KeyVaultSource({|FAZ0002:"http://vault"|});
                }
            }
            """);

    [Fact]
    public Task ConnectionStringsWithSecrets_AreReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            class C
            {
                void M(string fromConfig)
                {
                    FluentAzure.FluentConfig.Create()
                        .FromAppConfiguration({|FAZ0003:"Endpoint=https://x.azconfig.io;Id=abc;Secret=c2VjcmV0"|})
                        .FromAppConfiguration(fromConfig);
                    _ = new FluentAzure.Sources.AppConfigurationSource({|FAZ0003:"Endpoint=https://x.azconfig.io;Id=abc;Secret=c2VjcmV0"|});
                }
            }
            """);
}
