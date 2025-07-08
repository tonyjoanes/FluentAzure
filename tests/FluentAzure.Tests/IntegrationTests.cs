using FluentAssertions;
using FluentAzure;
using FluentAzure.Core;
using FluentAzure.Extensions;

namespace FluentAzure.Tests;

/// <summary>
/// Integration tests for the complete FluentAzure configuration pipeline.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "FluentAzureIntegrationTests",
        Guid.NewGuid().ToString()
    );

    public IntegrationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task EndToEnd_CompleteConfigurationPipeline_ShouldWork()
    {
        // Arrange
        var appSettingsPath = Path.Combine(_testDirectory, "appsettings.json");
        await File.WriteAllTextAsync(
            appSettingsPath,
            """
            {
                "App": {
                    "Name": "TestApp",
                    "Version": "1.0.0",
                    "Debug": true
                },
                "Database": {
                    "ConnectionString": "Server=localhost;Database=test",
                    "TimeoutSeconds": 30
                },
                "Features": {
                    "EnableLogging": true,
                    "MaxUsers": 1000
                }
            }
            """
        );

        // Set some environment variables to override JSON values
        Environment.SetEnvironmentVariable("App__Name", "TestApp-Production");
        Environment.SetEnvironmentVariable("Database__TimeoutSeconds", "60");
        Environment.SetEnvironmentVariable("NewFeature", "true");

        try
        {
            // Act
            var result = await FluentConfig()
                .FromJsonFile(appSettingsPath)
                .FromEnvironment()
                .Required("App:Name")
                .Required("Database:ConnectionString")
                .Optional("Features:EnableLogging", "false")
                .Optional("Features:MaxUsers", "500")
                .BuildAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();

            var config = result.Value!;
            config.Should().ContainKey("App:Name");
            config.Should().ContainKey("Database:ConnectionString");
            config.Should().ContainKey("Database:TimeoutSeconds");
            config.Should().ContainKey("Features:EnableLogging");
            config.Should().ContainKey("Features:MaxUsers");
            config.Should().ContainKey("NewFeature");

            // Environment variables should override JSON values
            config["App:Name"].Should().Be("TestApp-Production");
            config["Database:TimeoutSeconds"].Should().Be("60");
            config["NewFeature"].Should().Be("true");

            // Optional values should be set
            config["Features:EnableLogging"].Should().Be("false");
            config["Features:MaxUsers"].Should().Be("500");
        }
        finally
        {
            // Cleanup environment variables
            Environment.SetEnvironmentVariable("App__Name", null);
            Environment.SetEnvironmentVariable("Database__TimeoutSeconds", null);
            Environment.SetEnvironmentVariable("NewFeature", null);
        }
    }

    [Fact]
    public async Task Integration_WithValidationFailure_ShouldReturnErrors()
    {
        // Arrange
        var appSettingsPath = Path.Combine(_testDirectory, "invalid-settings.json");
        await File.WriteAllTextAsync(
            appSettingsPath,
            """
            {
                "Port": "99999",
                "MaxConnections": "-5"
            }
            """
        );

        // Act
        var result = await FluentConfig()
            .FromJsonFile(appSettingsPath)
            .Required("Port")
            .Required("MaxConnections")
            .Validate(
                "Port",
                port =>
                {
                    if (
                        int.TryParse(port, out var portNumber)
                        && portNumber >= 1
                        && portNumber <= 65535
                    )
                    {
                        return Result<string>.Success(port);
                    }
                    return Result<string>.Error($"Port must be between 1-65535, got {port}");
                }
            )
            .Validate(
                "MaxConnections",
                connections =>
                {
                    if (int.TryParse(connections, out var count) && count > 0)
                    {
                        return Result<string>.Success(connections);
                    }
                    return Result<string>.Error(
                        $"MaxConnections must be positive, got {connections}"
                    );
                }
            )
            .BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Contains("Port must be between 1-65535"));
        result.Errors.Should().Contain(e => e.Contains("MaxConnections must be positive"));
    }

    [Fact]
    public async Task Integration_WithTransformationFailure_ShouldReturnError()
    {
        // Arrange
        Environment.SetEnvironmentVariable("API_URL", "not-a-valid-url");

        try
        {
            // Act
            var result = await FluentAzure
                .Configuration()
                .FromEnvironment()
                .Required("API_URL")
                .Transform(
                    "API_URL",
                    url =>
                    {
                        if (Uri.TryCreate(url, UriKind.Absolute, out _))
                        {
                            return Result<string>.Success(url.ToUpperInvariant());
                        }
                        return Result<string>.Error($"API_URL is not a valid URL: {url}");
                    }
                )
                .BuildAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result
                .Errors.Should()
                .Contain(error => error.Contains("API_URL is not a valid URL: not-a-valid-url"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("API_URL", null);
        }
    }

    [Fact]
    public async Task Integration_WithMissingRequiredKeys_ShouldReturnErrors()
    {
        // Arrange
        var appSettingsPath = Path.Combine(_testDirectory, "minimal-settings.json");
        await File.WriteAllTextAsync(
            appSettingsPath,
            """
            {
                "App": {
                    "Name": "TestApp"
                }
            }
            """
        );

        // Act
        var result = await FluentAzure
            .Configuration()
            .FromJsonFile(appSettingsPath)
            .Required("App__Name")
            .Required("Database__ConnectionString")
            .Required("Api__Key")
            .BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result
            .Errors.Should()
            .Contain(error =>
                error.Contains("Required key 'Database__ConnectionString' was not found")
            );
        result
            .Errors.Should()
            .Contain(error => error.Contains("Required key 'Api__Key' was not found"));
    }

    [Fact]
    public async Task Integration_WithSourcePriorities_ShouldRespectOverrideOrder()
    {
        // Arrange
        var lowPriorityPath = Path.Combine(_testDirectory, "low-priority.json");
        var highPriorityPath = Path.Combine(_testDirectory, "high-priority.json");

        await File.WriteAllTextAsync(
            lowPriorityPath,
            """
            {
                "SharedSetting": "LowPriorityValue",
                "OnlyInLow": "LowValue"
            }
            """
        );

        await File.WriteAllTextAsync(
            highPriorityPath,
            """
            {
                "SharedSetting": "HighPriorityValue",
                "OnlyInHigh": "HighValue"
            }
            """
        );

        Environment.SetEnvironmentVariable("SharedSetting", "EnvironmentValue");

        try
        {
            // Act
            var result = await FluentAzure
                .Configuration()
                .FromJsonFile(lowPriorityPath, priority: 100)
                .FromJsonFile(highPriorityPath, priority: 200)
                .FromEnvironment(priority: 300) // Highest priority
                .BuildAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();

            result.Value["SharedSetting"].Should().Be("EnvironmentValue"); // Environment wins
            result.Value["OnlyInLow"].Should().Be("LowValue"); // Only in low priority
            result.Value["OnlyInHigh"].Should().Be("HighValue"); // Only in high priority
        }
        finally
        {
            Environment.SetEnvironmentVariable("SharedSetting", null);
        }
    }

    [Fact]
    public async Task Integration_WithComplexJsonStructure_ShouldFlattenCorrectly()
    {
        // Arrange
        var complexPath = Path.Combine(_testDirectory, "complex.json");
        await File.WriteAllTextAsync(
            complexPath,
            """
            {
                "Level1": {
                    "Level2": {
                        "Level3": {
                            "DeepValue": "Found me!",
                            "DeepArray": ["item1", "item2", "item3"]
                        },
                        "SimpleValue": "Level2Value"
                    }
                },
                "RootArray": [
                    { "Name": "First", "Value": 1 },
                    { "Name": "Second", "Value": 2 }
                ]
            }
            """
        );

        // Act
        var result = await FluentAzure
            .Configuration()
            .FromJsonFile(complexPath)
            .BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var config = result.Value;
        config["Level1__Level2__Level3__DeepValue"].Should().Be("Found me!");
        config["Level1__Level2__SimpleValue"].Should().Be("Level2Value");
        config["Level1__Level2__Level3__DeepArray__0"].Should().Be("item1");
        config["Level1__Level2__Level3__DeepArray__1"].Should().Be("item2");
        config["Level1__Level2__Level3__DeepArray__2"].Should().Be("item3");
        config["RootArray__0__Name"].Should().Be("First");
        config["RootArray__0__Value"].Should().Be("1");
        config["RootArray__1__Name"].Should().Be("Second");
        config["RootArray__1__Value"].Should().Be("2");
    }

    [Fact]
    public async Task Integration_WithChainedTransformations_ShouldApplyInOrder()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_VALUE", "hello");

        try
        {
            // Act
            var result = await FluentAzure
                .Configuration()
                .FromEnvironment()
                .Required("TEST_VALUE")
                .Transform("TEST_VALUE", value => Result<string>.Success(value.ToUpperInvariant()))
                .Transform("TEST_VALUE", value => Result<string>.Success($"[{value}]"))
                .Transform("TEST_VALUE", value => Result<string>.Success($"PREFIX_{value}_SUFFIX"))
                .BuildAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value["TEST_VALUE"].Should().Be("PREFIX_[HELLO]_SUFFIX");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_VALUE", null);
        }
    }

    [Fact]
    public async Task Integration_EmptyConfiguration_ShouldReturnEmptyResult()
    {
        // Act
        var result = await FluentAzure.Configuration().BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Integration_OnlyOptionalKeys_ShouldIncludeDefaults()
    {
        // Act
        var result = await FluentAzure
            .Configuration()
            .Optional("MissingKey1", "default1")
            .Optional("MissingKey2", "default2")
            .BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value["MissingKey1"].Should().Be("default1");
        result.Value["MissingKey2"].Should().Be("default2");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Configuration class for strongly-typed binding tests.
    /// </summary>
    public class AppConfiguration
    {
        public AppSettings App { get; set; } = new();
        public DatabaseSettings Database { get; set; } = new();
        public FeatureSettings Features { get; set; } = new();
        public bool NewFeature { get; set; }
    }

    public class AppSettings
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public bool Debug { get; set; }
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "";
        public int TimeoutSeconds { get; set; }
    }

    public class FeatureSettings
    {
        public bool EnableLogging { get; set; }
        public int MaxUsers { get; set; }
    }
}
