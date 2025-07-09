using FluentAssertions;
using FluentAzure.Core;

namespace FluentAzure.Tests;

/// <summary>
/// Tests for the ConfigurationBuilder class.
/// </summary>
public class ConfigurationBuilderTests
{
    [Fact]
    public void Configuration_ShouldReturnNewBuilderInstance()
    {
        // Act
        var builder = FluentAzure.FluentConfig.Create();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ConfigurationBuilder>();
    }

    [Fact]
    public void FromEnvironment_ShouldReturnSameBuilderInstance()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.FromEnvironment();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void FromJsonFile_WithValidPath_ShouldReturnSameBuilderInstance()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.FromJsonFile("test.json");

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void FromJsonFile_WithNullPath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.FromJsonFile(null!)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromJsonFile_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.FromJsonFile("")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromKeyVault_WithValidUrl_ShouldReturnSameBuilderInstance()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.FromKeyVault("https://test.vault.azure.net");

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void FromKeyVault_WithNullUrl_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.FromKeyVault(null!)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Required_WithValidKey_ShouldReturnSameBuilderInstance()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.Required("TestKey");

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void Required_WithNullKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.Required(null!)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Required_WithEmptyKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.Required("")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Optional_WithValidKeyAndValue_ShouldReturnSameBuilderInstance()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.Optional("TestKey", "TestValue");

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void Optional_WithNullKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.Optional(null!, "value")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Optional_WithNullValue_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.Optional("key", null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_WithValidFunction_ShouldReturnSameBuilderInstance()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.Validate(config => null);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void Validate_WithNullFunction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.Validate(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAsync_WithNoSources_ShouldReturnEmptyConfiguration()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_WithOptionalKeys_ShouldIncludeDefaultValues()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("Key1", "Value1")
            .Optional("Key2", "Value2");

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Key1", "Value1");
        result.Value.Should().Contain("Key2", "Value2");
    }

    [Fact]
    public async Task BuildAsync_WithMissingRequiredKey_ShouldReturnError()
    {
        // Arrange
        var builder = new ConfigurationBuilder().Required("MissingKey");

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result
            .Errors.Should()
            .Contain(error => error.Contains("Required key 'MissingKey' was not found"));
    }

    [Fact]
    public async Task BuildAsync_WithValidation_ShouldApplyValidationRules()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("Port", "99999")
            .Validate(config =>
            {
                if (
                    config.TryGetValue("Port", out var portStr)
                    && int.TryParse(portStr, out var port)
                    && port > 65535
                )
                {
                    return "Port must be <= 65535";
                }
                return null;
            });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Port must be <= 65535");
    }

    [Fact]
    public async Task BuildAsync_WithValidConfiguration_ShouldPassValidation()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("Port", "8080")
            .Validate(config =>
            {
                if (
                    config.TryGetValue("Port", out var portStr)
                    && int.TryParse(portStr, out var port)
                    && port > 65535
                )
                {
                    return "Port must be <= 65535";
                }
                return null;
            });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Port", "8080");
    }

    [Fact]
    public async Task BuildAsync_WithTransformation_ShouldApplyTransform()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("Key1", "value1")
            .Transform(async config =>
            {
                var transformed = new Dictionary<string, string>(config);
                if (transformed.ContainsKey("Key1"))
                {
                    transformed["Key1"] = transformed["Key1"].ToUpperInvariant();
                }
                return await Task.FromResult(
                    Result<Dictionary<string, string>>.Success(transformed)
                );
            });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Key1", "VALUE1");
    }

    [Fact]
    public async Task BuildAsync_WithFailingTransformation_ShouldReturnError()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("Key1", "value1")
            .Transform(async config =>
            {
                return await Task.FromResult(
                    Result<Dictionary<string, string>>.Error("Transform failed")
                );
            });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Transform failed");
    }

    [Fact]
    public async Task BuildAsync_WithMultipleValidations_ShouldAccumulateErrors()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("Port", "99999")
            .Optional("Name", "")
            .Validate(config =>
            {
                if (
                    config.TryGetValue("Port", out var portStr)
                    && int.TryParse(portStr, out var port)
                    && port > 65535
                )
                {
                    return "Port must be <= 65535";
                }
                return null;
            })
            .Validate(config =>
            {
                if (config.TryGetValue("Name", out var name) && string.IsNullOrEmpty(name))
                {
                    return "Name cannot be empty";
                }
                return null;
            });

        // Act
        var result = await builder.BuildAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Port must be <= 65535");
        result.Errors.Should().Contain("Name cannot be empty");
    }

    [Fact]
    public async Task BuildAsync_Generic_WithValidConfiguration_ShouldBindToType()
    {
        // Arrange
        var builder = new ConfigurationBuilder()
            .Optional("AppName", "Test App")
            .Optional("Debug", "true")
            .Optional("MaxConnections", "50");

        // Act
        var result = await builder.BuildAsync<TestAppSettings>();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AppName.Should().Be("Test App");
        result.Value.Debug.Should().BeTrue();
        result.Value.MaxConnections.Should().Be(50);
    }

    [Fact]
    public async Task BuildAsync_Generic_WithInvalidTypeConversion_ShouldReturnError()
    {
        // Arrange
        var builder = new ConfigurationBuilder().Optional("MaxConnections", "not-a-number");

        // Act
        var result = await builder.BuildAsync<TestAppSettings>();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Contains("Failed to bind"));
    }

    [Fact]
    public async Task BuildAsync_Generic_WithConfigurationErrors_ShouldReturnConfigurationErrors()
    {
        // Arrange
        var builder = new ConfigurationBuilder().Required("MissingKey");

        // Act
        var result = await builder.BuildAsync<TestAppSettings>();

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result
            .Errors.Should()
            .Contain(error => error.Contains("Required key 'MissingKey' was not found"));
    }

    [Fact]
    public void Transform_WithNullFunction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        builder.Invoking(b => b.Transform(null!)).Should().Throw<ArgumentNullException>();
    }

    // Test helper class
    public class TestAppSettings
    {
        public string AppName { get; set; } = "";
        public bool Debug { get; set; }
        public int MaxConnections { get; set; }
    }
}
