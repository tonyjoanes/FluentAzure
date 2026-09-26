using FluentAssertions;
using FluentAzure.Binding;
using FluentAzure.Core;

namespace FluentAzure.Tests.Binding;

/// <summary>
/// Tests that the enhanced binder only binds a property from the key whose full path matches it, and
/// leaves properties without a key alone.
/// </summary>
public class KeyMatchingBindingTests
{
    [Fact]
    public void Bind_WithRootKeyNamedLikeNestedProperty_ShouldPreferNestedKey()
    {
        // Arrange: the root key comes first, which used to win for Database.Name too
        var config = new Dictionary<string, string>
        {
            ["Name"] = "root",
            ["Database:Name"] = "orders",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Name.Should().Be("root");
        result.Value.Database.Name.Should().Be("orders");
    }

    [Fact]
    public void Bind_WithOnlyRootKey_ShouldNotBindNestedPropertyWithSameName()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "root",
            ["Database:Host"] = "db.local",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Database.Host.Should().Be("db.local");
        result.Value.Database.Name.Should().BeEmpty();
    }

    [Fact]
    public void Bind_WithListElementMissingProperty_ShouldNotBindRootKeyWithSameName()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "root",
            ["Endpoints:0:Url"] = "https://a.local",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Endpoints.Should().ContainSingle();
        result.Value.Endpoints[0].Url.Should().Be("https://a.local");
        result.Value.Endpoints[0].Name.Should().BeEmpty();
    }

    [Fact]
    public void Bind_WithSeparatorInDifferentPlace_ShouldNotBind()
    {
        // Arrange: "Data:BaseHost" and "Database:Host" only look alike with separators removed
        var config = new Dictionary<string, string> { ["Data:BaseHost"] = "wrong.local" };

        // Act
        var result = EnhancedConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Database.Host.Should().BeEmpty();
    }

    [Fact]
    public void Bind_WithCaseSensitiveOption_ShouldIgnoreKeysWithDifferentCase()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["name"] = "lower", ["DATABASE:HOST"] = "upper" };

        // Act
        var sensitive = EnhancedConfigurationBinder.Bind<AppSettings>(config, new BindingOptions { CaseSensitive = true });
        var insensitive = EnhancedConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        sensitive.Value!.Name.Should().BeEmpty();
        sensitive.Value.Database.Host.Should().BeEmpty();
        insensitive.Value!.Name.Should().Be("lower");
        insensitive.Value.Database.Host.Should().Be("upper");
    }

    [Fact]
    public void BindJson_ShouldNotCopyNameIntoOtherProperties()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Name"] = "App" };

        // Act
        var result = EnhancedConfigurationBinder.BindJson<JsonSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Name.Should().Be("App");
        result.Value.StringProperty.Should().BeEmpty();
    }

    [Fact]
    public void Bind_WithMissingKeys_ShouldKeepPropertyDefaults()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Name"] = "App" };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DefaultedSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Name.Should().Be("App");
        result.Value.TimeoutSeconds.Should().Be(30);
        result.Value.Region.Should().Be("westeurope");
    }

    [Fact]
    public void Bind_WithIgnoreMissingOptionalDisabled_ShouldResetMissingProperties()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Name"] = "App" };
        var options = new BindingOptions { IgnoreMissingOptional = false };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DefaultedSettings>(config, options);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.TimeoutSeconds.Should().Be(0);
        result.Value.Region.Should().BeNull();
    }

    private static string Describe<T>(Result<T> result) =>
        result.IsFailure ? string.Join(Environment.NewLine, result.Errors) : string.Empty;

    private sealed class AppSettings
    {
        public string Name { get; set; } = string.Empty;

        public DatabaseSettings Database { get; set; } = new();

        public List<Endpoint> Endpoints { get; set; } = new();
    }

    private sealed class DatabaseSettings
    {
        public string Name { get; set; } = string.Empty;

        public string Host { get; set; } = string.Empty;
    }

    private sealed class Endpoint
    {
        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;
    }

    private sealed class DefaultedSettings
    {
        public string Name { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 30;

        public string? Region { get; set; } = "westeurope";
    }

    private sealed class JsonSettings
    {
        public string Name { get; set; } = string.Empty;

        public string StringProperty { get; set; } = string.Empty;
    }
}
