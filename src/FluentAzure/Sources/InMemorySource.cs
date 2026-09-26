using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from an in-memory dictionary. The dictionary is not copied, so later
/// changes to it are visible; keys are matched case-insensitively whatever the dictionary's own comparer.
/// </summary>
public class InMemorySource : IConfigurationSource
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySource"/> class.
    /// </summary>
    /// <param name="values">The configuration values.</param>
    /// <param name="priority">The priority of this source; higher priorities override lower ones.</param>
    public InMemorySource(Dictionary<string, string> values, int priority = 1000)
    {
        _values = values ?? new Dictionary<string, string>();
        Priority = priority;
    }

    /// <inheritdoc />
    public string Name => "InMemory";

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        // Copy key by key: a case-insensitive copy of a case-sensitive dictionary can meet the same key twice
        var copy = new Dictionary<string, string>(_values.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _values)
        {
            copy[kvp.Key] = kvp.Value;
        }

        return Task.FromResult(Result<Dictionary<string, string>>.Success(copy));
    }

    /// <inheritdoc />
    public bool ContainsKey(string key) => TryGetValue(key, out _);

    /// <inheritdoc />
    public string? GetValue(string key) => TryGetValue(key, out var value) ? value : null;

    private bool TryGetValue(string key, out string? value)
    {
        if (_values.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var kvp in _values)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
