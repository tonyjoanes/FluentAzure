using FluentAzure.Extensions;
using FluentAzure.Core;
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
        services.AddFluentAzure<TestConfig>(
            config => config
                .FromEnvironment()
                .Required("Name")
                .Required("Value")
                .Required("Enabled")
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
            config => config
                .FromEnvironment()
                .Required("Name")
                .Required("Value")
                .Required("Enabled"),
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
            services.AddFluentAzure<TestConfig>(
                config => config
                    .FromEnvironment()
                    .Required("Name")
                    .Required("Value")
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
            ServiceCollectionExtensions.AddFluentAzure<TestConfig>(
                services!,
                config => config
            )
        );
    }

    [Fact]
    public void AddFluentAzure_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        Func<ConfigurationBuilder, ConfigurationBuilder>? configure = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddFluentAzure<TestConfig>(
                configure!
            )
        );
    }

    [Fact]
    public void AddFluentAzure_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        Func<TestConfig, TestConfig>? factory = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddFluentAzure<TestConfig>(
                config => config,
                factory!
            )
        );
    }
}
