using FluentAzure.Binding;
using FluentAzure.Extensions;
using FluentAzure.Sources;

namespace FluentAzure.Core;

/// <summary>
/// Fluent builder for creating configuration pipelines that can load configuration from multiple sources.
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
    private readonly List<Action<ConfigurationChangedEventArgs>> _changeHandlers = new();

    /// <summary>
    /// Adds a configuration change handler to the builder.
    /// </summary>
    /// <param name="handler">The change handler.</param>
    internal void AddConfigurationChangeHandler(Action<ConfigurationChangedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _changeHandlers.Add(handler);
    }

    /// <summary>
    /// Adds a configuration change handler to the builder.
    /// </summary>
    /// <param name="handler">The change handler.</param>
    internal void AddConfigurationChangeHandler(Action<Dictionary<string, string>, Dictionary<string, string>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _changeHandlers.Add(args => handler(args.PreviousValues, args.NewValues));
    }

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

        // Wire up change handlers for sources that support hot reload
        if (source.SupportsHotReload && _changeHandlers.Count > 0)
        {
            source.ConfigurationChanged += (sender, e) =>
            {
                foreach (var handler in _changeHandlers)
                {
                    try
                    {
                        handler(e);
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't throw - change handlers should be resilient
                        System.Diagnostics.Debug.WriteLine($"Error in configuration change handler: {ex.Message}");
                    }
                }
            };
        }

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
        _sources.Add(new KeyVaultSource(vaultUrl, priority));
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
                var newConfig = new Dictionary<string, string>(config);
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
    public async Task<Result<Dictionary<string, string>>> BuildAsync()
    {
        var errors = new List<string>();
        var configuration = new Dictionary<string, string>();

        // Load from all sources, sorted by priority (highest first)
        var sortedSources = _sources.OrderByDescending(s => s.Priority).ToList();

        foreach (var source in sortedSources)
        {
            var result = await source.LoadAsync();
            if (result.IsSuccess)
            {
                // Merge configuration values (higher priority sources override lower priority ones)
                // Only set values that don't already exist since we process highest priority first
                foreach (var kvp in result.Value)
                {
                    if (!configuration.ContainsKey(kvp.Key))
                    {
                        configuration[kvp.Key] = kvp.Value;
                    }
                }
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

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
                configuration[optionalKey.Key] = optionalKey.Value.ToString()!;
            }
        }

        // Apply transformations
        foreach (var transformation in _transformations)
        {
            var transformResult = await transformation(configuration);
            if (transformResult.IsSuccess)
            {
                configuration = transformResult.Value;
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
    public async Task<Result<T>> BuildAsync<T>()
        where T : class, new()
    {
        var configResult = await BuildAsync();
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
        var result = await BuildAsync();
        return result.ToOption();
    }

    /// <summary>
    /// Builds the configuration and binds it to a strongly-typed object, returning an Option.
    /// </summary>
    /// <typeparam name="T">The type to bind the configuration to.</typeparam>
    /// <returns>A task that represents the asynchronous build operation. The task result contains the bound configuration object as an Option.</returns>
    public async Task<Option<T>> BuildOptionalAsync<T>()
        where T : class, new()
    {
        var result = await BuildAsync<T>();
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
                    var newConfig = new Dictionary<string, string>(config);
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
            var newConfig = new Dictionary<string, string>(config);
            newConfig[key] = transformOption.GetValueOrDefault(fallback);
            return Task.FromResult(Result<Dictionary<string, string>>.Success(newConfig));
        });
        return this;
    }
}
