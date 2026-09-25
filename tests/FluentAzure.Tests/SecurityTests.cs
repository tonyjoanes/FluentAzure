using Azure.Core;
using Azure.Identity;
using FluentAssertions;
using FluentAzure.Binding;
using FluentAzure.Core;
using FluentAzure.Extensions;
using FluentAzure.Sources;
using FluentAzure.Tests.Sources;
using Microsoft.Extensions.Configuration;
using FluentPipeline = FluentAzure.Core.ConfigurationBuilder;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests;

/// <summary>
/// Tests for pipeline identity configuration and secret redaction.
/// </summary>
public class SecurityTests
{
    private const string VaultUrl = "https://test-vault.vault.azure.net/";
    private const string SecretValue = "hunter2-super-secret";

    public class Identity
    {
        [Fact]
        public void Credential_ByDefault_ResolvesToDefaultAzureCredential()
        {
            // Arrange
            var builder = new FluentPipeline();

            // Act
            var resolved = ((PipelineCredential)builder.Credential).Resolve();

            // Assert
            resolved.Should().BeOfType<DefaultAzureCredential>();
        }

        [Fact]
        public void UseManagedIdentity_CalledAfterFromKeyVault_StillAppliesToKeyVault()
        {
            // Arrange
            var builder = new FluentPipeline().FromKeyVault(VaultUrl);

            // Act
            builder.UseManagedIdentity("11111111-1111-1111-1111-111111111111");

            // Assert
            var keyVault = builder.Sources.OfType<KeyVaultSource>().Single();
            keyVault.Configuration.Credential.Should().BeSameAs(builder.Credential);
            ((PipelineCredential)builder.Credential).Resolve().Should().BeOfType<ManagedIdentityCredential>();
        }

        [Fact]
        public void UseWorkloadIdentity_ResolvesToWorkloadIdentityCredential()
        {
            // Arrange & Act
            var builder = new FluentPipeline().UseWorkloadIdentity();

            // Assert
            ((PipelineCredential)builder.Credential).Resolve().Should().BeOfType<WorkloadIdentityCredential>();
        }

        [Fact]
        public void AzureSources_ShareTheSamePipelineCredential()
        {
            // Arrange & Act
            var builder = new FluentPipeline()
                .FromKeyVault(VaultUrl)
                .FromKeyVault(VaultUrl, configure: _ => { })
                .FromAppConfiguration("https://test.azconfig.io");

            // Assert
            builder.Sources.OfType<KeyVaultSource>().Should().AllSatisfy(s =>
                s.Configuration.Credential.Should().BeSameAs(builder.Credential)
            );
            builder.Sources.OfType<AppConfigurationSource>().Single().Options.Credential
                .Should().BeSameAs(builder.Credential);
        }

        [Fact]
        public void PerSourceCredential_TakesPrecedenceOverPipelineCredential()
        {
            // Arrange
            var keyVaultCredential = new FakeCredential("kv");
            var appConfigCredential = new FakeCredential("ac");

            // Act
            var builder = new FluentPipeline()
                .UseManagedIdentity()
                .FromKeyVault(VaultUrl, configure: c => c.Credential = keyVaultCredential)
                .FromAppConfiguration("https://test.azconfig.io", o => o.Credential = appConfigCredential);

            // Assert
            builder.Sources.OfType<KeyVaultSource>().Single().Configuration.Credential.Should().BeSameAs(keyVaultCredential);
            builder.Sources.OfType<AppConfigurationSource>().Single().Options.Credential.Should().BeSameAs(appConfigCredential);
        }

        [Fact]
        public async Task PipelineCredential_DelegatesTokenRequestsToConfiguredCredential()
        {
            // Arrange
            var builder = new FluentPipeline().UseCredential(new FakeCredential("token-123"));

            // Act
            var token = await builder.Credential.GetTokenAsync(new TokenRequestContext(new[] { "scope" }), default);

            // Assert
            token.Token.Should().Be("token-123");
        }

        [Fact]
        public async Task UseCredential_AfterCredentialWasUsed_Throws()
        {
            // Arrange
            var builder = new FluentPipeline().UseCredential(new FakeCredential("first"));
            await builder.Credential.GetTokenAsync(new TokenRequestContext(new[] { "scope" }), default);

            // Act
            var act = () => builder.UseCredential(new FakeCredential("second"));

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }

    public class Redaction
    {
        [Fact]
        public async Task BuildAsyncOfT_WhenSecretFailsToConvert_ErrorDoesNotContainValue()
        {
            // Arrange
            var builder = new FluentPipeline()
                .AddSource(new InMemorySource(new Dictionary<string, string> { ["Port"] = SecretValue }));

            // Act
            var result = await builder.BuildAsync<PortSettings>();

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("Port"));
            result.Errors.Should().NotContain(e => e.Contains(SecretValue));
        }

        [Fact]
        public void EnhancedBinder_WhenSecretFailsToConvert_ErrorDoesNotContainValue()
        {
            // Arrange
            var config = new Dictionary<string, string> { ["Port"] = SecretValue };

            // Act
            var result = EnhancedConfigurationBinder.Bind<PortSettings>(config);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().NotContain(e => e.Contains(SecretValue));
        }

        [Fact]
        public void GetRequired_WhenSecretFailsToConvert_ErrorDoesNotContainValue()
        {
            // Arrange
            var config = new Dictionary<string, string> { ["Port"] = SecretValue };

            // Act
            var result = config.GetRequired<int>("Port");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle().Which.Should().Contain("Port").And.NotContain(SecretValue);
        }

        [Fact]
        public async Task IsSensitive_TracksKeysWhoseWinningValueCameFromASensitiveSource()
        {
            // Arrange
            var builder = new FluentPipeline()
                .AddSource(new TestSensitiveSource(new() { ["Db:Password"] = SecretValue, ["Shared"] = "from-vault" }, priority: 200))
                .AddSource(new InMemorySource(new Dictionary<string, string> { ["Shared"] = "override", ["App:Name"] = "Demo" }, priority: 300));

            // Act
            await builder.BuildAsync();

            // Assert
            builder.IsSensitive("Db:Password").Should().BeTrue();
            builder.IsSensitive("db:password").Should().BeTrue();
            builder.IsSensitive("Shared").Should().BeFalse("a non-sensitive source supplied the winning value");
            builder.IsSensitive("App:Name").Should().BeFalse();
        }

        [Fact]
        public void Sensitive_MarksAdditionalKeys()
        {
            // Arrange & Act
            var builder = new FluentPipeline().Sensitive("Jwt:SigningKey");

            // Assert
            builder.IsSensitive("Jwt:SigningKey").Should().BeTrue();
            builder.IsSensitive("Jwt:Issuer").Should().BeFalse();
        }

        [Fact]
        public void KeyVaultSource_TreatsEveryValueAsSensitive()
        {
            // Arrange
            var source = new MockKeyVaultSource(VaultUrl, new Dictionary<string, string>());

            // Act & Assert
            source.IsSensitive("anything").Should().BeTrue();
        }

        [Fact]
        public void GetRedactedDebugView_MasksSecretsAndKeepsOtherValues()
        {
            // Arrange
            var configuration = new MsConfigurationBuilder()
                .AddFluentAzure(fluent => fluent
                    .AddSource(new TestSensitiveSource(new() { ["Db__Password"] = SecretValue }, priority: 200))
                    .AddSource(new InMemorySource(new Dictionary<string, string> { ["App:Name"] = "Demo", ["Api:Key"] = "env-secret" }))
                    .Sensitive("Api:Key"))
                .Build();

            // Act
            var view = configuration.GetRedactedDebugView();
            var unredacted = configuration.GetDebugView();

            // Assert
            unredacted.Should().Contain(SecretValue, "the standard debug view is what leaks secrets");
            view.Should().NotContain(SecretValue);
            view.Should().NotContain("env-secret");
            view.Should().Contain("Demo");
            view.Should().Contain("***");
            configuration["Db:Password"].Should().Be(SecretValue, "redaction only affects the debug view");
        }
    }

    public class PortSettings
    {
        public int Port { get; set; }
    }

    private sealed class TestSensitiveSource : ISensitiveConfigurationSource
    {
        private readonly Dictionary<string, string> _values;

        public TestSensitiveSource(Dictionary<string, string> values, int priority)
        {
            _values = values;
            Priority = priority;
        }

        public string Name => "TestSensitive";

        public int Priority { get; }

        public Task<Result<Dictionary<string, string>>> LoadAsync() =>
            Task.FromResult(Result<Dictionary<string, string>>.Success(new Dictionary<string, string>(_values)));

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public string? GetValue(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public bool IsSensitive(string key) => true;
    }

    private sealed class FakeCredential : TokenCredential
    {
        private readonly string _token;

        public FakeCredential(string token) => _token = token;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(_token, DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
