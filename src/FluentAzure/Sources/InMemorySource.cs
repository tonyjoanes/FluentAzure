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
        return Task.FromResult(
            Result<Dictionary<string, string>>.Success(new Dictionary<string, string>(_values))
        );
    }

    /// <inheritdoc />
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <inheritdoc />
    public string? GetValue(string key) => _values.TryGetValue(key, out var value) ? value : null;
}
