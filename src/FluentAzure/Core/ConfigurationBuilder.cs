using FluentAzure.Binding;
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
                return Task.FromResult(Result<Dictionary<string, string>>.Error(transformResult.Errors));
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
}
