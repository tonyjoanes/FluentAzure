using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using FluentAzure.Configuration;
using FluentAzure.Core;
using FluentAzure.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MsConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace FluentAzure.Tests.Configuration;

/// <summary>
/// Tests for using FluentAzure pipelines as Microsoft.Extensions.Configuration providers.
/// </summary>
public class FluentAzureConfigurationProviderTests
{
    [Fact]
    public void AddFluentAzure_ExposesPipelineValuesThroughIConfiguration()
    {
        // Arrange
        var source = new MutableSource(new() { ["Database__Host"] = "db.local", ["Name"] = "App" });

        // Act
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(source).Required("Name"))
            .Build();

        // Assert
        configuration["Name"].Should().Be("App");
        configuration["name"].Should().Be("App");
        configuration["Database:Host"].Should().Be("db.local");
        configuration.GetSection("Database")["Host"].Should().Be("db.local");
    }

    [Fact]
    public void AddFluentAzure_ExplicitColonKey_WinsOverDoubleUnderscoreKey()
    {
        // Arrange
        var source = new MutableSource(new() { ["A__B"] = "derived", ["A:B"] = "explicit" });

        // Act
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(source))
            .Build();

        // Assert
        configuration["A:B"].Should().Be("explicit");
    }

    [Fact]
    public void AddFluentAzure_MissingRequiredKey_FailsFastOnBuild()
    {
        // Arrange
        var builder = new MsConfigurationBuilder().AddFluentAzure(fluent =>
            fluent.AddSource(new MutableSource(new())).Required("ConnectionString")
        );

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should()
            .Throw<FluentAzureConfigurationException>()
            .Which.Errors.Should()
            .Contain(e => e.Contains("ConnectionString"));
    }

    [Fact]
    public void AddFluentAzure_FailedValidation_FailsFastOnBuild()
    {
        // Arrange
        var builder = new MsConfigurationBuilder().AddFluentAzure(fluent =>
            fluent
                .AddSource(new MutableSource(new() { ["Url"] = "not-a-url" }))
                .Validate(c =>
                    Uri.IsWellFormedUriString(c["Url"], UriKind.Absolute) ? null : "Url must be absolute"
                )
        );

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should()
            .Throw<FluentAzureConfigurationException>()
            .Which.Errors.Should()
            .Contain("Url must be absolute");
    }

    [Fact]
    public async Task AddFluentAzureAsync_UsesPreloadedValuesWithoutLoadingAgain()
    {
        // Arrange
        var source = new MutableSource(new() { ["Name"] = "Preloaded" });
        var builder = new MsConfigurationBuilder();

        // Act
        await builder.AddFluentAzureAsync(fluent => fluent.AddSource(source).Required("Name"));
        var configuration = builder.Build();

        // Assert
        configuration["Name"].Should().Be("Preloaded");
        source.LoadCount.Should().Be(1);
    }

    [Fact]
    public async Task AddFluentAzureAsync_MissingRequiredKey_Throws()
    {
        // Arrange
        var builder = new MsConfigurationBuilder();

        // Act
        var act = () =>
            builder.AddFluentAzureAsync(fluent =>
                fluent.AddSource(new MutableSource(new())).Required("Name")
            );

        // Assert
        await act.Should().ThrowAsync<FluentAzureConfigurationException>();
    }

    [Fact]
    public async Task ReloadAsync_WithChangedValues_UpdatesConfigurationAndFiresChangeToken()
    {
        // Arrange
        var source = new MutableSource(new() { ["Secret"] = "v1" });
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(source))
            .Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();
        var changed = false;
        configuration.GetReloadToken().RegisterChangeCallback(_ => changed = true, null);

        // Act
        source.Values["Secret"] = "v2";
        var succeeded = await provider.ReloadAsync();

        // Assert
        succeeded.Should().BeTrue();
        configuration["Secret"].Should().Be("v2");
        changed.Should().BeTrue();
        source.ReloadCount.Should().Be(1);
    }

    [Fact]
    public async Task ReloadAsync_WithUnchangedValues_DoesNotFireChangeToken()
    {
        // Arrange
        var source = new MutableSource(new() { ["Secret"] = "v1" });
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(source))
            .Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();
        var changed = false;
        configuration.GetReloadToken().RegisterChangeCallback(_ => changed = true, null);

        // Act
        await provider.ReloadAsync();

        // Assert
        changed.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadAsync_WhenPipelineFails_KeepsLastGoodValuesAndReportsErrors()
    {
        // Arrange
        var source = new MutableSource(new() { ["Secret"] = "good" });
        IReadOnlyList<string>? reported = null;
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(
                fluent => fluent.AddSource(source),
                options => options.OnReloadError = errors => reported = errors
            )
            .Build();
        var provider = configuration.Providers.OfType<FluentAzureConfigurationProvider>().Single();

        // Act
        source.FailWith = "Key Vault unavailable";
        var succeeded = await provider.ReloadAsync();

        // Assert
        succeeded.Should().BeFalse();
        configuration["Secret"].Should().Be("good");
        provider.LastReloadErrors.Should().Contain("Key Vault unavailable");
        reported.Should().Contain("Key Vault unavailable");
    }

    [Fact]
    public async Task ReloadInterval_PushesChangesToOptionsMonitor()
    {
        // Arrange
        var source = new MutableSource(new() { ["App:Name"] = "before" });
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(source), TimeSpan.FromMilliseconds(50))
            .Build();

        var services = new ServiceCollection();
        services.AddFluentAzureOptions<AppOptions>(configuration, "App");
        using var serviceProvider = services.BuildServiceProvider();
        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<AppOptions>>();
        monitor.CurrentValue.Name.Should().Be("before");

        var updated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = monitor.OnChange(options => updated.TrySetResult(options.Name));

        // Act
        source.Values["App:Name"] = "after";
        var completed = await Task.WhenAny(updated.Task, Task.Delay(TimeSpan.FromSeconds(10)));

        // Assert
        completed.Should().Be(updated.Task, "the periodic reload should notify IOptionsMonitor");
        (await updated.Task).Should().Be("after");
        monitor.CurrentValue.Name.Should().Be("after");

        ((IDisposable)configuration).Dispose();
    }

    [Fact]
    public void AddFluentAzureOptions_BindsSectionFromFluentPipeline()
    {
        // Arrange
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent =>
                fluent.AddSource(new MutableSource(new() { ["App__Name"] = "Bound", ["App__Port"] = "8080" }))
            )
            .Build();
        var services = new ServiceCollection();

        // Act
        services.AddFluentAzureOptions<AppOptions>(configuration, "App");
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        // Assert
        options.Name.Should().Be("Bound");
        options.Port.Should().Be(8080);
    }

    [Fact]
    public void AddFluentAzureOptions_InvalidDataAnnotations_FailsStartupValidation()
    {
        // Arrange
        var configuration = new MsConfigurationBuilder()
            .AddFluentAzure(fluent => fluent.AddSource(new MutableSource(new() { ["App:Port"] = "80" })))
            .Build();
        var services = new ServiceCollection();
        services.AddFluentAzureOptions<AppOptions>(configuration, "App");
        using var serviceProvider = services.BuildServiceProvider();

        // Act: the same check the host runs at startup because of ValidateOnStart
        var act = () => serviceProvider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        act.Should()
            .Throw<OptionsValidationException>()
            .Which.Failures.Should()
            .Contain(f => f.Contains(nameof(AppOptions.Name)));
    }

    [Fact]
    public async Task Host_WithValidFluentConfiguration_StartsAndInjectsOptions()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        await builder.Configuration.AddFluentAzureAsync(fluent =>
            fluent.AddSource(new MutableSource(new() { ["App:Name"] = "Hosted" })).Required("App:Name")
        );
        builder.Services.AddFluentAzureOptions<AppOptions>(builder.Configuration, "App");

        // Act
        using var host = builder.Build();
        await host.StartAsync();

        // Assert
        host.Services.GetRequiredService<IOptions<AppOptions>>().Value.Name.Should().Be("Hosted");
        await host.StopAsync();
    }

    [Fact]
    public async Task Host_WithInvalidOptions_FailsToStart()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddFluentAzure(fluent => fluent.AddSource(new MutableSource(new())));
        builder.Services.AddFluentAzureOptions<AppOptions>(builder.Configuration, "App");
        using var host = builder.Build();

        // Act
        var act = () => host.StartAsync();

        // Assert
        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task EnvironmentSource_ReloadAsync_PicksUpChangedVariables()
    {
        // Arrange
        const string key = "FA_RELOADTEST_VALUE";
        Environment.SetEnvironmentVariable(key, "one");

        try
        {
            var source = new EnvironmentSource();
            Environment.SetEnvironmentVariable(key, "two");

            // Act
            var beforeReload = source.GetValue(key);
            var result = await source.ReloadAsync();

            // Assert
            beforeReload.Should().Be("one");
            result.Value[key].Should().Be("two");
            source.GetValue(key).Should().Be("two");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public class AppOptions
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public int Port { get; set; }
    }

    /// <summary>
    /// A reloadable source whose values and failure state can be changed by the test.
    /// </summary>
    private sealed class MutableSource : IReloadableConfigurationSource
    {
        public MutableSource(Dictionary<string, string> values) => Values = values;

        public Dictionary<string, string> Values { get; }

        public string? FailWith { get; set; }

        public int LoadCount { get; private set; }

        public int ReloadCount { get; private set; }

        public string Name => "Mutable";

        public int Priority => 100;

        public Task<Result<Dictionary<string, string>>> LoadAsync()
        {
            LoadCount++;
            return Task.FromResult(Snapshot());
        }

        public Task<Result<Dictionary<string, string>>> ReloadAsync()
        {
            ReloadCount++;
            return Task.FromResult(Snapshot());
        }

        public bool ContainsKey(string key) => Values.ContainsKey(key);

        public string? GetValue(string key) => Values.TryGetValue(key, out var v) ? v : null;

        private Result<Dictionary<string, string>> Snapshot() =>
            FailWith is null
                ? Result<Dictionary<string, string>>.Success(new Dictionary<string, string>(Values))
                : Result<Dictionary<string, string>>.Error(FailWith);
    }
}
