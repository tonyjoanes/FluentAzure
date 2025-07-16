using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from environment variables.
/// </summary>
public class EnvironmentSource : IConfigurationSource
{
    private readonly int _priority;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentSource"/> class.
    /// </summary>
    /// <param name="priority">The priority of this configuration source.</param>
    public EnvironmentSource(int priority = 100)
    {
        _priority = priority;
    }

    /// <inheritdoc />
    public string Name => "Environment";

    /// <inheritdoc />
    public int Priority => _priority;

    /// <inheritdoc />
    public bool SupportsHotReload => false;

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <inheritdoc />
    public Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        var values = new Dictionary<string, string>();

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                values[key] = value;
            }
        }

        return Task.FromResult(Result<Dictionary<string, string>>.Success(values));
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return Environment.GetEnvironmentVariable(key) != null;
    }

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    /// <inheritdoc />
    public Task<Result<Dictionary<string, string>>> ReloadAsync()
    {
        return LoadAsync();
    }
}
