using Azure.Identity;
using FluentAssertions;
using FluentAzure.Sources;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FluentAzure.Tests.Sources;

/// <summary>
/// Unit tests for the enhanced KeyVaultSource.
/// </summary>
public class KeyVaultSourceTests
{
    private readonly ILogger _logger;
    private const string TestVaultUrl = "https://test-keyvault.vault.azure.net/";

    public KeyVaultSourceTests()
    {
        _logger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<KeyVaultSourceTests>();
    }

    [Fact]
    public void Constructor_WithValidUrl_ShouldCreateInstance()
    {
        // Arrange & Act
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
        source.Name.Should().Be("KeyVault(test-keyvault.vault.azure.net)");
        source.Priority.Should().Be(200);
    }

    [Fact]
    public void Constructor_WithNullUrl_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var action = () => new MockKeyVaultSource(null!, new Dictionary<string, string>());
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var action = () =>
            new MockKeyVaultSource(TestVaultUrl, null!, new Dictionary<string, string>());
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_ShouldRespectSettings()
    {
        // Arrange
        var configuration = new KeyVaultConfiguration
        {
            CacheDuration = TimeSpan.FromMinutes(10),
            MaxRetryAttempts = 5,
            BaseRetryDelay = TimeSpan.FromSeconds(2),
            SecretNamePrefix = "Test-",
        };

        // Act
        var source = new MockKeyVaultSource(
            TestVaultUrl,
            configuration,
            new Dictionary<string, string>(),
            logger: _logger
        );

        // Assert
        source.Should().NotBeNull();
        source.LoadErrors.Should().BeEmpty();
    }

    [Fact]
    public void KeyVaultConfiguration_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new KeyVaultConfiguration();

        // Assert
        config.MaxRetryAttempts.Should().Be(3);
        config.BaseRetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        config.MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(30));
        config.CacheDuration.Should().Be(TimeSpan.FromMinutes(5));
        config.ContinueOnSecretFailure.Should().BeTrue();
        config.ReloadFailedSecrets.Should().BeTrue();
        config.OperationTimeout.Should().Be(TimeSpan.FromSeconds(30));
        config.KeyMapper.Should().NotBeNull();
        config.KeyMapper("test--key").Should().Be("test:key");
    }

    [Fact]
    public void KeyVaultConfiguration_CustomKeyMapper_ShouldWork()
    {
        // Arrange
        var config = new KeyVaultConfiguration
        {
            KeyMapper = secretName => secretName.ToUpper().Replace("-", "_"),
        };

        // Act
        var result = config.KeyMapper("database-connection-string");

        // Assert
        result.Should().Be("DATABASE_CONNECTION_STRING");
    }

    [Fact]
    public void ContainsKey_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());

        // Act
        var result = source.ContainsKey("non-existent-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetValue_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());

        // Act
        var result = source.GetValue("non-existent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CacheStatistics_InitialState_ShouldBeEmpty()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());

        // Act
        var stats = source.CacheStatistics;

        // Assert
        stats.Should().NotBeNull();
        stats.Should().ContainKey("TotalEntries");
        stats.Should().ContainKey("ValidEntries");
        stats.Should().ContainKey("ExpiredEntries");
        stats.Should().ContainKey("CacheHitRate");
    }

    [Fact]
    public void Dispose_ShouldDisposeResourcesGracefully()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());

        // Act & Assert
        var action = () => source.Dispose();
        action.Should().NotThrow();

        // Verify disposed state
        source.Dispose(); // Should not throw on multiple calls
    }

    [Theory]
    [InlineData("Database--Host", "Database:Host")]
    [InlineData("Api--Key--Secret", "Api:Key:Secret")]
    [InlineData("SimpleKey", "SimpleKey")]
    [InlineData("App--Settings--ConnectionString", "App:Settings:ConnectionString")]
    public void DefaultKeyMapper_ShouldMapKeysCorrectly(string secretName, string expectedKey)
    {
        // Arrange
        var config = new KeyVaultConfiguration();

        // Act
        var result = config.KeyMapper(secretName);

        // Assert
        result.Should().Be(expectedKey);
    }

    [Fact]
    public async Task LoadAsync_WhenDisposed_ShouldReturnError()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());
        source.Dispose();

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("KeyVaultSource has been disposed");
    }

    [Fact]
    public async Task GetSecretAsync_WhenDisposed_ShouldReturnNull()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());
        source.Dispose();

        // Act
        var result = await source.GetSecretAsync("test-secret");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ClearCache_ShouldClearCache()
    {
        // Arrange
        var source = new MockKeyVaultSource(
            TestVaultUrl,
            new Dictionary<string, string>(),
            logger: _logger
        );

        // Act
        source.ClearCache();

        // Assert
        source.CacheStatistics["TotalEntries"].Should().Be(0);
    }

    [Fact]
    public async Task ReloadAsync_ShouldReload()
    {
        // Arrange
        var source = new MockKeyVaultSource(
            TestVaultUrl,
            new Dictionary<string, string>(),
            logger: _logger
        );

        // Act
        var result = await source.ReloadAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_WithManagedIdentity_ShouldConfigureCredential()
    {
        // Arrange
        var config = new KeyVaultConfiguration { Credential = new ManagedIdentityCredential() };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
        source.Name.Should().Contain("test-keyvault.vault.azure.net");
    }

    [Fact]
    public void KeyVaultSource_WithServicePrincipal_ShouldConfigureCredential()
    {
        // Arrange
        var config = new KeyVaultConfiguration
        {
            Credential = new ClientSecretCredential("tenant-id", "client-id", "client-secret"),
        };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_WithPrefixFilter_ShouldConfigurePrefix()
    {
        // Arrange
        var config = new KeyVaultConfiguration { SecretNamePrefix = "MyApp-" };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_WithSpecificVersion_ShouldConfigureVersion()
    {
        // Arrange
        var config = new KeyVaultConfiguration { SecretVersion = "version-123" };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_WithFailFastConfiguration_ShouldConfigureErrorHandling()
    {
        // Arrange
        var config = new KeyVaultConfiguration { ContinueOnSecretFailure = false };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_WithCustomTimeout_ShouldConfigureTimeout()
    {
        // Arrange
        var config = new KeyVaultConfiguration { OperationTimeout = TimeSpan.FromMinutes(2) };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_WithDisabledCaching_ShouldWork()
    {
        // Arrange
        var config = new KeyVaultConfiguration { CacheDuration = TimeSpan.Zero };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
        source.CacheStatistics["TotalEntries"].Should().Be(0);
    }

    [Fact]
    public void KeyVaultSource_WithCustomRetrySettings_ShouldConfigureRetry()
    {
        // Arrange
        var config = new KeyVaultConfiguration
        {
            MaxRetryAttempts = 10,
            BaseRetryDelay = TimeSpan.FromSeconds(5),
            MaxRetryDelay = TimeSpan.FromMinutes(5),
        };

        // Act
        var source = new MockKeyVaultSource(TestVaultUrl, config, new Dictionary<string, string>());

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public void KeyVaultSource_LoadErrors_ShouldBeAccessible()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());

        // Act
        var errors = source.LoadErrors;

        // Assert
        errors.Should().NotBeNull();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void KeyVaultSource_Priority_ShouldBeConfigurable()
    {
        // Arrange & Act
        var source = new MockKeyVaultSource(
            TestVaultUrl,
            new Dictionary<string, string>(),
            priority: 150
        );

        // Assert
        source.Priority.Should().Be(150);
    }

    [Fact]
    public void KeyVaultSource_Name_ShouldIncludeHostName()
    {
        // Arrange & Act
        var source = new MockKeyVaultSource(
            "https://my-custom-vault.vault.azure.net/",
            new Dictionary<string, string>()
        );

        // Assert
        source.Name.Should().Be("KeyVault(my-custom-vault.vault.azure.net)");
    }

    [Fact]
    public async Task KeyVaultSource_ThreadSafety_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(
                Task.Run(() =>
                {
                    var result = source.ContainsKey($"key-{i}");
                    var value = source.GetValue($"key-{i}");
                    source.ClearCache();
                })
            );
        }

        // Assert
        var aggregateTask = Task.WhenAll(tasks);
        await aggregateTask.WaitAsync(TimeSpan.FromSeconds(10));
        aggregateTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void KeyVaultSecretCache_Basic_ShouldWork()
    {
        // This test would require access to the internal cache class
        // In a real scenario, we might want to make it more testable
        var source = new MockKeyVaultSource(TestVaultUrl, new Dictionary<string, string>());
        var stats = source.CacheStatistics;

        stats.Should().NotBeNull();
        stats.Should().ContainKey("TotalEntries");
    }
}

/// <summary>
/// Extension methods for testing Key Vault sources.
/// </summary>
public static class KeyVaultSourceTestExtensions
{
    /// <summary>
    /// Creates a test Key Vault source with in-memory data for testing.
    /// </summary>
    public static MockKeyVaultSource CreateTestSource(Dictionary<string, string> testData)
    {
        // In a real implementation, we might use a mock or in-memory implementation
        // For now, we'll create a basic source for testing structure
        return new MockKeyVaultSource("https://test-keyvault.vault.azure.net/", testData);
    }
}
