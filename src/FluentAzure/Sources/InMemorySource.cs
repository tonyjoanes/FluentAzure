using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from an in-memory dictionary.
/// </summary>
public class InMemorySource : IConfigurationSource
{
    private readonly Dictionary<string, string> _values;
    public string Name => "InMemory";
    public int Priority { get; }

    public InMemorySource(Dictionary<string, string> values, int priority = 1000)
    {
        _values = values ?? new Dictionary<string, string>();
        Priority = priority;
    }

    public Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        return Task.FromResult(
            Result<Dictionary<string, string>>.Success(new Dictionary<string, string>(_values))
        );
    }

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public string? GetValue(string key) => _values.TryGetValue(key, out var value) ? value : null;
}
