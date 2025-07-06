using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FluentAzure.Core;
using Microsoft.Extensions.Logging;
using Polly;

namespace FluentAzure.Sources;

/// <summary>
/// Enhanced configuration source that loads values from Azure Key Vault with retry logic,
/// caching, secret versioning, and advanced error handling.
/// </summary>
public class KeyVaultSource : IConfigurationSource, IDisposable
{
    private readonly string _vaultUrl;
    private readonly SecretClient _client;
    private readonly KeyVaultConfiguration _configuration;
    private readonly KeyVaultSecretCache _cache;
    private readonly ILogger? _logger;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly ConcurrentDictionary<string, string> _values = new();
    private readonly List<string> _loadErrors = new();
    private volatile bool _isLoaded;
    private readonly object _loadLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultSource"/> class.
    /// </summary>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="logger">Optional logger for debugging and monitoring.</param>
    public KeyVaultSource(string vaultUrl, int priority = 200, ILogger? logger = null)
        : this(vaultUrl, new KeyVaultConfiguration(), priority, logger) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultSource"/> class with custom configuration.
    /// </summary>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="configuration">The Key Vault configuration options.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="logger">Optional logger for debugging and monitoring.</param>
    public KeyVaultSource(
        string vaultUrl,
        KeyVaultConfiguration configuration,
        int priority = 200,
        ILogger? logger = null
    )
    {
        _vaultUrl = vaultUrl ?? throw new ArgumentNullException(nameof(vaultUrl));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        Priority = priority;

        // Initialize the Secret Client with proper credentials and options
        var credential = _configuration.Credential ?? new DefaultAzureCredential();
        var options = new SecretClientOptions
        {
            Retry =
            {
                Delay = _configuration.BaseRetryDelay,
                MaxDelay = _configuration.MaxRetryDelay,
                MaxRetries = _configuration.MaxRetryAttempts,
            },
        };

        _client = new SecretClient(new Uri(vaultUrl), credential, options);

        // Initialize cache
        _cache = new KeyVaultSecretCache(_configuration.CacheDuration, _logger);

        // Initialize retry pipeline with exponential backoff
        _retryPipeline = CreateRetryPipeline();

        _logger?.LogInformation("KeyVaultSource initialized for vault: {VaultUrl}", _vaultUrl);
    }

    /// <inheritdoc />
    public string Name => $"KeyVault({new Uri(_vaultUrl).Host})";

    /// <inheritdoc />
    public int Priority { get; }

    /// <summary>
    /// Gets the cache statistics for monitoring purposes.
    /// </summary>
    public Dictionary<string, object> CacheStatistics => _cache.GetStatistics();

    /// <summary>
    /// Gets the errors that occurred during the last load operation.
    /// </summary>
    public IReadOnlyList<string> LoadErrors => _loadErrors.AsReadOnly();

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        if (_disposed)
        {
            return Result<Dictionary<string, string>>.Error("KeyVaultSource has been disposed");
        }

        // Thread-safe loading
        if (_isLoaded)
        {
            return Result<Dictionary<string, string>>.Success(
                _values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            );
        }

        lock (_loadLock)
        {
            if (_isLoaded)
            {
                return Result<Dictionary<string, string>>.Success(
                    _values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                );
            }
        }

        try
        {
            _logger?.LogInformation("Loading secrets from Key Vault: {VaultUrl}", _vaultUrl);
            _loadErrors.Clear();

            var loadedSecrets = new Dictionary<string, string>();
            var secretLoadTasks = new List<Task<SecretLoadResult>>();

            // Get all secret properties first
            var secretProperties = await _retryPipeline.ExecuteAsync(async _ =>
            {
                var secrets = new List<SecretProperties>();
                var secretsAsync = _client.GetPropertiesOfSecretsAsync();

                await foreach (var secret in secretsAsync)
                {
                    // Apply prefix filter if specified
                    if (
                        !string.IsNullOrEmpty(_configuration.SecretNamePrefix)
                        && !secret.Name.StartsWith(
                            _configuration.SecretNamePrefix,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }

                    secrets.Add(secret);
                }

                return secrets;
            });

            // Load secrets in parallel with retry logic
            foreach (var secretProperty in secretProperties)
            {
                var task = LoadSecretAsync(secretProperty);
                secretLoadTasks.Add(task);
            }

            var results = await Task.WhenAll(secretLoadTasks);

            // Process results
            foreach (var result in results)
            {
                if (result.IsSuccess)
                {
                    var configKey = _configuration.KeyMapper(result.SecretName);
                    loadedSecrets[configKey] = result.Value!;
                    _values[configKey] = result.Value!;

                    // Cache the secret
                    _cache.Set(result.SecretName, result.Value!, _configuration.CacheDuration);
                }
                else
                {
                    _loadErrors.Add(result.Error!);
                    _logger?.LogWarning(
                        "Failed to load secret '{SecretName}': {Error}",
                        result.SecretName,
                        result.Error
                    );

                    if (!_configuration.ContinueOnSecretFailure)
                    {
                        return Result<Dictionary<string, string>>.Error(_loadErrors);
                    }
                }
            }

            _isLoaded = true;

            var message = $"Successfully loaded {loadedSecrets.Count} secrets from Key Vault";
            if (_loadErrors.Count > 0)
            {
                message += $" (with {_loadErrors.Count} errors)";
            }

            _logger?.LogInformation("{Message}", message);

            return _loadErrors.Count == 0 || _configuration.ContinueOnSecretFailure
                ? Result<Dictionary<string, string>>.Success(loadedSecrets)
                : Result<Dictionary<string, string>>.Error(_loadErrors);
        }
        catch (Exception ex)
        {
            var error = $"Failed to load secrets from Key Vault '{_vaultUrl}': {ex.Message}";
            _logger?.LogError(ex, "Key Vault load operation failed");
            return Result<Dictionary<string, string>>.Error(error);
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
        // First check in-memory values
        if (_values.TryGetValue(key, out var value))
        {
            return value;
        }

        // Check cache for dynamically loaded secrets
        var originalKey = GetOriginalSecretName(key);
        if (_cache.TryGetValue(originalKey, out var cachedValue))
        {
            return cachedValue;
        }

        return null;
    }

    /// <summary>
    /// Reloads secrets from Key Vault, bypassing the cache.
    /// </summary>
    /// <returns>A task that represents the asynchronous reload operation.</returns>
    public async Task<Result<Dictionary<string, string>>> ReloadAsync()
    {
        _logger?.LogInformation("Reloading secrets from Key Vault: {VaultUrl}", _vaultUrl);

        _isLoaded = false;
        _values.Clear();
        _cache.Clear();

        return await LoadAsync();
    }

    /// <summary>
    /// Gets a specific secret by name with optional version.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="version">The version of the secret. If null, gets the latest version.</param>
    /// <returns>The secret value if found; otherwise, null.</returns>
    public async Task<string?> GetSecretAsync(string secretName, string? version = null)
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            var effectiveVersion = version ?? _configuration.SecretVersion;
            var cacheKey =
                effectiveVersion != null ? $"{secretName}:{effectiveVersion}" : secretName;

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out var cachedValue))
            {
                return cachedValue;
            }

            // Load from Key Vault with retry
            var secretResponse = await _retryPipeline.ExecuteAsync(async _ =>
            {
                return effectiveVersion != null
                    ? await _client.GetSecretAsync(secretName, effectiveVersion)
                    : await _client.GetSecretAsync(secretName);
            });

            if (secretResponse?.Value?.Value != null)
            {
                // Cache the result
                _cache.Set(cacheKey, secretResponse.Value.Value, _configuration.CacheDuration);
                return secretResponse.Value.Value;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get secret '{SecretName}' from Key Vault", secretName);
            return null;
        }
    }

    /// <summary>
    /// Clears the secret cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger?.LogInformation("Key Vault cache cleared");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _cache.Clear();
            _disposed = true;
            _logger?.LogInformation("KeyVaultSource disposed");
        }
    }

    private async Task<SecretLoadResult> LoadSecretAsync(SecretProperties secretProperties)
    {
        try
        {
            var secret = await _retryPipeline.ExecuteAsync(async _ =>
            {
                return _configuration.SecretVersion != null
                    ? await _client.GetSecretAsync(
                        secretProperties.Name,
                        _configuration.SecretVersion
                    )
                    : await _client.GetSecretAsync(secretProperties.Name);
            });

            if (secret?.Value?.Value != null)
            {
                return new SecretLoadResult
                {
                    SecretName = secretProperties.Name,
                    Value = secret.Value.Value,
                    IsSuccess = true,
                };
            }

            return new SecretLoadResult
            {
                SecretName = secretProperties.Name,
                Error = "Secret value is null or empty",
                IsSuccess = false,
            };
        }
        catch (Exception ex)
        {
            return new SecretLoadResult
            {
                SecretName = secretProperties.Name,
                Error = ex.Message,
                IsSuccess = false,
            };
        }
    }

    private ResiliencePipeline CreateRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(
                new Polly.Retry.RetryStrategyOptions
                {
                    MaxRetryAttempts = _configuration.MaxRetryAttempts,
                    Delay = _configuration.BaseRetryDelay,
                    MaxDelay = _configuration.MaxRetryDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        _logger?.LogWarning(
                            "Retrying Key Vault operation (attempt {AttemptNumber}): {Exception}",
                            args.AttemptNumber,
                            args.Outcome.Exception?.Message
                        );
                        return ValueTask.CompletedTask;
                    },
                }
            )
            .AddTimeout(_configuration.OperationTimeout)
            .Build();
    }

    private string GetOriginalSecretName(string configKey)
    {
        // Reverse the key mapping to find the original secret name
        return configKey.Replace(":", "--");
    }

    private record SecretLoadResult
    {
        public string SecretName { get; init; } = string.Empty;
        public string? Value { get; init; }
        public string? Error { get; init; }
        public bool IsSuccess { get; init; }
    }
}
