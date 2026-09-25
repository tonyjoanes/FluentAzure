using FluentAssertions;
using FluentAzure.Configuration;
using FluentAzure.Tests.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests.Configuration;

/// <summary>
/// Tests for <see cref="FluentAzureHealthCheck"/> and its registration.
/// </summary>
public class FluentAzureHealthCheckTests
{
    private const string SecretValue = "health-secret-value";

    [Fact]
    public async Task Healthy_WhenProviderLoadedSuccessfully()
    {
        // Arrange
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(new ControllableSource("S", new() { ["Api:Key"] = SecretValue, ["Name"] = "App" })))
            .Build();

        // Act
        var report = await RunAsync(configuration);

        // Assert
        var entry = report.Entries["fluentazure"];
        entry.Status.Should().Be(HealthStatus.Healthy);
        entry.Data["keys"].Should().Be(2);
        entry.Data.Should().ContainKey("lastSuccessfulLoad");
        AssertNoSecrets(entry);
    }

    [Fact]
    public async Task Degraded_WhenLastReloadFailed_AndErrorTextIsNotExposed()
    {
        // Arrange
        var source = new ControllableSource("S", new() { ["Name"] = "App" });
        var configuration = new MsConfigurationBuilder().AddFluentAzure(fluent => fluent.AddSource(source)).Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();
        source.FailWith = $"vault rejected {SecretValue}";
        await provider.ReloadAsync();

        // Act
        var report = await RunAsync(configuration);

        // Assert
        var entry = report.Entries["fluentazure"];
        entry.Status.Should().Be(HealthStatus.Degraded);
        entry.Data["lastReloadErrorCount"].Should().Be(1);
        configuration["Name"].Should().Be("App", "last good values are still served");
        AssertNoSecrets(entry);
    }

    [Fact]
    public async Task RecoversToHealthy_AfterASuccessfulReload()
    {
        // Arrange
        var source = new ControllableSource("S", new() { ["Name"] = "App" }) { };
        var configuration = new MsConfigurationBuilder().AddFluentAzure(fluent => fluent.AddSource(source)).Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();
        source.FailWith = "temporary";
        await provider.ReloadAsync();

        // Act
        source.FailWith = null;
        await provider.ReloadAsync();
        var report = await RunAsync(configuration);

        // Assert
        report.Entries["fluentazure"].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task FailureStatus_WhenValuesAreOlderThanThreeReloadIntervals()
    {
        // Arrange
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        using var configuration = (ConfigurationRoot)new MsConfigurationBuilder()
            .AddFluentAzure(
                fluent => fluent.AddSource(new ControllableSource("S", new() { ["Name"] = "App" })),
                source =>
                {
                    source.ReloadInterval = TimeSpan.FromHours(1);
                    source.TimeProvider = clock;
                })
            .Build();

        // Act
        clock.Advance(TimeSpan.FromHours(2));
        var fresh = await RunAsync(configuration);
        clock.Advance(TimeSpan.FromHours(2));
        var stale = await RunAsync(configuration, failureStatus: HealthStatus.Degraded);

        // Assert
        fresh.Entries["fluentazure"].Status.Should().Be(HealthStatus.Healthy);
        stale.Entries["fluentazure"].Status.Should().Be(HealthStatus.Degraded, "the configured failure status is used");
        stale.Entries["fluentazure"].Data.Should().ContainKey("staleFor");
    }

    [Fact]
    public async Task ExplicitStaleAfter_OverridesReloadIntervalDefault()
    {
        // Arrange
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(
                fluent => fluent.AddSource(new ControllableSource("S", new() { ["Name"] = "App" })),
                source => source.TimeProvider = clock)
            .Build();

        // Act
        clock.Advance(TimeSpan.FromMinutes(11));
        var report = await RunAsync(configuration, staleAfter: TimeSpan.FromMinutes(10));

        // Assert
        report.Entries["fluentazure"].Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task WithoutReloadInterval_ValuesAreNeverStale()
    {
        // Arrange
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(
                fluent => fluent.AddSource(new ControllableSource("S", new() { ["Name"] = "App" })),
                source => source.TimeProvider = clock)
            .Build();

        // Act
        clock.Advance(TimeSpan.FromDays(30));
        var report = await RunAsync(configuration);

        // Assert
        report.Entries["fluentazure"].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_WhenNoFluentAzureProviderIsRegistered()
    {
        // Arrange
        var configuration = new MsConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["A"] = "1" }).Build();

        // Act
        var report = await RunAsync(configuration);

        // Assert
        report.Entries["fluentazure"].Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task MultipleProviders_AreReportedSeparately()
    {
        // Arrange
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(new ControllableSource("A", new() { ["A"] = "1" })))
            .AddFluentAzure(fluent => fluent.AddSource(new ControllableSource("B", new() { ["B"] = "1", ["C"] = "2" })))
            .Build();

        // Act
        var report = await RunAsync(configuration);

        // Assert
        var entry = report.Entries["fluentazure"];
        entry.Data["provider0.keys"].Should().Be(1);
        entry.Data["provider1.keys"].Should().Be(2);
    }

    private static async Task<HealthReport> RunAsync(
        IConfiguration configuration,
        HealthStatus? failureStatus = null,
        TimeSpan? staleAfter = null
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddHealthChecks().AddFluentAzure(failureStatus: failureStatus, staleAfter: staleAfter);
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    }

    private static void AssertNoSecrets(HealthReportEntry entry)
    {
        entry.Description.Should().NotContain(SecretValue);
        entry.Data.Values.Select(v => v?.ToString()).Should().NotContain(v => v != null && v.Contains(SecretValue));
    }
}
