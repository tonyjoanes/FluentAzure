using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using FluentAzure.Core;

namespace FluentAzure;

/// <summary>
/// Configuration source that loads values from Azure Key Vault.
/// </summary>
public class KeyVaultSource : IConfigurationSource
{
    private readonly string _vaultUrl;
    private readonly SecretClient _client;
    private Dictionary<string, string>? _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultSource"/> class.
    /// </summary>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    public KeyVaultSource(string vaultUrl, int priority = 200)
    {
        _vaultUrl = vaultUrl ?? throw new ArgumentNullException(nameof(vaultUrl));
        Priority = priority;
        _client = new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
    }

    /// <inheritdoc />
    public string Name => $"KeyVault({new Uri(_vaultUrl).Host})";

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        try
        {
            _values = new Dictionary<string, string>();

            // Get all secrets from the Key Vault
            var secrets = _client.GetPropertiesOfSecretsAsync();

            await foreach (var secretProperties in secrets)
            {
                try
                {
                    var secret = await _client.GetSecretAsync(secretProperties.Name);
                    if (secret?.Value?.Value != null)
                    {
                        // Convert Key Vault secret names to configuration keys
                        // Replace '--' with ':' for hierarchical configuration
                        var configKey = secretProperties.Name.Replace("--", ":");
                        _values[configKey] = secret.Value.Value;
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other secrets
                    // This allows partial success when some secrets are inaccessible
                    return Result<Dictionary<string, string>>.Error($"Failed to load secret '{secretProperties.Name}' from Key Vault: {ex.Message}");
                }
            }

            return Result<Dictionary<string, string>>.Success(_values);
        }
        catch (Exception ex)
        {
            return Result<Dictionary<string, string>>.Error($"Failed to load secrets from Key Vault '{_vaultUrl}': {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return _values?.ContainsKey(key) ?? false;
    }

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        return _values?.TryGetValue(key, out var value) == true ? value : null;
    }
}
