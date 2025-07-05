using FluentAzure.Core;

namespace FluentAzure;

/// <summary>
/// Defines a configuration source that can provide configuration values.
/// </summary>
public interface IConfigurationSource
{
    /// <summary>
    /// Gets the name of the configuration source (e.g., "Environment", "KeyVault", "JsonFile").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this configuration source. Higher priority sources override lower priority ones.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Asynchronously loads configuration values from the source.
    /// </summary>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the loaded configuration values.</returns>
    Task<Result<Dictionary<string, string>>> LoadAsync();

    /// <summary>
    /// Determines if this source contains a configuration value for the specified key.
    /// </summary>
    /// <param name="key">The configuration key to check.</param>
    /// <returns>True if the key exists in this source; otherwise, false.</returns>
    bool ContainsKey(string key);

    /// <summary>
    /// Gets the configuration value for the specified key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value if found; otherwise, null.</returns>
    string? GetValue(string key);
}
