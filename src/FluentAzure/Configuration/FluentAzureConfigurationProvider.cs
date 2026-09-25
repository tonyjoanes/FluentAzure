using Microsoft.Extensions.Configuration;

namespace FluentAzure.Configuration;

/// <summary>
/// Microsoft.Extensions.Configuration provider backed by a FluentAzure pipeline.
/// </summary>
/// <remarks>
/// The initial load fails fast with <see cref="FluentAzureConfigurationException"/> if the pipeline
/// reports errors. Later reloads that fail keep the last good values and report through
/// <see cref="FluentAzureConfigurationSource.OnReloadError"/>, so a transient Key Vault outage
/// never empties a running application's configuration.
/// </remarks>
public sealed class FluentAzureConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly FluentAzureConfigurationSource _source;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private Task? _reloadLoop;
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentAzureConfigurationProvider"/> class.
    /// </summary>
    /// <param name="source">The configuration source describing the pipeline.</param>
    public FluentAzureConfigurationProvider(FluentAzureConfigurationSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Gets the errors from the most recent failed reload, or an empty list if it succeeded.
    /// </summary>
    public IReadOnlyList<string> LastReloadErrors { get; private set; } = Array.Empty<string>();

    /// <inheritdoc />
    public override void Load()
    {
        if (!_loaded && _source.PreloadedValues is { } preloaded)
        {
            Data = ToConfigurationData(preloaded);
            _source.PreloadedValues = null;
            _loaded = true;
        }
        else
        {
            // Configuration providers load synchronously by design; run the async pipeline on the
            // thread pool so awaits never try to resume on a blocked synchronization context
            Task.Run(() => LoadCoreAsync(notifyOnChange: false)).GetAwaiter().GetResult();
        }

        StartReloadLoop();
    }

    /// <summary>
    /// Re-runs the pipeline, reloading cached sources such as Key Vault. If the values changed,
    /// the provider's change token fires. On failure the previous values are kept.
    /// </summary>
    /// <returns>True if the reload succeeded; otherwise, false.</returns>
    public Task<bool> ReloadAsync() => LoadCoreAsync(notifyOnChange: true);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return;
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    private async Task<bool> LoadCoreAsync(bool notifyOnChange)
    {
        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await _source
                .Pipeline.BuildAsync(reloadSources: _loaded)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                if (!_loaded)
                {
                    throw new FluentAzureConfigurationException(result.Errors);
                }

                LastReloadErrors = result.Errors;
                _source.OnReloadError?.Invoke(result.Errors);
                return false;
            }

            LastReloadErrors = Array.Empty<string>();
            var newData = ToConfigurationData(result.Value);
            var changed = !_loaded || !HasSameValues(Data, newData);
            Data = newData;
            _loaded = true;

            if (notifyOnChange && changed)
            {
                OnReload();
            }

            return true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private void StartReloadLoop()
    {
        if (
            _reloadLoop != null
            || _disposeCts.IsCancellationRequested
            || _source.ReloadInterval is not { } interval
            || interval <= TimeSpan.Zero
        )
        {
            return;
        }

        var token = _disposeCts.Token;
        _reloadLoop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        await ReloadAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Never let a reload failure kill the loop; keep the last good values
                        LastReloadErrors = new[] { ex.Message };
                        _source.OnReloadError?.Invoke(LastReloadErrors);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Provider disposed
            }
        });
    }

    /// <summary>
    /// Converts pipeline output into configuration data, translating the "__" hierarchy separator
    /// used by environment variables and FluentAzure's JSON flattening into the standard ":".
    /// An explicit ":" key always wins over one derived from "__".
    /// </summary>
    private static Dictionary<string, string?> ToConfigurationData(
        IReadOnlyDictionary<string, string> values
    )
    {
        var data = new Dictionary<string, string?>(values.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in values)
        {
            if (!kvp.Key.Contains("__"))
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in values)
        {
            if (kvp.Key.Contains("__"))
            {
                data.TryAdd(kvp.Key.Replace("__", ":"), kvp.Value);
            }
        }

        return data;
    }

    private static bool HasSameValues(
        IDictionary<string, string?> current,
        IDictionary<string, string?> updated
    )
    {
        if (current.Count != updated.Count)
        {
            return false;
        }

        foreach (var kvp in updated)
        {
            if (!current.TryGetValue(kvp.Key, out var value) || !string.Equals(value, kvp.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
