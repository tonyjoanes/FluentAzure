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

    [Fact]
    public Task MicrosoftProviders_WithHttpsEndpointsAndCredentials_AreNotReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            using System;
            using Azure.Core;
            using Microsoft.Extensions.Configuration;
            class C
            {
                void M(IConfigurationBuilder builder, TokenCredential credential, string fromConfig, Uri endpoint)
                {
                    builder
                        .AddAzureAppConfiguration(o => o.Connect(new Uri("https://x.azconfig.io"), credential))
                        .AddAzureAppConfiguration(fromConfig)
                        .AddAzureKeyVault(new Uri("https://my-vault.vault.azure.net/"), credential)
                        .AddAzureKeyVault(endpoint, credential);
                }
            }
            """);

    [Fact]
    public Task MicrosoftProviders_WithHttpEndpoints_AreReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            using System;
            using Azure.Core;
            using Microsoft.Extensions.Configuration;
            class C
            {
                void M(IConfigurationBuilder builder, TokenCredential credential)
                {
                    builder
                        .AddAzureAppConfiguration(o => o.Connect(new Uri({|FAZ0002:"http://x.azconfig.io"|}), credential))
                        .AddAzureAppConfiguration(new Uri({|FAZ0002:"http://y.azconfig.io"|}), credential)
                        .AddAzureKeyVault(new Uri({|FAZ0002:"http://my-vault.vault.azure.net/"|}), credential);
                }
            }
            """);

    [Fact]
    public Task MicrosoftProviders_WithHardcodedConnectionStringSecrets_AreReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            using Microsoft.Extensions.Configuration;
            class C
            {
                void M(IConfigurationBuilder builder)
                {
                    builder
                        .AddAzureAppConfiguration({|FAZ0003:"Endpoint=https://x.azconfig.io;Id=abc;Secret=c2VjcmV0"|})
                        .AddAzureAppConfiguration(o => o.Connect({|FAZ0003:"Endpoint=https://x.azconfig.io;Id=abc;Secret=c2VjcmV0"|}));
                }
            }
            """);

    [Fact]
    public Task AzureSdkClients_WithHttpEndpointsOrSecrets_AreReported() =>
        AnalyzerVerifier<AzureEndpointAnalyzer>.VerifyAsync("""
            using System;
            using Azure.Core;
            using Azure.Data.AppConfiguration;
            using Azure.Security.KeyVault.Secrets;
            class C
            {
                void M(TokenCredential credential)
                {
                    _ = new SecretClient(new Uri({|FAZ0002:"http://my-vault.vault.azure.net/"|}), credential);
                    _ = new SecretClient(new Uri("https://my-vault.vault.azure.net/"), credential);
                    _ = new ConfigurationClient({|FAZ0003:"Endpoint=https://x.azconfig.io;Id=abc;Secret=c2VjcmV0"|});
                    _ = new ConfigurationClient(new Uri({|FAZ0002:"http://x.azconfig.io"|}), credential);
                }
            }
            """);
}
