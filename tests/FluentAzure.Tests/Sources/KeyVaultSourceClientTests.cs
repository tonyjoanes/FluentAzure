using FluentAssertions;
using FluentAzure.Sources;
using FluentAzure.Tests.TestDoubles;

namespace FluentAzure.Tests.Sources;

/// <summary>
/// Tests for <see cref="KeyVaultSource"/>'s real loading logic, using an injected in-memory <see cref="Azure.Security.KeyVault.Secrets.SecretClient"/>.
/// </summary>
public class KeyVaultSourceClientTests
{
    [Fact]
    public async Task LoadAsync_LoadsSecretsAndMapsDoubleDashToColon()
    {
        // Arrange
        var vault = new FakeVaultClient();
        vault.Add("Database--Password", "p@ss");
        vault.Add("ApiKey", "key");
        var source = new KeyVaultSource(vault);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new Dictionary<string, string> { ["Database:Password"] = "p@ss", ["ApiKey"] = "key" });
        source.GetValue("database:password").Should().Be("p@ss");
    }

    [Fact]
    public async Task LoadAsync_WithHangingRead_IsCancelledByOperationTimeout()
    {
        // Arrange: a read that only finishes if its cancellation token fires
        var vault = new FakeVaultClient { ReadDelay = TimeSpan.FromMinutes(10) };
        vault.Add("Slow", "value");
        var source = new KeyVaultSource(
            vault,
            new KeyVaultConfiguration
            {
                OperationTimeout = TimeSpan.FromMilliseconds(200),
                MaxRetryAttempts = 1,
                BaseRetryDelay = TimeSpan.FromMilliseconds(10),
            });

        // Act
        var load = source.LoadAsync();
        var completed = await Task.WhenAny(load, Task.Delay(TimeSpan.FromSeconds(15)));

        // Assert
        completed.Should().BeSameAs(load, "the operation timeout should cancel the Key Vault call");
        var result = await load;
        (result.IsFailure || !result.Value.ContainsKey("Slow")).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_SkipsDisabledExpiredAndNotYetActiveSecrets()
    {
        // Arrange
        var vault = new FakeVaultClient();
        vault.Add("Active", "yes");
        vault.Add("Disabled", "no", enabled: false);
        vault.Add("Expired", "no", expiresOn: DateTimeOffset.UtcNow.AddMinutes(-1));
        vault.Add("Future", "no", notBefore: DateTimeOffset.UtcNow.AddDays(1));
        var source = new KeyVaultSource(vault);

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value.Keys.Should().BeEquivalentTo("Active");
        vault.GetCalls.Should().Be(1, "inactive secrets should not even be read");
    }

    [Fact]
    public async Task LoadAsync_WithPrefix_LoadsOnlyMatchingSecrets()
    {
        // Arrange
        var vault = new FakeVaultClient();
        vault.Add("MyApp--Name", "demo");
        vault.Add("Other--Name", "other");
        var source = new KeyVaultSource(vault, new KeyVaultConfiguration { SecretNamePrefix = "MyApp--" });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value.Keys.Should().BeEquivalentTo("MyApp:Name");
    }

    [Fact]
    public async Task LoadAsync_BoundsConcurrentSecretReads()
    {
        // Arrange
        var vault = new FakeVaultClient { ReadDelay = TimeSpan.FromMilliseconds(20) };
        for (var i = 0; i < 30; i++)
        {
            vault.Add($"Secret{i}", $"value{i}");
        }

        var source = new KeyVaultSource(vault, new KeyVaultConfiguration { MaxConcurrentSecretLoads = 3 });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.Value.Should().HaveCount(30);
        vault.PeakConcurrentReads.Should().BeInRange(1, 3);
    }

    [Fact]
    public async Task LoadAsync_ConcurrentCallers_ListTheVaultOnlyOnce()
    {
        // Arrange
        var vault = new FakeVaultClient { ReadDelay = TimeSpan.FromMilliseconds(20) };
        vault.Add("Secret", "value");
        var source = new KeyVaultSource(vault);

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => source.LoadAsync()));

        // Assert
        results.Should().OnlyContain(r => r.IsSuccess && r.Value["Secret"] == "value");
        vault.ListCalls.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WhenASecretFailsAndContinueOnFailure_ReturnsTheRest()
    {
        // Arrange
        var vault = new FakeVaultClient();
        vault.Add("Good", "ok");
        vault.Add("Bad", "unused");
        vault.FailingSecrets.Add("Bad");
        var source = new KeyVaultSource(vault, FastRetries());

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Keys.Should().BeEquivalentTo("Good");
        source.LoadErrors.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAsync_WhenASecretFailsAndContinueOnFailureIsOff_Fails()
    {
        // Arrange
        var vault = new FakeVaultClient();
        vault.Add("Bad", "unused");
        vault.FailingSecrets.Add("Bad");
        var source = new KeyVaultSource(vault, new KeyVaultConfiguration
            {
                ContinueOnSecretFailure = false,
                BaseRetryDelay = TimeSpan.FromMilliseconds(1),
                MaxRetryDelay = TimeSpan.FromMilliseconds(5),
            });

        // Act
        var result = await source.LoadAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ReloadAsync_PicksUpRotatedSecrets()
    {
        // Arrange
        var vault = new FakeVaultClient();
        vault.Add("Secret", "v1");
        var source = new KeyVaultSource(vault);
        await source.LoadAsync();

        // Act
        vault.Add("Secret", "v2");
        var cached = await source.LoadAsync();
        var reloaded = await source.ReloadAsync();

        // Assert
        cached.Value["Secret"].Should().Be("v1");
        reloaded.Value["Secret"].Should().Be("v2");
        vault.ListCalls.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithClient_UsesVaultHostAsName()
    {
        // Arrange & Act
        var source = new KeyVaultSource(new FakeVaultClient("https://prod-vault.vault.azure.net/"));

        // Assert
        source.Name.Should().Be("KeyVault(prod-vault.vault.azure.net)");
        source.IsSensitive("anything").Should().BeTrue();
    }

    private static KeyVaultConfiguration FastRetries() =>
        new()
        {
            BaseRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(5),
        };
}
