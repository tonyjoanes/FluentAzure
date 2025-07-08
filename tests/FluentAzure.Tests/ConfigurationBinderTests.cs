using FluentAssertions;
using FluentAzure.Binding;

namespace FluentAzure.Tests;

/// <summary>
/// Tests for the ConfigurationBinder class.
/// </summary>
public class ConfigurationBinderTests
{
    [Fact]
    public void Bind_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange
        var instance = new SimpleTestClass();

        // Act & Assert
        FluentActions
            .Invoking(() => ConfigurationBinder.Bind<SimpleTestClass>(null!, instance))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bind_WithNullInstance_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = new Dictionary<string, string>();

        // Act & Assert
        FluentActions
            .Invoking(() => ConfigurationBinder.Bind<SimpleTestClass>(config, null!))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bind_WithEmptyConfiguration_ShouldReturnSuccessWithDefaultValues()
    {
        // Arrange
        var config = new Dictionary<string, string>();
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(instance);
        result.Value.StringProperty.Should().Be("");
        result.Value.IntProperty.Should().Be(0);
        result.Value.BoolProperty.Should().BeFalse();
    }

    [Fact]
    public void Bind_WithStringProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["StringProperty"] = "Test Value" };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.StringProperty.Should().Be("Test Value");
    }

    [Fact]
    public void Bind_WithIntProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["IntProperty"] = "42" };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.IntProperty.Should().Be(42);
    }

    [Fact]
    public void Bind_WithBoolProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["BoolProperty"] = "true" };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.BoolProperty.Should().BeTrue();
    }

    [Fact]
    public void Bind_WithInvalidIntValue_ShouldReturnError()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["IntProperty"] = "not-a-number" };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Contains("IntProperty"));
    }

    [Fact]
    public void Bind_WithInvalidBoolValue_ShouldReturnError()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["BoolProperty"] = "maybe" };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Contains("BoolProperty"));
    }

    [Fact]
    public void Bind_WithCaseInsensitiveMatch_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["stringproperty"] = "Test Value",
            ["INTPROPERTY"] = "42",
        };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.StringProperty.Should().Be("Test Value");
        result.Value.IntProperty.Should().Be(42);
    }

    [Fact]
    public void Bind_WithNullableTypes_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["NullableInt"] = "100",
            ["NullableBool"] = "false",
        };
        var instance = new NullableTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.NullableInt.Should().Be(100);
        result.Value.NullableBool.Should().BeFalse();
    }

    [Fact]
    public void Bind_WithEmptyStringForNullable_ShouldSetToNull()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["NullableInt"] = "", ["NullableBool"] = "" };
        var instance = new NullableTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.NullableInt.Should().BeNull();
        result.Value.NullableBool.Should().BeNull();
    }

    [Fact]
    public void Bind_WithDateTimeProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["DateTimeProperty"] = "2023-12-25T10:30:00",
        };
        var instance = new AdvancedTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.DateTimeProperty.Should().Be(new DateTime(2023, 12, 25, 10, 30, 0));
    }

    [Fact]
    public void Bind_WithTimeSpanProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["TimeSpanProperty"] = "01:30:45" };
        var instance = new AdvancedTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.TimeSpanProperty.Should().Be(new TimeSpan(1, 30, 45));
    }

    [Fact]
    public void Bind_WithGuidProperty_ShouldBindCorrectly()
    {
        // Arrange
        var testGuid = Guid.NewGuid();
        var config = new Dictionary<string, string> { ["GuidProperty"] = testGuid.ToString() };
        var instance = new AdvancedTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.GuidProperty.Should().Be(testGuid);
    }

    [Fact]
    public void Bind_WithUriProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["UriProperty"] = "https://example.com/test",
        };
        var instance = new AdvancedTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.UriProperty.Should().Be(new Uri("https://example.com/test"));
    }

    [Fact]
    public void Bind_WithEnumProperty_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["EnumProperty"] = "Value2" };
        var instance = new AdvancedTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.EnumProperty.Should().Be(TestEnum.Value2);
    }

    [Fact]
    public void Bind_WithEnumProperty_CaseInsensitive_ShouldBindCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["EnumProperty"] = "value3" };
        var instance = new AdvancedTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.EnumProperty.Should().Be(TestEnum.Value3);
    }

    [Fact]
    public void Bind_WithMultipleProperties_ShouldBindAll()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["StringProperty"] = "Hello",
            ["IntProperty"] = "123",
            ["BoolProperty"] = "true",
        };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.StringProperty.Should().Be("Hello");
        result.Value.IntProperty.Should().Be(123);
        result.Value.BoolProperty.Should().BeTrue();
    }

    [Fact]
    public void Bind_WithMultipleErrors_ShouldAccumulateAllErrors()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["IntProperty"] = "not-a-number",
            ["BoolProperty"] = "not-a-bool",
        };
        var instance = new SimpleTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(error => error.Contains("IntProperty"));
        result.Errors.Should().Contain(error => error.Contains("BoolProperty"));
    }

    [Fact]
    public void Bind_StaticMethod_WithNewInstance_ShouldCreateAndBind()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["StringProperty"] = "Test Value",
            ["IntProperty"] = "42",
        };

        // Act
        var result = ConfigurationBinder.Bind<SimpleTestClass>(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.StringProperty.Should().Be("Test Value");
        result.Value.IntProperty.Should().Be(42);
    }

    [Fact]
    public void Bind_WithReadOnlyProperty_ShouldSkipProperty()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["ReadOnlyProperty"] = "Should be ignored",
            ["WritableProperty"] = "Should be set",
        };
        var instance = new ReadOnlyTestClass();

        // Act
        var result = ConfigurationBinder.Bind(config, instance);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.ReadOnlyProperty.Should().Be("Default"); // Should remain unchanged
        result.Value.WritableProperty.Should().Be("Should be set");
    }

    // Test helper classes
    public class SimpleTestClass
    {
        public string StringProperty { get; set; } = "";
        public int IntProperty { get; set; }
        public bool BoolProperty { get; set; }
    }

    public class NullableTestClass
    {
        public int? NullableInt { get; set; }
        public bool? NullableBool { get; set; }
    }

    public class AdvancedTestClass
    {
        public DateTime DateTimeProperty { get; set; }
        public TimeSpan TimeSpanProperty { get; set; }
        public Guid GuidProperty { get; set; }
        public Uri? UriProperty { get; set; }
        public TestEnum EnumProperty { get; set; }
        public decimal DecimalProperty { get; set; }
        public double DoubleProperty { get; set; }
        public long LongProperty { get; set; }
    }

    public class ReadOnlyTestClass
    {
        public string ReadOnlyProperty { get; } = "Default";
        public string WritableProperty { get; set; } = "";
    }

    public enum TestEnum
    {
        Value1,
        Value2,
        Value3,
    }
}
