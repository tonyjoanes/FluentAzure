using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Azure.Core;
using Azure.Identity;
using FluentAzure.Binding;
using FluentAzure.Extensions;
using FluentAzure.Sources;

namespace FluentAzure.Core;

/// <summary>
/// Fluent builder for creating configuration pipelines that can load configuration from multiple sources.
/// Configuration keys are case-insensitive, matching Microsoft.Extensions.Configuration.
/// </summary>
public class ConfigurationBuilder
{
    private readonly List<IConfigurationSource> _sources = new();
    private readonly Dictionary<string, object> _requiredKeys = new();
    private readonly Dictionary<string, object> _optionalKeys = new();
    private readonly List<
        Func<Dictionary<string, string>, Task<Result<Dictionary<string, string>>>>
    > _transformations = new();
    private readonly List<Func<Dictionary<string, string>, Result<string>>> _validations = new();
    private readonly PipelineCredential _credential = new();
    private readonly HashSet<string> _markedSensitiveKeys = new(StringComparer.OrdinalIgnoreCase);
    private volatile HashSet<string> _sourceSensitiveKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the credential used by Azure sources (Key Vault, App Configuration) in this pipeline that
    /// do not specify their own. Defaults to <see cref="DefaultAzureCredential"/>; set it with
    /// <see cref="UseCredential"/>, <see cref="UseManagedIdentity"/> or <see cref="UseWorkloadIdentity"/>.
    /// </summary>
    public TokenCredential Credential => _credential;

    /// <summary>
    /// Gets the sources added to this pipeline.
    /// </summary>
    internal IReadOnlyList<IConfigurationSource> Sources => _sources;

    /// <summary>
    /// Uses the given credential for all Azure sources in this pipeline that do not specify their own.
    /// Can be called before or after the sources are added.
    /// </summary>
    /// <param name="credential">The credential to use.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder UseCredential(TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credential.Configure(credential);
        return this;
    }

    /// <summary>
    /// Uses a managed identity for all Azure sources in this pipeline. This is the recommended identity
    /// for App Service, Functions, Container Apps and VMs: it is deterministic and never falls back to
    /// developer credentials the way <see cref="DefaultAzureCredential"/> can.
    /// </summary>
    /// <param name="clientId">The client ID of a user-assigned managed identity, or null for the system-assigned identity.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder UseManagedIdentity(string? clientId = null) =>
        UseCredential(PipelineCredential.CreateManagedIdentity(clientId));

    /// <summary>
    /// Uses Microsoft Entra Workload ID (e.g. on AKS) for all Azure sources in this pipeline.
    /// Reads the tenant, client ID and token file from the standard AZURE_* environment variables.
    /// </summary>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder UseWorkloadIdentity() => UseCredential(new WorkloadIdentityCredential());

    /// <summary>
    /// Marks configuration keys as sensitive in addition to those loaded from Key Vault, so they are
    /// masked by <c>GetRedactedDebugView()</c>. Useful for secrets supplied through environment variables.
    /// </summary>
    /// <param name="keys">The keys to mark as sensitive.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Sensitive(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            _markedSensitiveKeys.Add(key);
        }

        return this;
    }

    /// <summary>
    /// Determines whether a key holds a secret: either marked with <see cref="Sensitive"/>, or its value
    /// in the most recent build came from a sensitive source such as Key Vault.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>True if the key's value should be treated as a secret.</returns>
    public bool IsSensitive(string key) =>
        _markedSensitiveKeys.Contains(key) || _sourceSensitiveKeys.Contains(key);

    /// <summary>
    /// Adds an environment variable source to the configuration pipeline.
    /// </summary>
    /// <param name="priority">The priority of this source. Higher priority sources override lower priority ones.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder FromEnvironment(int priority = 100)
    {
        _sources.Add(new EnvironmentSource(priority));
        return this;
    }

    /// <summary>
    /// Adds a JSON file source to the configuration pipeline.
    /// </summary>
    /// <param name="filePath">The path to the JSON configuration file.</param>
    /// <param name="priority">The priority of this source. Higher priority sources override lower priority ones.</param>
    /// <param name="optional">Whether the file is optional. If false, missing file will cause an error.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder FromJsonFile(
        string filePath,
        int priority = 50,
        bool optional = false
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _sources.Add(new JsonFileSource(filePath, priority, optional));
        return this;
    }

    /// <summary>
    /// Adds a custom configuration source to the pipeline.
    /// </summary>
    /// <param name="source">The configuration source to add.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder AddSource(IConfigurationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Adds an Azure Key Vault source to the configuration pipeline.
    /// </summary>
    /// <param name="vaultUrl">The URL of the Azure Key Vault.</param>
    /// <param name="priority">The priority of this source. Higher priority sources override lower priority ones.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder FromKeyVault(string vaultUrl, int priority = 200)
    {
        ArgumentException.ThrowIfNullOrEmpty(vaultUrl);
        _sources.Add(CreateKeyVaultSource(vaultUrl, new KeyVaultConfiguration(), priority));
        return this;
    }

    /// <summary>
    /// Adds an Azure App Configuration source to the configuration pipeline.
    /// </summary>
    /// <param name="endpointOrConnectionString">
    /// The App Configuration endpoint (e.g. <c>https://myconfig.azconfig.io</c>, authenticated with
    /// Microsoft Entra ID - recommended) or a connection string.
    /// </param>
    /// <param name="configure">Optional configuration of labels, snapshots, Key Vault references, feature flags and refresh.</param>
    /// <param name="priority">The priority of this source. Higher priority sources override lower priority ones.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder FromAppConfiguration(
        string endpointOrConnectionString,
        Action<AppConfigurationOptions>? configure = null,
        int priority = 150
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointOrConnectionString);

        var options = new AppConfigurationOptions();
        configure?.Invoke(options);
        options.Credential ??= _credential;

        var isEndpoint =
            Uri.TryCreate(endpointOrConnectionString, UriKind.Absolute, out var endpoint)
            && endpoint.Scheme == Uri.UriSchemeHttps;

        _sources.Add(
            isEndpoint
                ? new AppConfigurationSource(endpoint!, options, priority)
                : new AppConfigurationSource(endpointOrConnectionString, options, priority)
        );
        return this;
    }

    /// <summary>
    /// Specifies a required configuration key. The build will fail if this key is not found.
    /// </summary>
    /// <param name="key">The configuration key that must be present.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Required(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _requiredKeys[key] = null!;
        return this;
    }

    /// <summary>
    /// Specifies a required configuration key with a specific type. The build will fail if this key is not found.
    /// </summary>
    /// <typeparam name="T">The expected type of the configuration value.</typeparam>
    /// <param name="key">The configuration key that must be present.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Required<T>(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _requiredKeys[key] = typeof(T);
        return this;
    }

    /// <summary>
    /// Specifies an optional configuration key with a default value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value to use if the key is not found.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Optional(string key, string defaultValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(defaultValue);
        _optionalKeys[key] = defaultValue;
        return this;
    }

    /// <summary>
    /// Specifies an optional configuration key with a default value and specific type.
    /// </summary>
    /// <typeparam name="T">The expected type of the configuration value.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value to use if the key is not found.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Optional<T>(string key, T defaultValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(defaultValue);
        _optionalKeys[key] = defaultValue;
        return this;
    }

    /// <summary>
    /// Adds a transformation function to the configuration pipeline.
    /// </summary>
    /// <param name="transform">The transformation function to apply to the configuration values.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Transform(
        Func<Dictionary<string, string>, Task<Result<Dictionary<string, string>>>> transform
    )
    {
        ArgumentNullException.ThrowIfNull(transform);
        _transformations.Add(transform);
        return this;
    }

    /// <summary>
    /// Adds a transformation function for a specific configuration key.
    /// </summary>
    /// <param name="key">The configuration key to transform.</param>
    /// <param name="transform">The transformation function to apply to the key's value.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Transform(string key, Func<string, Result<string>> transform)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(transform);

        _transformations.Add(config =>
        {
            if (!config.TryGetValue(key, out var value))
            {
                // Key doesn't exist, nothing to transform
                return Task.FromResult(Result<Dictionary<string, string>>.Success(config));
            }

            var transformResult = transform(value);
            if (transformResult.IsSuccess)
            {
                var newConfig = CopyConfiguration(config);
                newConfig[key] = transformResult.Value;
                return Task.FromResult(Result<Dictionary<string, string>>.Success(newConfig));
            }
            else
            {
                return Task.FromResult(
                    Result<Dictionary<string, string>>.Error(transformResult.Errors)
                );
            }
        });
        return this;
    }

    /// <summary>
    /// Adds a validation function to the configuration pipeline.
    /// </summary>
    /// <param name="validate">The validation function that returns an error message if validation fails, or null if validation succeeds.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Validate(Func<Dictionary<string, string>, string?> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        _validations.Add(config =>
        {
            var error = validate(config);
            return error == null ? Result<string>.Success("") : Result<string>.Error(error);
        });
        return this;
    }

    /// <summary>
    /// Adds a validation function for a specific configuration key.
    /// </summary>
    /// <param name="key">The configuration key to validate.</param>
    /// <param name="validate">The validation function to apply to the key's value.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ConfigurationBuilder Validate(string key, Func<string, Result<string>> validate)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(validate);

        _validations.Add(config =>
        {
            if (!config.TryGetValue(key, out var value))
            {
                // Key doesn't exist, can't validate it
                // This is not necessarily an error - the key might be optional
                return Result<string>.Success("");
            }

            var validationResult = validate(value);
            return validationResult.IsSuccess
                ? Result<string>.Success("")
                : Result<string>.Error(validationResult.Errors);
        });
        return this;
    }

    /// <summary>
    /// Builds the configuration by loading values from all configured sources.
    /// </summary>
    /// <returns>A task that represents the asynchronous build operation. The task result contains the built configuration or errors.</returns>
    public Task<Result<Dictionary<string, string>>> BuildAsync() => BuildAsync(reloadSources: false);

    /// <summary>
    /// Builds the configuration, optionally asking sources that cache their values to fetch them again.
    /// </summary>
    /// <param name="reloadSources">
    /// When true, sources implementing <see cref="IReloadableConfigurationSource"/> are reloaded
    /// rather than returning previously loaded values.
    /// </param>
    /// <returns>A task that represents the asynchronous build operation. The task result contains the built configuration or errors.</returns>
    internal async Task<Result<Dictionary<string, string>>> BuildAsync(bool reloadSources)
    {
        var errors = new List<string>();
        var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Load from all sources, sorted by priority (highest first)
        var sortedSources = _sources.OrderByDescending(s => s.Priority).ToList();

        foreach (var source in sortedSources)
        {
            var result = await (
                reloadSources && source is IReloadableConfigurationSource reloadable
                    ? reloadable.ReloadAsync()
                    : source.LoadAsync()
            ).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                // Merge configuration values (higher priority sources override lower priority ones)
                // Only set values that don't already exist since we process highest priority first
                var sensitiveSource = source as ISensitiveConfigurationSource;
                foreach (var kvp in result.Value)
                {
                    if (!configuration.ContainsKey(kvp.Key))
                    {
                        configuration[kvp.Key] = kvp.Value;
                        if (sensitiveSource?.IsSensitive(kvp.Key) == true)
                        {
                            sensitiveKeys.Add(kvp.Key);
                        }
                    }
                }
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        _sourceSensitiveKeys = sensitiveKeys;

        // Return early if we have source loading errors
        if (errors.Count > 0)
        {
            return Result<Dictionary<string, string>>.Error(errors);
        }

        // Check required keys
        foreach (var requiredKey in _requiredKeys.Keys)
        {
            if (!configuration.ContainsKey(requiredKey))
            {
                errors.Add($"Required key '{requiredKey}' was not found");
            }
        }

        // Add optional keys with default values
        foreach (var optionalKey in _optionalKeys)
        {
            if (!configuration.ContainsKey(optionalKey.Key))
            {
                configuration[optionalKey.Key] = FormatDefaultValue(optionalKey.Value);
            }
        }

        // Apply transformations
        foreach (var transformation in _transformations)
        {
            var transformResult = await transformation(configuration).ConfigureAwait(false);
            if (transformResult.IsSuccess)
            {
                // Custom transforms may return a case-sensitive dictionary; keep lookups case-insensitive
                configuration = CopyConfiguration(transformResult.Value);
            }
            else
            {
                errors.AddRange(transformResult.Errors);
            }
        }

        // Apply validations
        foreach (var validation in _validations)
        {
            var validationResult = validation(configuration);
            if (validationResult.IsFailure)
            {
                errors.AddRange(validationResult.Errors);
            }
        }

        return errors.Count > 0
            ? Result<Dictionary<string, string>>.Error(errors)
            : Result<Dictionary<string, string>>.Success(configuration);
    }

    /// <summary>
    /// Builds the configuration and binds it to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to bind the configuration to.</typeparam>
    /// <returns>A task that represents the asynchronous build operation. The task result contains the bound configuration object or errors.</returns>
    [RequiresUnreferencedCode(AotMessages.ReflectionBinding)]
    [RequiresDynamicCode(AotMessages.ReflectionBinding)]
    public async Task<Result<T>> BuildAsync<T>()
        where T : class, new()
    {
        var configResult = await BuildAsync().ConfigureAwait(false);
        if (configResult.IsFailure)
        {
            return Result<T>.Error(configResult.Errors);
        }

        try
        {
            var instance = new T();
            var bindingResult = ConfigurationBinder.Bind(configResult.Value, instance);
            return bindingResult;
        }
        catch (Exception ex)
        {
            return Result<T>.Error(
                $"Failed to bind configuration to type {typeof(T).Name}: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Builds the configuration and returns it as an Option.
    /// </summary>
    /// <returns>A task that represents the asynchronous build operation. The task result contains the configuration as an Option.</returns>
    public async Task<Option<Dictionary<string, string>>> BuildOptionalAsync()
    {
        var result = await BuildAsync().ConfigureAwait(false);
        return result.ToOption();
    }

    /// <summary>
    /// Builds the configuration and binds it to a strongly-typed object, returning an Option.
    /// </summary>
    /// <typeparam name="T">The type to bind the configuration to.</typeparam>
    /// <returns>A task that represents the asynchronous build operation. The task result contains the bound configuration object as an Option.</returns>
    [RequiresUnreferencedCode(AotMessages.ReflectionBinding)]
    [RequiresDynamicCode(AotMessages.ReflectionBinding)]
    public async Task<Option<T>> BuildOptionalAsync<T>()
        where T : class, new()
    {
        var result = await BuildAsync<T>().ConfigureAwait(false);
        return result.ToOption();
    }

    /// <summary>
    /// Specifies a configuration key that should be validated as an Option.
    /// </summary>
    /// <param name="key">The configuration key to validate</param>
    /// <param name="validator">The validation function that returns an Option</param>
    /// <returns>The configuration builder for method chaining</returns>
    public ConfigurationBuilder ValidateOptional(string key, Func<string, Option<string>> validator)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(validator);

        _validations.Add(config =>
        {
            if (!config.TryGetValue(key, out var value))
            {
                // Key doesn't exist, can't validate it
                return Result<string>.Success("");
            }

            var validationOption = validator(value);
            return validationOption.Match(
                some => Result<string>.Success(""),
                () => Result<string>.Error($"Validation failed for key '{key}'")
            );
        });
        return this;
    }

    /// <summary>
    /// Specifies a configuration key that should be validated with a predicate, returning an Option.
    /// </summary>
    /// <param name="key">The configuration key to validate</param>
    /// <param name="predicate">The validation predicate</param>
    /// <param name="errorMessage">The error message if validation fails</param>
    /// <returns>The configuration builder for method chaining</returns>
    public ConfigurationBuilder ValidateOptional(
        string key,
        Func<string, bool> predicate,
        string errorMessage
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return ValidateOptional(
            key,
            value => predicate(value) ? Option<string>.Some(value) : Option<string>.None()
        );
    }

    /// <summary>
    /// Specifies an optional configuration key that returns an Option.
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <returns>The configuration builder for method chaining</returns>
    public ConfigurationBuilder Optional(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        // For optional keys without defaults, we just track them but don't require them
        // This allows for Option-based access later
        return this;
    }

    /// <summary>
    /// Specifies a configuration key that should be transformed using an Option-based function.
    /// </summary>
    /// <param name="key">The configuration key to transform</param>
    /// <param name="transform">The transformation function that returns an Option</param>
    /// <returns>The configuration builder for method chaining</returns>
    public ConfigurationBuilder TransformOptional(
        string key,
        Func<string, Option<string>> transform
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(transform);

        _transformations.Add(config =>
        {
            if (!config.TryGetValue(key, out var value))
            {
                // Key doesn't exist, nothing to transform
                return Task.FromResult(Result<Dictionary<string, string>>.Success(config));
            }

            var transformOption = transform(value);
            return transformOption.Match(
                some =>
                {
                    var newConfig = CopyConfiguration(config);
                    newConfig[key] = some;
                    return Task.FromResult(Result<Dictionary<string, string>>.Success(newConfig));
                },
                () =>
                    Task.FromResult(
                        Result<Dictionary<string, string>>.Error(
                            $"Transformation failed for key '{key}'"
                        )
                    )
            );
        });
        return this;
    }

    /// <summary>
    /// Specifies a configuration key that should be transformed with a fallback value.
    /// </summary>
    /// <param name="key">The configuration key to transform</param>
    /// <param name="transform">The transformation function</param>
    /// <param name="fallback">The fallback value if transformation fails</param>
    /// <returns>The configuration builder for method chaining</returns>
    public ConfigurationBuilder TransformWithFallback(
        string key,
        Func<string, Option<string>> transform,
        string fallback
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(fallback);

        _transformations.Add(config =>
        {
            if (!config.TryGetValue(key, out var value))
            {
                // Key doesn't exist, nothing to transform
                return Task.FromResult(Result<Dictionary<string, string>>.Success(config));
            }

            var transformOption = transform(value);
            var newConfig = CopyConfiguration(config);
            newConfig[key] = transformOption.GetValueOrDefault(fallback);
            return Task.FromResult(Result<Dictionary<string, string>>.Success(newConfig));
        });
        return this;
    }

    /// <summary>
    /// Creates a Key Vault source that uses the pipeline credential unless the configuration specifies one.
    /// </summary>
    internal KeyVaultSource CreateKeyVaultSource(
        string vaultUrl,
        KeyVaultConfiguration configuration,
        int priority,
        Microsoft.Extensions.Logging.ILogger? logger = null
    )
    {
        configuration.Credential ??= _credential;
        return new KeyVaultSource(vaultUrl, configuration, priority, logger);
    }

    /// <summary>
    /// Copies configuration values into a new case-insensitive dictionary.
    /// If the source contains keys differing only by case, the last one wins.
    /// </summary>
    private static Dictionary<string, string> CopyConfiguration(
        IReadOnlyDictionary<string, string> source
    )
    {
        var copy = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
        {
            copy[kvp.Key] = kvp.Value;
        }

        return copy;
    }

    /// <summary>
    /// Formats a typed default value culture-invariantly so it round-trips through binding
    /// regardless of the current culture (e.g. 1.5 stays "1.5" rather than "1,5" in de-DE).
    /// </summary>
    private static string FormatDefaultValue(object value) =>
        value switch
        {
            string s => s,
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
}
