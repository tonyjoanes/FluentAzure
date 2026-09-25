using Azure.Data.AppConfiguration;
using FluentAssertions;
using FluentAzure.Configuration;
using FluentAzure.Sources;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests.Integration;

/// <summary>
/// Runs a fact only when an Azure App Configuration emulator is available, via the
/// FLUENTAZURE_APPCONFIG_EMULATOR connection string, e.g. <c>Endpoint=http://localhost:8483;Id=emulator;Secret=c2VjcmV0</c>.
/// The SDK signs connection-string requests with HMAC, so start the emulator with a matching access key:
/// <c>docker run -p 8483:8483 -e Tenant:HmacSha256Enabled=true -e Tenant:AccessKeys:0:Id=emulator -e Tenant:AccessKeys:0:Secret=c2VjcmV0 mcr.microsoft.com/azure-app-configuration/app-configuration-emulator:1.2.0</c>.
/// </summary>
public sealed class EmulatorFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "FLUENTAZURE_APPCONFIG_EMULATOR";

    public EmulatorFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Set {ConnectionStringVariable} to run App Configuration emulator tests.";
        }
    }
}

/// <summary>
/// End-to-end tests of <see cref="AppConfigurationSource"/> against the Azure App Configuration emulator
/// (real HTTP, real service semantics). Each test uses a unique key prefix so tests can run in parallel.
/// </summary>
[Trait("Category", "Emulator")]
public class AppConfigurationEmulatorTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable(EmulatorFactAttribute.ConnectionStringVariable)!;

    [EmulatorFact]
    public async Task LoadsSettings_WithLabelLayeringAndPrefixTrimming()
    {
        // Arrange
        var (client, prefix) = await CreateClientAsync();
        await client.SetConfigurationSettingAsync($"{prefix}Name", "default");
        await client.SetConfigurationSettingAsync($"{prefix}Timeout", "30");
        await client.SetConfigurationSettingAsync(new ConfigurationSetting($"{prefix}Name", "production", "Production"));

        var options = new AppConfigurationOptions { KeyFilter = $"{prefix}*" };
        options.Labels.Add("Production");
        options.TrimKeyPrefixes.Add(prefix);

        // Act
        var result = await new AppConfigurationSource(ConnectionString, options).LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? string.Join("; ", result.Errors) : string.Empty);
        result.Value.Should().BeEquivalentTo(new Dictionary<string, string> { ["Name"] = "production", ["Timeout"] = "30" });
    }

    [EmulatorFact]
    public async Task FlattensJsonContentTypeSettings()
    {
        // Arrange
        var (client, prefix) = await CreateClientAsync();
        await client.SetConfigurationSettingAsync(
            new ConfigurationSetting($"{prefix}Cors", """{"Origins":["https://a","https://b"]}""") { ContentType = "application/json" }
        );

        var options = new AppConfigurationOptions { KeyFilter = $"{prefix}*" };
        options.TrimKeyPrefixes.Add(prefix);

        // Act
        var result = await new AppConfigurationSource(ConnectionString, options).LoadAsync();

        // Assert
        result.Value["Cors:Origins:1"].Should().Be("https://b");
    }

    [EmulatorFact]
    public async Task LoadsFeatureFlags()
    {
        // Arrange
        var (client, prefix) = await CreateClientAsync();
        var flagId = $"{prefix.TrimEnd(':')}-Beta";
        await client.SetConfigurationSettingAsync(new FeatureFlagConfigurationSetting(flagId, isEnabled: true));

        var options = new AppConfigurationOptions { KeyFilter = $"{prefix}*", IncludeFeatureFlags = true };

        // Act
        var result = await new AppConfigurationSource(ConnectionString, options).LoadAsync();

        // Assert
        result.Value[$"FeatureManagement:{flagId}"].Should().Be("true");
    }

    [EmulatorFact]
    public async Task SentinelRefresh_ReloadsOnlyWhenTheSentinelChanges()
    {
        // Arrange
        var (client, prefix) = await CreateClientAsync();
        await client.SetConfigurationSettingAsync($"{prefix}Sentinel", "1");
        await client.SetConfigurationSettingAsync($"{prefix}Name", "v1");

        var options = new AppConfigurationOptions { KeyFilter = $"{prefix}*", SentinelKey = $"{prefix}Sentinel" };
        options.TrimKeyPrefixes.Add(prefix);

        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(new AppConfigurationSource(ConnectionString, options)).Required("Name"))
            .Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();

        // Act
        await client.SetConfigurationSettingAsync($"{prefix}Name", "v2");
        await provider.ReloadAsync();
        var beforeSentinel = configuration["Name"];

        await client.SetConfigurationSettingAsync($"{prefix}Sentinel", "2");
        await provider.ReloadAsync();

        // Assert
        beforeSentinel.Should().Be("v1", "the sentinel had not changed");
        configuration["Name"].Should().Be("v2");
    }

    [EmulatorFact]
    public async Task MissingRequiredKey_FailsFastThroughTheProvider()
    {
        // Arrange
        var (_, prefix) = await CreateClientAsync();
        var options = new AppConfigurationOptions { KeyFilter = $"{prefix}*" };

        // Act
        var act = () => new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(new AppConfigurationSource(ConnectionString, options)).Required("Missing"))
            .Build();

        // Assert
        act.Should().Throw<FluentAzureConfigurationException>();
    }

    private static Task<(ConfigurationClient Client, string Prefix)> CreateClientAsync() =>
        Task.FromResult((new ConfigurationClient(ConnectionString), $"t{Guid.NewGuid():N}:"));
}
