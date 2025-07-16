using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from an in-memory dictionary.
/// </summary>
public class InMemorySource : IConfigurationSource
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySource"/> class.
    /// </summary>
    /// <param name="values">The configuration values.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    public InMemorySource(Dictionary<string, string> values, int priority = 0)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
        Priority = priority;
    }

    /// <inheritdoc />
    public string Name => "InMemory";

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public bool SupportsHotReload => false;

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <inheritdoc />
    public Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        return Task.FromResult(Result<Dictionary<string, string>>.Success(_values));
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return _values.ContainsKey(key);
    }

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    /// <inheritdoc />
    public Task<Result<Dictionary<string, string>>> ReloadAsync()
    {
        return LoadAsync();
    }
}
