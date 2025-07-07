using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from environment variables.
/// </summary>
public class EnvironmentSource : IConfigurationSource
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentSource"/> class.
    /// </summary>
    /// <param name="priority">The priority of this configuration source.</param>
    public EnvironmentSource(int priority = 100)
    {
        Priority = priority;
        _values = new Dictionary<string, string>();
        LoadEnvironmentVariables();
    }

    /// <inheritdoc />
    public string Name => "Environment";

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        try
        {
            return Task.FromResult(Result<Dictionary<string, string>>.Success(_values));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Result<Dictionary<string, string>>.Error(
                    $"Failed to load environment variables: {ex.Message}"
                )
            );
        }
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

    private void LoadEnvironmentVariables()
    {
        try
        {
            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var key in environmentVariables.Keys)
            {
                var keyString = key.ToString();
                var value = environmentVariables[key]?.ToString();

                if (!string.IsNullOrEmpty(keyString) && value != null)
                {
                    _values[keyString] = value;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail completely - we'll handle this in LoadAsync
            throw new InvalidOperationException(
                $"Failed to load environment variables: {ex.Message}",
                ex
            );
        }
    }
}
