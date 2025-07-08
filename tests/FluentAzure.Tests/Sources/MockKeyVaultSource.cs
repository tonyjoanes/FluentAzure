using FluentAzure.Core;
using FluentAzure.Sources;
using Microsoft.Extensions.Logging;

namespace FluentAzure.Tests.Sources;

/// <summary>
/// Mock implementation of KeyVaultSource for testing purposes.
/// This avoids creating real Azure connections during unit tests.
/// </summary>
public class MockKeyVaultSource : KeyVaultSource
{
    private readonly Dictionary<string, string> _mockSecrets;
    private readonly bool _shouldFail;

    public MockKeyVaultSource(
        string vaultUrl,
        Dictionary<string, string> mockSecrets,
        bool shouldFail = false,
        int priority = 200,
        ILogger? logger = null
    )
        : base(vaultUrl, new KeyVaultConfiguration(), priority, logger, false)
    {
        _mockSecrets = mockSecrets ?? new Dictionary<string, string>();
        _shouldFail = shouldFail;
    }

    public MockKeyVaultSource(
        string vaultUrl,
        KeyVaultConfiguration configuration,
        Dictionary<string, string> mockSecrets,
        bool shouldFail = false,
        int priority = 200,
        ILogger? logger = null
    )
        : base(vaultUrl, configuration, priority, logger, false)
    {
        _mockSecrets = mockSecrets ?? new Dictionary<string, string>();
        _shouldFail = shouldFail;
    }

    /// <summary>
    /// Override the LoadAsync method to return mock data instead of connecting to Azure
    /// </summary>
    public override async Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        if (_disposed)
        {
            return Result<Dictionary<string, string>>.Error("KeyVaultSource has been disposed");
        }

        if (_shouldFail)
        {
            return Result<Dictionary<string, string>>.Error("Mock Key Vault failure");
        }

        // Simulate async operation
        await Task.Delay(1);

        return Result<Dictionary<string, string>>.Success(_mockSecrets);
    }

    /// <summary>
    /// Override GetSecretAsync to return mock data
    /// </summary>
    public override async Task<string?> GetSecretAsync(string secretName, string? version = null)
    {
        if (_shouldFail)
        {
            return null;
        }

        // Simulate async operation
        await Task.Delay(1);

        return _mockSecrets.TryGetValue(secretName, out var value) ? value : null;
    }

    /// <summary>
    /// Override ContainsKey to check mock data
    /// </summary>
    public override bool ContainsKey(string key)
    {
        return _mockSecrets.ContainsKey(key);
    }

    /// <summary>
    /// Override GetValue to return mock data
    /// </summary>
    public override string? GetValue(string key)
    {
        return _mockSecrets.TryGetValue(key, out var value) ? value : null;
    }
}
