using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using FluentAzure.Binding;
using FluentAzure.Core;
using Xunit;
using static FluentAzure.Tests.Binding.EnhancedConfigurationBinderParameterTests;
using FluentAzure.Tests.Binding;

namespace FluentAzure.Tests.Binding;

/// <summary>
/// Unit tests for the enhanced configuration binding system.
/// </summary>
public class EnhancedConfigurationBinderTests
{
    [Fact]
    public void Bind_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "Test App",
            ["Version"] = "1.0.0",
            ["MaxConnections"] = "100",
            ["EnableFeature"] = "true"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<TestConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test App");
        result.Value.Version.Should().Be("1.0.0");
        result.Value.MaxConnections.Should().Be(100);
        result.Value.EnableFeature.Should().BeTrue();
    }

    [Fact]
    public void Bind_WithNestedConfiguration_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "testdb",
            ["Api:BaseUrl"] = "https://api.example.com",
            ["Api:Timeout"] = "30"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<NestedConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Database.Host.Should().Be("localhost");
        result.Value.Database.Port.Should().Be(5432);
        result.Value.Database.Name.Should().Be("testdb");
        result.Value.Api.BaseUrl.Should().Be("https://api.example.com");
        result.Value.Api.Timeout.Should().Be(30);
    }

    [Fact]
    public void Bind_WithRecordType_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "Test Record",
            ["Version"] = "2.0.0",
            ["Environment"] = "Development",
            ["MaxConnections"] = "50",
            ["EnableFeature"] = "false"
        };

        // Print all constructors for TestRecord
        var ctors = typeof(TestRecord).GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            var paramList = string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            Console.WriteLine($"TestRecord constructor: ({paramList})");
        }

        // Act
        var result = EnhancedConfigurationBinder.Bind<TestRecord>(config);

        // Assert
        if (!result.IsSuccess)
        {
            Console.WriteLine("Binding errors: " + string.Join("; ", result.Errors));
        }
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test Record");
        result.Value.Version.Should().Be("2.0.0");
        result.Value.Environment.Should().Be("Development");
        result.Value.MaxConnections.Should().Be(50);
        result.Value.EnableFeature.Should().BeFalse();
    }

    [Fact]
    public void Bind_WithCollection_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Items__0__Name"] = "Item 1",
            ["Items__0__Value"] = "100",
            ["Items__1__Name"] = "Item 2",
            ["Items__1__Value"] = "200"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<CollectionConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items[0].Name.Should().Be("Item 1");
        result.Value.Items[0].Value.Should().Be(100);
        result.Value.Items[1].Name.Should().Be("Item 2");
        result.Value.Items[1].Value.Should().Be(200);
    }

    [Fact]
    public void Bind_WithValidationErrors_ShouldFail()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Email"] = "invalid-email",
            ["Age"] = "150",
            ["RequiredField"] = ""
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<ValidatedConfig>(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("Email"));
        result.Errors.Should().Contain(e => e.Contains("Age"));
        result.Errors.Should().Contain(e => e.Contains("RequiredField"));
    }

    [Fact]
    public void Bind_WithoutValidation_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Email"] = "invalid-email",
            ["Age"] = "150"
        };

        var options = new BindingOptions { EnableValidation = false };

        // Act
        var result = EnhancedConfigurationBinder.Bind<ValidatedConfig>(config, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be("invalid-email");
        result.Value.Age.Should().Be(150);
    }

    [Fact]
    public void BindJson_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "Test App",
            ["Version"] = "1.0.0",
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432"
        };

        // Act
        var result = EnhancedConfigurationBinder.BindJson<TestConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test App");
        result.Value.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void BindJson_WithCustomOptions_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["name"] = "Test App",
            ["version"] = "1.0.0"
        };

        var options = new BindingOptions
        {
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        };

        // Act
        var result = EnhancedConfigurationBinder.BindJson<TestConfig>(config, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test App");
        result.Value.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Bind_WithCaseSensitiveKeys_ShouldRespectCase()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "Correct Case",
            ["name"] = "Wrong Case"
        };

        var options = new BindingOptions { CaseSensitive = true };

        // Act
        var result = EnhancedConfigurationBinder.Bind<TestConfig>(config, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Correct Case");
    }

    [Fact]
    public void Bind_WithMissingRequiredProperty_ShouldFail()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["OptionalField"] = "value"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<RequiredConfig>(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("RequiredField"));
    }

    [Fact]
    public void Bind_WithInitOnlyProperties_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "Init Only",
            ["Value"] = "42"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<InitOnlyConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Init Only");
        result.Value.Value.Should().Be(42);
    }

    [Fact]
    public void Bind_WithComplexNestedObject_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["User:Profile:FirstName"] = "John",
            ["User:Profile:LastName"] = "Doe",
            ["User:Profile:Age"] = "30",
            ["User:Settings:Theme"] = "dark",
            ["User:Settings:Language"] = "en-US"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<ComplexConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.User.Profile.FirstName.Should().Be("John");
        result.Value.User.Profile.LastName.Should().Be("Doe");
        result.Value.User.Profile.Age.Should().Be(30);
        result.Value.User.Settings.Theme.Should().Be("dark");
        result.Value.User.Settings.Language.Should().Be("en-US");
    }

    [Fact]
    public void Bind_WithEnumValues_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Status"] = "Active",
            ["Type"] = "Admin"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<EnumConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(UserStatus.Active);
        result.Value.Type.Should().Be(UserType.Admin);
    }

    [Fact]
    public void Bind_WithNullableTypes_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["RequiredInt"] = "42",
            ["OptionalInt"] = "100",
            ["RequiredString"] = "required",
            ["OptionalString"] = "optional"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<NullableConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RequiredInt.Should().Be(42);
        result.Value.OptionalInt.Should().Be(100);
        result.Value.RequiredString.Should().Be("required");
        result.Value.OptionalString.Should().Be("optional");
    }

    [Fact]
    public void Bind_WithEmptyNullableValues_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["RequiredInt"] = "42",
            ["OptionalInt"] = "",
            ["RequiredString"] = "required",
            ["OptionalString"] = ""
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<NullableConfig>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RequiredInt.Should().Be(42);
        result.Value.OptionalInt.Should().BeNull();
        result.Value.RequiredString.Should().Be("required");
        result.Value.OptionalString.Should().BeNull();
    }

    [Fact]
    public void Bind_WithInvalidTypeConversion_ShouldFail()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["IntValue"] = "not-a-number",
            ["BoolValue"] = "not-a-boolean"
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<TestConfig>(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("IntValue"));
        result.Errors.Should().Contain(e => e.Contains("BoolValue"));
    }

    [Fact]
    public void Bind_WithExistingInstance_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "Updated Name",
            ["Version"] = "2.0.0"
        };

        var instance = new TestConfig { Name = "Original Name", Version = "1.0.0" };

        // Act
        var result = EnhancedConfigurationBinder.BindToInstance(config, instance);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(instance);
        result.Value!.Name.Should().Be("Updated Name");
        result.Value.Version.Should().Be("2.0.0");
    }
}

// Test configuration classes

public class TestConfig
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int MaxConnections { get; set; }
    public bool EnableFeature { get; set; }
    public int IntValue { get; set; }
    public bool BoolValue { get; set; }
}

public class NestedConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public ApiConfig Api { get; set; } = new();
}

public class DatabaseConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ApiConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public int Timeout { get; set; }
}

public class CollectionConfig
{
    public List<CollectionItem> Items { get; set; } = new();
}

public class CollectionItem
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class ValidatedConfig
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(1, 120)]
    public int Age { get; set; }

    [Required]
    public string RequiredField { get; set; } = string.Empty;
}

public class RequiredConfig
{
    [Required]
    public string RequiredField { get; set; } = string.Empty;
    public string OptionalField { get; set; } = string.Empty;
}

public class InitOnlyConfig
{
    public string Name { get; init; } = string.Empty;
    public int Value { get; init; }
}

public class ComplexConfig
{
    public UserConfig User { get; set; } = new();
}

public class UserConfig
{
    public UserProfile Profile { get; set; } = new();
    public UserSettings Settings { get; set; } = new();
}

public class UserProfile
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class UserSettings
{
    public string Theme { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}

public class EnumConfig
{
    public UserStatus Status { get; set; }
    public UserType Type { get; set; }
}

public enum UserType
{
    User,
    Admin,
    Moderator
}

public class NullableConfig
{
    public int RequiredInt { get; set; }
    public int? OptionalInt { get; set; }
    public string RequiredString { get; set; } = string.Empty;
    public string? OptionalString { get; set; }
}
