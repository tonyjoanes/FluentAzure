using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAzure;
using Xunit;

namespace FluentAzure.Tests;

/// <summary>
/// Tests for configuration source implementations.
/// </summary>
public class ConfigurationSourceTests
{
    public class EnvironmentSourceTests
    {
        [Fact]
        public void Constructor_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var source = new EnvironmentSource(150);

            // Assert
            source.Name.Should().Be("Environment");
            source.Priority.Should().Be(150);
        }

        [Fact]
        public void Constructor_WithDefaultPriority_ShouldUseDefaultValue()
        {
            // Arrange & Act
            var source = new EnvironmentSource();

            // Assert
            source.Name.Should().Be("Environment");
            source.Priority.Should().Be(100);
        }

        [Fact]
        public async Task LoadAsync_ShouldReturnEnvironmentVariables()
        {
            // Arrange
            var source = new EnvironmentSource();

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().NotBeEmpty(); // Should have at least some environment variables
        }

        [Fact]
        public async Task LoadAsync_ShouldIncludePathVariable()
        {
            // Arrange
            var source = new EnvironmentSource();

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            // PATH should exist on all systems (Windows uses PATH, Unix uses PATH)
            var hasPath = result.Value.ContainsKey("PATH") || result.Value.ContainsKey("Path");
            hasPath.Should().BeTrue("PATH environment variable should exist");
        }

        [Fact]
        public void ContainsKey_WithExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var source = new EnvironmentSource();
            var testKey = Environment.GetEnvironmentVariables().Keys.Cast<string>().First();

            // Act
            var result = source.ContainsKey(testKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ContainsKey_WithNonExistingKey_ShouldReturnFalse()
        {
            // Arrange
            var source = new EnvironmentSource();

            // Act
            var result = source.ContainsKey("NON_EXISTING_KEY_12345");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetValue_WithExistingKey_ShouldReturnValue()
        {
            // Arrange
            var source = new EnvironmentSource();
            var envVars = Environment.GetEnvironmentVariables();
            var testKey = envVars.Keys.Cast<string>().First();
            var expectedValue = envVars[testKey]?.ToString();

            // Act
            var result = source.GetValue(testKey);

            // Assert
            result.Should().Be(expectedValue);
        }

        [Fact]
        public void GetValue_WithNonExistingKey_ShouldReturnNull()
        {
            // Arrange
            var source = new EnvironmentSource();

            // Act
            var result = source.GetValue("NON_EXISTING_KEY_12345");

            // Assert
            result.Should().BeNull();
        }
    }

    public class JsonFileSourceTests : IDisposable
    {
        private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "FluentAzureTests", Guid.NewGuid().ToString());

        public JsonFileSourceTests()
        {
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public void Constructor_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var source = new JsonFileSource("test.json", 75, true);

            // Assert
            source.Name.Should().Be("JsonFile(test.json)");
            source.Priority.Should().Be(75);
        }

        [Fact]
        public void Constructor_WithDefaultValues_ShouldUseDefaults()
        {
            // Arrange & Act
            var source = new JsonFileSource("config.json");

            // Assert
            source.Name.Should().Be("JsonFile(config.json)");
            source.Priority.Should().Be(50);
        }

        [Fact]
        public void Constructor_WithNullFilePath_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new JsonFileSource(null!))
                         .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task LoadAsync_WithValidJsonFile_ShouldReturnFlattenedConfiguration()
        {
            // Arrange
            var jsonContent = """
            {
                "ConnectionString": "Server=localhost;Database=test",
                "Logging": {
                    "Level": "Information",
                    "Providers": ["Console", "File"]
                },
                "Port": 8080,
                "Debug": true
            }
            """;
            var filePath = Path.Combine(_testDirectory, "valid.json");
            await File.WriteAllTextAsync(filePath, jsonContent);
            var source = new JsonFileSource(filePath);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().Contain("ConnectionString", "Server=localhost;Database=test");
            result.Value.Should().Contain("Logging__Level", "Information");
            result.Value.Should().Contain("Logging__Providers__0", "Console");
            result.Value.Should().Contain("Logging__Providers__1", "File");
            result.Value.Should().Contain("Port", "8080");
            result.Value.Should().Contain("Debug", "true");
        }

        [Fact]
        public async Task LoadAsync_WithEmptyJsonFile_ShouldReturnEmptyConfiguration()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "empty.json");
            await File.WriteAllTextAsync(filePath, "{}");
            var source = new JsonFileSource(filePath);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task LoadAsync_WithWhitespaceFile_ShouldReturnEmptyConfiguration()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "whitespace.json");
            await File.WriteAllTextAsync(filePath, "   \n  \t  ");
            var source = new JsonFileSource(filePath);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task LoadAsync_WithMissingOptionalFile_ShouldReturnEmptyConfiguration()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "missing.json");
            var source = new JsonFileSource(filePath, optional: true);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task LoadAsync_WithMissingRequiredFile_ShouldReturnError()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "missing-required.json");
            var source = new JsonFileSource(filePath, optional: false);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(error => error.Contains("was not found"));
        }

        [Fact]
        public async Task LoadAsync_WithInvalidJson_ShouldReturnError()
        {
            // Arrange
            var jsonContent = "{ invalid json content }";
            var filePath = Path.Combine(_testDirectory, "invalid.json");
            await File.WriteAllTextAsync(filePath, jsonContent);
            var source = new JsonFileSource(filePath);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(error => error.Contains("Failed to parse JSON"));
        }

        [Fact]
        public async Task LoadAsync_WithNestedArrays_ShouldFlattenCorrectly()
        {
            // Arrange
            var jsonContent = """
            {
                "Items": [
                    { "Name": "Item1", "Value": 100 },
                    { "Name": "Item2", "Value": 200 }
                ]
            }
            """;
            var filePath = Path.Combine(_testDirectory, "arrays.json");
            await File.WriteAllTextAsync(filePath, jsonContent);
            var source = new JsonFileSource(filePath);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("Items__0__Name", "Item1");
            result.Value.Should().Contain("Items__0__Value", "100");
            result.Value.Should().Contain("Items__1__Name", "Item2");
            result.Value.Should().Contain("Items__1__Value", "200");
        }

        [Fact]
        public async Task LoadAsync_WithNullValues_ShouldHandleCorrectly()
        {
            // Arrange
            var jsonContent = """
            {
                "NullValue": null,
                "EmptyString": "",
                "ValidValue": "test"
            }
            """;
            var filePath = Path.Combine(_testDirectory, "nulls.json");
            await File.WriteAllTextAsync(filePath, jsonContent);
            var source = new JsonFileSource(filePath);

            // Act
            var result = await source.LoadAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("NullValue", "");
            result.Value.Should().Contain("EmptyString", "");
            result.Value.Should().Contain("ValidValue", "test");
        }

        [Fact]
        public void ContainsKey_BeforeLoad_ShouldReturnFalse()
        {
            // Arrange
            var source = new JsonFileSource("any.json");

            // Act
            var result = source.ContainsKey("AnyKey");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ContainsKey_AfterLoad_ShouldReturnCorrectValue()
        {
            // Arrange
            var jsonContent = """{ "ExistingKey": "value" }""";
            var filePath = Path.Combine(_testDirectory, "contains.json");
            await File.WriteAllTextAsync(filePath, jsonContent);
            var source = new JsonFileSource(filePath);
            await source.LoadAsync();

            // Act & Assert
            source.ContainsKey("ExistingKey").Should().BeTrue();
            source.ContainsKey("MissingKey").Should().BeFalse();
        }

        [Fact]
        public async Task GetValue_AfterLoad_ShouldReturnCorrectValue()
        {
            // Arrange
            var jsonContent = """{ "TestKey": "TestValue" }""";
            var filePath = Path.Combine(_testDirectory, "getvalue.json");
            await File.WriteAllTextAsync(filePath, jsonContent);
            var source = new JsonFileSource(filePath);
            await source.LoadAsync();

            // Act & Assert
            source.GetValue("TestKey").Should().Be("TestValue");
            source.GetValue("MissingKey").Should().BeNull();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}
