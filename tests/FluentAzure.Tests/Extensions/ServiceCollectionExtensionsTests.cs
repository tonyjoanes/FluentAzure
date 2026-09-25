using FluentAzure.Core;
using FluentAzure.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FluentAzure.Tests.Extensions;

public class TestConfig
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool Enabled { get; set; }
}

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFluentAzure_BasicUsage_RegistersConfigurationAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        Environment.SetEnvironmentVariable("Name", "TestApp");
        Environment.SetEnvironmentVariable("Value", "42");
        Environment.SetEnvironmentVariable("Enabled", "true");

        // Act
        services.AddFluentAzure<TestConfig>(config =>
            config.FromEnvironment().Required("Name").Required("Value").Required("Enabled")
        );

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var config1 = serviceProvider.GetRequiredService<TestConfig>();
        var config2 = serviceProvider.GetRequiredService<TestConfig>();
        Assert.Equal("TestApp", config1.Name);
        Assert.Equal(42, config1.Value);
        Assert.True(config1.Enabled);
        Assert.Same(config1, config2); // Singleton
    }

    [Fact]
    public void AddFluentAzure_WithFactory_AppliesFactoryTransformation()
    {
        // Arrange
        var services = new ServiceCollection();
        Environment.SetEnvironmentVariable("Name", "TestApp");
        Environment.SetEnvironmentVariable("Value", "42");
        Environment.SetEnvironmentVariable("Enabled", "true");

        // Act
        services.AddFluentAzure<TestConfig>(
            config =>
                config.FromEnvironment().Required("Name").Required("Value").Required("Enabled"),
            settings =>
            {
                settings.Name = $"Modified_{settings.Name}";
                return settings;
            }
        );

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var config1 = serviceProvider.GetRequiredService<TestConfig>();
        var config2 = serviceProvider.GetRequiredService<TestConfig>();
        Assert.Equal("Modified_TestApp", config1.Name);
        Assert.Equal(42, config1.Value);
        Assert.True(config1.Enabled);
        Assert.Same(config1, config2); // Singleton
    }

    [Fact]
    public void AddFluentAzure_ConfigurationFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        Environment.SetEnvironmentVariable("Name", ""); // Required but empty
        Environment.SetEnvironmentVariable("Value", ""); // Required but empty

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddFluentAzure<TestConfig>(config =>
                config.FromEnvironment().Required("Name").Required("Value")
            )
        );
    }

    [Fact]
    public void AddFluentAzure_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddFluentAzure<TestConfig>(services!, config => config)
        );
    }

    [Fact]
    public void AddFluentAzure_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        Func<ConfigurationBuilder, ConfigurationBuilder>? configure = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddFluentAzure<TestConfig>(configure!));
    }

    [Fact]
    public void AddFluentAzure_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        Func<TestConfig, TestConfig>? factory = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddFluentAzure<TestConfig>(config => config, factory!)
        );
    }

    [Fact]
    public async Task AddFluentAzureAsync_RegistersConfigurationAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var values = new Dictionary<string, string> { ["Name"] = "AsyncApp", ["Value"] = "7" };

        // Act
        await services.AddFluentAzureAsync<TestConfig>(config =>
            config.AddSource(new YieldingSource(values)).Required("Name")
        );

        // Assert
        var settings = services.BuildServiceProvider().GetRequiredService<TestConfig>();
        Assert.Equal("AsyncApp", settings.Name);
        Assert.Equal(7, settings.Value);
    }

    [Fact]
    public async Task AddFluentAzureAsync_WithMissingRequiredKey_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.AddFluentAzureAsync<TestConfig>(config =>
                config.AddSource(new YieldingSource(new())).Required("Name")
            )
        );
    }

    [Fact]
    public void AddFluentAzure_UnderSingleThreadedSynchronizationContext_DoesNotDeadlock()
    {
        // Arrange: emulate a UI/legacy ASP.NET-style context where blocking on async code
        // that resumes on the captured context would deadlock
        var values = new Dictionary<string, string> { ["Name"] = "NoDeadlock" };
        TestConfig? settings = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockingSynchronizationContext());
            try
            {
                var services = new ServiceCollection();
                services.AddFluentAzure<TestConfig>(config =>
                    config.AddSource(new YieldingSource(values)).Required("Name")
                );
                settings = services.BuildServiceProvider().GetRequiredService<TestConfig>();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        // Act
        thread.Start();
        var completed = thread.Join(TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(completed, "AddFluentAzure deadlocked under a single-threaded synchronization context");
        Assert.Null(failure);
        Assert.Equal("NoDeadlock", settings!.Name);
    }

    /// <summary>
    /// A source that completes asynchronously, forcing continuations through the synchronization context.
    /// </summary>
    private sealed class YieldingSource : IConfigurationSource
    {
        private readonly Dictionary<string, string> _values;

        public YieldingSource(Dictionary<string, string> values) => _values = values;

        public string Name => "Yielding";

        public int Priority => 100;

        public async Task<Result<Dictionary<string, string>>> LoadAsync()
        {
            await Task.Yield();
            return Result<Dictionary<string, string>>.Success(_values);
        }

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public string? GetValue(string key) => _values.TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>
    /// A synchronization context whose posted callbacks only run if its owning thread pumps them,
    /// which it never does while blocked - so any continuation posted here never runs.
    /// </summary>
    private sealed class BlockingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Intentionally dropped: the owning thread is blocked and never pumps messages
        }
    }
}
