using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;
using Azure.Data.AppConfiguration;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using FluentAzure.Sources;
using FluentAzure.Tests.TestDoubles;
using Microsoft.Extensions.Configuration;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests.Sources;

/// <summary>
/// Tests for <see cref="AppConfigurationSource"/> using in-memory fakes of the Azure clients.
/// </summary>
public class AppConfigurationSourceTests
{
    [Fact]
    public async Task LoadAsync_WithNoLabelDefault_LoadsUnlabelledSettings()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("App:Name", "Demo");
        client.Add("App:Name", "Prod", label: "Production");

        var source = new AppConfigurationSource(client);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("App:Name", "Demo"));
    }

    [Fact]
    public async Task LoadAsync_WithMultipleLabels_LaterLabelOverridesEarlier()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("App:Name", "Default");
        client.Add("App:Timeout", "30");
        client.Add("App:Name", "Production", label: "Production");

        var options = new AppConfigurationOptions();
        options.Labels.Add("Production");
        var source = new AppConfigurationSource(client, options);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value["App:Name"].Should().Be("Production");
        result.Value["App:Timeout"].Should().Be("30");
    }

    [Fact]
    public async Task LoadAsync_WithKeyFilterAndTrimPrefix_ReturnsTrimmedMatchingKeys()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("MyApp:Database:Host", "db");
        client.Add("OtherApp:Database:Host", "other");

        var options = new AppConfigurationOptions { KeyFilter = "MyApp:*" };
        options.TrimKeyPrefixes.Add("MyApp:");
        var source = new AppConfigurationSource(client, options);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value.Should().ContainSingle();
        result.Value["Database:Host"].Should().Be("db");
    }

    [Fact]
    public async Task LoadAsync_WithSnapshot_LoadsOnlySnapshotSettings()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("App:Version", "live");
        client.Snapshots["release-42"] = new List<ConfigurationSetting>
        {
            ConfigurationModelFactory.ConfigurationSetting("App:Version", "42", null, null, new ETag("s1"), null, null),
        };

        var source = new AppConfigurationSource(client, new AppConfigurationOptions { SnapshotName = "release-42" });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value["App:Version"].Should().Be("42");
    }

    [Fact]
    public async Task LoadAsync_WithJsonContentType_FlattensIntoHierarchicalKeys()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("App:Cors", """{"Origins":["https://a","https://b"],"Enabled":true}""", contentType: "application/json; charset=utf-8");
        client.Add("App:Raw", "{not json", contentType: "application/json");

        var source = new AppConfigurationSource(client);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value["App:Cors:Origins:0"].Should().Be("https://a");
        result.Value["App:Cors:Origins:1"].Should().Be("https://b");
        result.Value["App:Cors:Enabled"].Should().Be("true");
        result.Value["App:Raw"].Should().Be("{not json");
    }

    [Fact]
    public async Task LoadAsync_WithKeyVaultReference_ResolvesSecretValue()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddSecretReference("Database:Password", new Uri("https://vault1.vault.azure.net/secrets/db-password"));
        var vault = new FakeSecretClient { Secrets = { ["db-password"] = "s3cr3t" } };

        var options = new AppConfigurationOptions { SecretClientFactory = _ => vault };
        var source = new AppConfigurationSource(client, options);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value["Database:Password"].Should().Be("s3cr3t");
    }

    [Fact]
    public async Task LoadAsync_WithVersionedKeyVaultReference_RequestsThatVersion()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddSecretReference("Api:Key", new Uri("https://vault1.vault.azure.net/secrets/api-key/abc123"));
        var vault = new FakeSecretClient { Secrets = { ["api-key/abc123"] = "pinned" } };

        var source = new AppConfigurationSource(client, new AppConfigurationOptions { SecretClientFactory = _ => vault });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value["Api:Key"].Should().Be("pinned");
    }

    [Fact]
    public async Task LoadAsync_WithUnresolvableKeyVaultReference_FailsWithoutLeakingValues()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddSecretReference("Database:Password", new Uri("https://vault1.vault.azure.net/secrets/missing"));
        var vault = new FakeSecretClient();

        var source = new AppConfigurationSource(client, new AppConfigurationOptions { SecretClientFactory = _ => vault });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Database:Password").And.Contain("missing");
    }

    [Fact]
    public async Task IsSensitive_IsTrueOnlyForValuesResolvedFromKeyVaultReferences()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddSecretReference("Database:Password", new Uri("https://vault1.vault.azure.net/secrets/db-password"));
        client.Add("App:Name", "Demo");
        var vault = new FakeSecretClient { Secrets = { ["db-password"] = "s3cr3t" } };
        var source = new AppConfigurationSource(client, new AppConfigurationOptions { SecretClientFactory = _ => vault });

        // Act
        await source.LoadAsync();

        // Assert
        source.IsSensitive("Database:Password").Should().BeTrue();
        source.IsSensitive("App:Name").Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WithReferenceResolutionDisabled_SkipsReferences()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddSecretReference("Database:Password", new Uri("https://vault1.vault.azure.net/secrets/db-password"));
        client.Add("App:Name", "Demo");

        var source = new AppConfigurationSource(client, new AppConfigurationOptions { ResolveKeyVaultReferences = false });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Keys.Should().BeEquivalentTo("App:Name");
    }

    [Fact]
    public async Task LoadAsync_FeatureFlags_AreExcludedByDefault()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddFeatureFlag("Beta", enabled: true);

        var source = new AppConfigurationSource(client);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithFeatureFlags_MapsToFeatureManagementSchema()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.AddFeatureFlag("Beta", enabled: true);
        client.AddFeatureFlag("Legacy", enabled: false);
        var rollout = client.AddFeatureFlag("NewCheckout", enabled: true);
        rollout.ClientFilters.Add(new FeatureFlagFilter("Microsoft.Percentage", new Dictionary<string, object> { ["Value"] = 25 }));

        var source = new AppConfigurationSource(client, new AppConfigurationOptions { IncludeFeatureFlags = true });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value["FeatureManagement:Beta"].Should().Be("true");
        result.Value["FeatureManagement:Legacy"].Should().Be("false");
        result.Value["FeatureManagement:NewCheckout:EnabledFor:0:Name"].Should().Be("Microsoft.Percentage");
        result.Value["FeatureManagement:NewCheckout:EnabledFor:0:Parameters:Value"].Should().Be("25");
        result.Value.Should().NotContainKey("FeatureManagement:NewCheckout");
    }

    [Fact]
    public async Task LoadAsync_WithNestedFeatureFilterParameters_FlattensWithoutReflection()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        var flag = client.AddFeatureFlag("Targeted", enabled: true);
        using var audience = System.Text.Json.JsonDocument.Parse("""{"Users":["alice","bob"],"DefaultRolloutPercentage":10}""");
        flag.ClientFilters.Add(
            new FeatureFlagFilter(
                "Microsoft.Targeting",
                new Dictionary<string, object>
                {
                    ["Audience"] = audience.RootElement.Clone(),
                    ["Options"] = new Dictionary<string, object?> { ["IgnoreCase"] = true, ["Ratio"] = 0.5 },
                }
            )
        );

        var source = new AppConfigurationSource(client, new AppConfigurationOptions { IncludeFeatureFlags = true });

        // Act
        var result = await source.LoadAsync();

        // Assert
        const string parameters = "FeatureManagement:Targeted:EnabledFor:0:Parameters";
        result.Value[$"{parameters}:Audience:Users:1"].Should().Be("bob");
        result.Value[$"{parameters}:Audience:DefaultRolloutPercentage"].Should().Be("10");
        result.Value[$"{parameters}:Options:IgnoreCase"].Should().Be("true");
        result.Value[$"{parameters}:Options:Ratio"].Should().Be("0.5");
    }

    [Fact]
    public async Task ReloadAsync_WithUnchangedSentinel_DoesNotReloadSettings()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("Sentinel", "1");
        client.Add("App:Name", "v1");
        var source = new AppConfigurationSource(client, new AppConfigurationOptions { SentinelKey = "Sentinel" });
        await source.LoadAsync();
        var listCallsAfterLoad = client.ListCalls;

        // Act
        client.Add("App:Name", "v2");
        var result = await source.ReloadAsync();

        // Assert
        result.Value["App:Name"].Should().Be("v1");
        client.ListCalls.Should().Be(listCallsAfterLoad);
    }

    [Fact]
    public async Task ReloadAsync_WithChangedSentinel_ReloadsSettings()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("Sentinel", "1");
        client.Add("App:Name", "v1");
        var source = new AppConfigurationSource(client, new AppConfigurationOptions { SentinelKey = "Sentinel" });
        await source.LoadAsync();

        // Act
        client.Add("App:Name", "v2");
        client.Add("Sentinel", "2");
        var result = await source.ReloadAsync();

        // Assert
        result.Value["App:Name"].Should().Be("v2");
    }

    [Fact]
    public async Task ReloadAsync_WithoutSentinel_AlwaysReloads()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("App:Name", "v1");
        var source = new AppConfigurationSource(client);
        await source.LoadAsync();

        // Act
        client.Add("App:Name", "v2");
        var cached = await source.LoadAsync();
        var reloaded = await source.ReloadAsync();

        // Assert
        cached.Value["App:Name"].Should().Be("v1");
        reloaded.Value["App:Name"].Should().Be("v2");
    }

    [Fact]
    public async Task LoadAsync_WhenServiceFails_ReturnsError()
    {
        // Arrange
        var client = new FakeConfigurationClient { FailWith = new RequestFailedException(403, "Forbidden") };
        var source = new AppConfigurationSource(client);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Forbidden");
    }

    [Fact]
    public async Task Pipeline_WithAppConfiguration_FlowsIntoIConfigurationAndReloads()
    {
        // Arrange
        var client = new FakeConfigurationClient();
        client.Add("Sentinel", "1");
        client.Add("App:Name", "v1");
        var appConfig = new AppConfigurationSource(client, new AppConfigurationOptions { SentinelKey = "Sentinel" });

        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(appConfig).Required("App:Name"))
            .Build();
        var provider = configuration.Providers.OfType<FluentAzure.Configuration.FluentAzureConfigurationProvider>().Single();

        // Act
        client.Add("App:Name", "v2");
        client.Add("Sentinel", "2");
        await provider.ReloadAsync();

        // Assert
        configuration["App:Name"].Should().Be("v2");
    }

    /// <summary>
    /// In-memory App Configuration client. Adding a setting with an existing key and label replaces it
    /// and assigns a new ETag, as the service does.
    /// </summary>
    private sealed class FakeConfigurationClient : ConfigurationClient
    {
        private readonly List<ConfigurationSetting> _settings = new();
        private int _version;

        public Dictionary<string, List<ConfigurationSetting>> Snapshots { get; } = new();

        public Exception? FailWith { get; set; }

        public int ListCalls { get; private set; }

        public void Add(string key, string value, string? label = null, string? contentType = null) =>
            Upsert(ConfigurationModelFactory.ConfigurationSetting(key, value, label, contentType, NextETag(), null, null));

        public void AddSecretReference(string key, Uri secretId, string? label = null) =>
            Upsert(ConfigurationModelFactory.SecretReferenceConfigurationSetting(key, secretId, label, NextETag(), null, null));

        public FeatureFlagConfigurationSetting AddFeatureFlag(string featureId, bool enabled, string? label = null)
        {
            var flag = ConfigurationModelFactory.FeatureFlagConfigurationSetting(featureId, enabled, label, NextETag(), null, null);
            Upsert(flag);
            return flag;
        }

        public override AsyncPageable<ConfigurationSetting> GetConfigurationSettingsAsync(
            SettingSelector selector,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfFailing();
            ListCalls++;
            var matches = _settings
                .Where(s => MatchesKey(s.Key, selector.KeyFilter) && MatchesLabel(s.Label, selector.LabelFilter))
                .ToList();
            return AsyncPageable<ConfigurationSetting>.FromPages(new[] { Page<ConfigurationSetting>.FromValues(matches, null, new FakeResponse(200)) });
        }

        public override AsyncPageable<ConfigurationSetting> GetConfigurationSettingsForSnapshotAsync(
            string snapshotName,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfFailing();
            return AsyncPageable<ConfigurationSetting>.FromPages(
                new[] { Page<ConfigurationSetting>.FromValues(Snapshots[snapshotName], null, new FakeResponse(200)) }
            );
        }

        public override Task<Response<ConfigurationSetting>> GetConfigurationSettingAsync(
            string key,
            string? label = null,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfFailing();
            var setting = _settings.FirstOrDefault(s => s.Key == key && s.Label == label)
                ?? throw new RequestFailedException(404, "Setting not found");
            return Task.FromResult(Response.FromValue(setting, new FakeResponse(200)));
        }

        private void Upsert(ConfigurationSetting setting)
        {
            _settings.RemoveAll(s => s.Key == setting.Key && s.Label == setting.Label);
            _settings.Add(setting);
        }

        private ETag NextETag() => new($"etag-{++_version}");

        private void ThrowIfFailing()
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }
        }

        private static bool MatchesKey(string key, string filter) =>
            filter == "*"
            || (filter.EndsWith('*') ? key.StartsWith(filter[..^1], StringComparison.Ordinal) : key == filter);

        private static bool MatchesLabel(string? label, string filter) =>
            filter == AppConfigurationOptions.NoLabel ? label is null : label == filter;
    }

    /// <summary>
    /// In-memory Key Vault client. Secrets are keyed by name, or "name/version" for pinned versions.
    /// </summary>
    private sealed class FakeSecretClient : SecretClient
    {
        public Dictionary<string, string> Secrets { get; } = new();

        public override Task<Response<KeyVaultSecret>> GetSecretAsync(
            string name,
            string? version = null,
            CancellationToken cancellationToken = default
        )
        {
            var lookup = version is null ? name : $"{name}/{version}";
            if (!Secrets.TryGetValue(lookup, out var value))
            {
                throw new RequestFailedException(404, $"Secret '{lookup}' not found");
            }

            return Task.FromResult(Response.FromValue(SecretModelFactory.KeyVaultSecret(SecretModelFactory.SecretProperties(name: name), value), new FakeResponse(200)));
        }
    }
}
