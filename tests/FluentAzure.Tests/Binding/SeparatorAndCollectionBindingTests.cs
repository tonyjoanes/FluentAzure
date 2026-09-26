using FluentAssertions;
using FluentAzure.Binding;
using FluentAzure.Core;
using FluentAzure.Sources;

namespace FluentAzure.Tests.Binding;

/// <summary>
/// Tests for the ":" hierarchy separator in the basic binder, and for simple-type collections and
/// dictionaries in the enhanced binder.
/// </summary>
public class SeparatorAndCollectionBindingTests
{
    private const string SecretValue = "hunter2-super-secret";

    [Fact]
    public void BasicBind_WithColonSeparatedKeys_ShouldBindNestedProperties()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Name"] = "App",
            ["Database:Host"] = "db.local",
            ["Database:Port"] = "5432",
        };

        // Act
        var result = ConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Database.Host.Should().Be("db.local");
        result.Value.Database.Port.Should().Be(5432);
    }

    [Fact]
    public void BasicBind_WithDoubleUnderscoreKeys_ShouldStillBindNestedProperties()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["database__host"] = "db.local",
            ["DATABASE__PORT"] = "5432",
        };

        // Act
        var result = ConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Database.Host.Should().Be("db.local");
        result.Value.Database.Port.Should().Be(5432);
    }

    [Fact]
    public void BasicBind_WithBothSeparatorsForSameKey_ShouldPreferColonKey()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Database__Host"] = "derived",
            ["Database:Host"] = "explicit",
        };

        // Act
        var result = ConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.Value!.Database.Host.Should().Be("explicit");
    }

    [Fact]
    public async Task BuildAsyncOfT_WithColonKeysFromSource_ShouldBindNestedProperties()
    {
        // Arrange: Key Vault maps "Database--Host" to "Database:Host"
        var pipeline = FluentConfig
            .Create()
            .AddSource(new InMemorySource(new() { ["Database:Host"] = "db.local", ["Database:Port"] = "5432" }));

        // Act
        var result = await pipeline.BuildAsync<AppSettings>();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Database.Host.Should().Be("db.local");
        result.Value.Database.Port.Should().Be(5432);
    }

    [Fact]
    public void BasicBind_WithUnconvertibleNestedValue_ShouldNameKeyButNotValue()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Database:Port"] = SecretValue };

        // Act
        var result = ConfigurationBinder.Bind<AppSettings>(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Database:Port");
        string.Join(" ", result.Errors).Should().NotContain(SecretValue);
    }

    [Fact]
    public void EnhancedBind_WithStringListAndArray_ShouldBindElementsInIndexOrder()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Hosts:1"] = "b.local",
            ["Hosts:0"] = "a.local",
            ["Tags__0"] = "blue",
            ["Tags__1"] = "green",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<CollectionSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Hosts.Should().Equal("a.local", "b.local");
        result.Value.Tags.Should().Equal("blue", "green");
    }

    [Fact]
    public void EnhancedBind_WithNumberArrayAndList_ShouldConvertElements()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Ports:0"] = "80",
            ["Ports:1"] = "443",
            ["Weights:0"] = "0.5",
            ["Weights:1"] = "1.25",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<CollectionSettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Ports.Should().Equal(80, 443);
        result.Value.Weights.Should().Equal(0.5, 1.25);
    }

    [Fact]
    public void EnhancedBind_WithUnconvertibleElement_ShouldNameKeyButNotValue()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Ports:0"] = "80", ["Ports:1"] = SecretValue };

        // Act
        var result = EnhancedConfigurationBinder.Bind<CollectionSettings>(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Ports:1");
        string.Join(" ", result.Errors).Should().NotContain(SecretValue);
    }

    [Fact]
    public void EnhancedBind_WithSimpleValueDictionaries_ShouldBindEntries()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["ConnectionStrings:Primary"] = "Server=a",
            ["ConnectionStrings__Replica"] = "Server=b",
            ["Limits:Requests"] = "100",
            ["Limits:Bursts"] = "10",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DictionarySettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.ConnectionStrings.Should().HaveCount(2);
        result.Value.ConnectionStrings["Primary"].Should().Be("Server=a");
        result.Value.ConnectionStrings["Replica"].Should().Be("Server=b");
        result.Value.Limits.Should().BeEquivalentTo(new Dictionary<string, int> { ["Requests"] = 100, ["Bursts"] = 10 });
    }

    [Fact]
    public void EnhancedBind_WithObjectValueDictionary_ShouldBindEachEntry()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Databases:Orders:Host"] = "orders.local",
            ["Databases:Orders:Port"] = "5432",
            ["Databases:Billing:Host"] = "billing.local",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DictionarySettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Databases.Should().HaveCount(2);
        result.Value.Databases["Orders"].Host.Should().Be("orders.local");
        result.Value.Databases["Orders"].Port.Should().Be(5432);
        result.Value.Databases["Billing"].Host.Should().Be("billing.local");
    }

    [Fact]
    public void EnhancedBind_WithInterfaceDictionaries_ShouldCreateAndBindThem()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["Features:Search"] = "true",
            ["Timeouts:Read"] = "00:00:30",
        };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DictionarySettings>(config);

        // Assert
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value!.Features!["search"].Should().BeTrue("dictionaries the binder creates ignore key case");
        result.Value.Timeouts!["Read"].Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void EnhancedBind_WithDictionaryDefaults_ShouldAddToExistingEntries()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Defaults:Region"] = "westeurope" };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DictionarySettings>(config);

        // Assert
        result.Value!.Defaults.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["Environment"] = "Production", ["Region"] = "westeurope" });
    }

    [Fact]
    public void EnhancedBind_WithInvalidDictionaryKey_ShouldReturnError()
    {
        // Arrange
        var config = new Dictionary<string, string> { ["Retries:1"] = "one", ["Retries:two"] = "two" };

        // Act
        var result = EnhancedConfigurationBinder.Bind<DictionarySettings>(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Retries:two");
    }

    private static string Describe<T>(Result<T> result) =>
        result.IsFailure ? string.Join(Environment.NewLine, result.Errors) : string.Empty;

    private sealed class AppSettings
    {
        public string Name { get; set; } = string.Empty;

        public DatabaseSettings Database { get; set; } = new();
    }

    private sealed class DatabaseSettings
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; }
    }

    private sealed class CollectionSettings
    {
        public List<string> Hosts { get; set; } = new();

        public string[] Tags { get; set; } = Array.Empty<string>();

        public int[] Ports { get; set; } = Array.Empty<int>();

        public List<double> Weights { get; set; } = new();
    }

    private sealed class DictionarySettings
    {
        public Dictionary<string, string> ConnectionStrings { get; set; } = new();

        public Dictionary<string, int> Limits { get; set; } = new();

        public Dictionary<string, DatabaseSettings> Databases { get; set; } = new();

        public IDictionary<string, bool>? Features { get; set; }

        public IReadOnlyDictionary<string, TimeSpan>? Timeouts { get; set; }

        public Dictionary<string, string> Defaults { get; set; } = new() { ["Environment"] = "Production" };

        public Dictionary<int, string> Retries { get; set; } = new();
    }
}
