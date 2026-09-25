using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from environment variables.
/// Keys are case-insensitive, and a variable containing the standard .NET hierarchy separator
/// "__" (e.g. <c>ConnectionStrings__Default</c>) is also exposed under its ":" form
/// (<c>ConnectionStrings:Default</c>), as App Service, Container Apps and Functions expect.
/// </summary>
public class EnvironmentSource : IReloadableConfigurationSource
{
    private volatile Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentSource"/> class.
    /// </summary>
    /// <param name="priority">The priority of this configuration source.</param>
    public EnvironmentSource(int priority = 100)
    {
        Priority = priority;
        _values = LoadEnvironmentVariables();
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

    /// <summary>
    /// Re-reads the process environment, picking up variables changed since this source was created.
    /// </summary>
    /// <returns>A task that represents the asynchronous reload operation. The task result contains the reloaded environment variables.</returns>
    public Task<Result<Dictionary<string, string>>> ReloadAsync()
    {
        try
        {
            _values = LoadEnvironmentVariables();
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

    private static Dictionary<string, string> LoadEnvironmentVariables()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var key in environmentVariables.Keys)
            {
                var keyString = key.ToString();
                var value = environmentVariables[key]?.ToString();

                if (!string.IsNullOrEmpty(keyString) && value != null)
                {
                    values[keyString] = value;
                }
            }

            // Add ":" aliases after all raw variables so an explicitly set "A:B" is never overwritten
            foreach (var kvp in values.Where(kvp => kvp.Key.Contains("__")).ToList())
            {
                values.TryAdd(kvp.Key.Replace("__", ":"), kvp.Value);
            }

            return values;
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
