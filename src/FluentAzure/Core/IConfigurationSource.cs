namespace FluentAzure.Core;

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

    /// <summary>
    /// Gets whether this configuration source supports hot reload.
    /// </summary>
    bool SupportsHotReload { get; }

    /// <summary>
    /// Event that is raised when configuration values change.
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Asynchronously reloads configuration values from the source.
    /// </summary>
    /// <returns>A task that represents the asynchronous reload operation.</returns>
    Task<Result<Dictionary<string, string>>> ReloadAsync();
}

/// <summary>
/// Event arguments for configuration change events.
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous configuration values.
    /// </summary>
    public Dictionary<string, string> PreviousValues { get; }

    /// <summary>
    /// Gets the new configuration values.
    /// </summary>
    public Dictionary<string, string> NewValues { get; }

    /// <summary>
    /// Gets the source that triggered the change.
    /// </summary>
    public IConfigurationSource Source { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousValues">The previous configuration values.</param>
    /// <param name="newValues">The new configuration values.</param>
    /// <param name="source">The source that triggered the change.</param>
    public ConfigurationChangedEventArgs(
        Dictionary<string, string> previousValues,
        Dictionary<string, string> newValues,
        IConfigurationSource source)
    {
        PreviousValues = previousValues ?? new Dictionary<string, string>();
        NewValues = newValues ?? new Dictionary<string, string>();
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
}
