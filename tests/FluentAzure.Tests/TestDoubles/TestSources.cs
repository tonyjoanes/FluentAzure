using FluentAzure.Core;

namespace FluentAzure.Tests.TestDoubles;

/// <summary>
/// A reloadable source with a configurable name whose values, failure and exception behaviour can be changed by a test.
/// </summary>
internal sealed class ControllableSource : IReloadableConfigurationSource
{
    public ControllableSource(string name, Dictionary<string, string>? values = null, int priority = 100)
    {
        Name = name;
        Values = values ?? new Dictionary<string, string>();
        Priority = priority;
    }

    public Dictionary<string, string> Values { get; }

    /// <summary>Gets or sets an error message the source reports instead of values.</summary>
    public string? FailWith { get; set; }

    /// <summary>Gets or sets an exception the source throws instead of returning a result.</summary>
    public Exception? ThrowWith { get; set; }

    public string Name { get; }

    public int Priority { get; }

    public Task<Result<Dictionary<string, string>>> LoadAsync() => Task.FromResult(Snapshot());

    public Task<Result<Dictionary<string, string>>> ReloadAsync() => Task.FromResult(Snapshot());

    public bool ContainsKey(string key) => Values.ContainsKey(key);

    public string? GetValue(string key) => Values.TryGetValue(key, out var value) ? value : null;

    private Result<Dictionary<string, string>> Snapshot()
    {
        if (ThrowWith is not null)
        {
            throw ThrowWith;
        }

        return FailWith is null
            ? Result<Dictionary<string, string>>.Success(new Dictionary<string, string>(Values))
            : Result<Dictionary<string, string>>.Error(FailWith);
    }
}
