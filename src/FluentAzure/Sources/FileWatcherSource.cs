using FluentAzure.Core;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads values from JSON files with hot reload support.
/// </summary>
public class FileWatcherSource : JsonFileSource, IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly int _debounceMs;
    private readonly object _lockObject = new object();
    private bool _disposed;
    private bool _isLoaded;
    private CancellationTokenSource? _debounceCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWatcherSource"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON configuration file.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="optional">Whether the file is optional. If false, missing file will cause an error.</param>
    /// <param name="debounceMs">Debounce time in milliseconds to prevent rapid-fire updates.</param>
    public FileWatcherSource(
        string filePath,
        int priority = 50,
        bool optional = false,
        int debounceMs = 500
    )
        : base(filePath, priority, optional)
    {
        _debounceMs = Math.Max(0, debounceMs);
    }

    /// <inheritdoc />
    public override bool SupportsHotReload => true;

    /// <inheritdoc />
    public override async Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        var result = await base.LoadAsync();

        if (result.IsSuccess)
        {
            _isLoaded = true;
            // Create and enable file watching after successful load
            InitializeFileWatcher();
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<Dictionary<string, string>>> ReloadAsync()
    {
        // Temporarily disable watching to prevent recursive events
        DisableFileWatcher();

        try
        {
            var result = await base.ReloadAsync();

            if (result.IsSuccess)
            {
                // Re-enable watching
                EnableFileWatcher();
            }

            return result;
        }
        catch
        {
            // Re-enable watching even if reload fails
            EnableFileWatcher();
            throw;
        }
    }

    private void InitializeFileWatcher()
    {
        if (_disposed || _watcher != null)
            return;

        try
        {
            var directory = Path.GetDirectoryName(_filePath) ?? ".";
            var filename = Path.GetFileName(_filePath);

            // Check if directory exists before creating watcher
            if (!Directory.Exists(directory))
            {
                return;
            }

            _watcher = new FileSystemWatcher(directory, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = false, // Will be enabled after creation
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;

            // Enable watching
            EnableFileWatcher();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - file watching should be resilient
            System.Diagnostics.Debug.WriteLine($"Error initializing file watcher: {ex.Message}");
            _watcher = null;
        }
    }

    private void EnableFileWatcher()
    {
        if (_watcher != null && !_disposed)
        {
            try
            {
                _watcher.EnableRaisingEvents = true;
            }
            catch (ObjectDisposedException)
            {
                // Watcher was disposed, ignore
            }
            catch (FileNotFoundException)
            {
                // Directory was deleted, ignore
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted, ignore
            }
            catch (Exception ex)
            {
                // Log other errors but don't throw
                System.Diagnostics.Debug.WriteLine($"Error enabling file watcher: {ex.Message}");
            }
        }
    }

    private void DisableFileWatcher()
    {
        if (_watcher != null && !_disposed)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
            }
            catch (ObjectDisposedException)
            {
                // Watcher was disposed, ignore
            }
            catch (FileNotFoundException)
            {
                // Directory was deleted, ignore
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted, ignore
            }
            catch (Exception ex)
            {
                // Log other errors but don't throw
                System.Diagnostics.Debug.WriteLine($"Error disabling file watcher: {ex.Message}");
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lockObject)
        {
            if (_disposed || !_isLoaded)
                return;

            // Cancel any pending debounce
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            // Start a new debounce task
            _ = DebouncedReloadAsync(token);
        }
    }

    private async Task DebouncedReloadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounceMs, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            // Temporarily disable watching to prevent recursive events
            DisableFileWatcher();

            try
            {
                // Check if file still exists before trying to reload
                if (!File.Exists(_filePath))
                {
                    // File was deleted, don't try to reload
                    return;
                }

                var previousValues = new Dictionary<string, string>(
                    _values ?? new Dictionary<string, string>()
                );
                var result = await base.ReloadAsync();

                if (result.IsSuccess)
                {
                    _values = result.Value;

                    // Raise configuration changed event
                    OnConfigurationChanged(
                        new ConfigurationChangedEventArgs(previousValues, _values, this)
                    );
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - file watching should be resilient
                System.Diagnostics.Debug.WriteLine($"Error reloading configuration file: {ex.Message}");
            }
            finally
            {
                lock (_lockObject)
                {
                    if (!_disposed)
                    {
                        EnableFileWatcher();
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
    }

    /// <summary>
    /// Raises the ConfigurationChanged event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnConfigurationChanged(ConfigurationChangedEventArgs e)
    {
        base.OnConfigurationChanged(e);
    }

    /// <summary>
    /// Disposes the file watcher and releases resources.
    /// </summary>
    public void Dispose()
    {
        lock (_lockObject)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        try
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _watcher?.Dispose();
            _watcher = null;
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}
