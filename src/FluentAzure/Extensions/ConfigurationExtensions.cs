using FluentAzure.Core;

namespace FluentAzure.Extensions;

/// <summary>
/// Extension methods for working with configuration dictionaries using Options.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Validates a configuration value using a predicate.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="validator">The validation predicate</param>
    /// <param name="errorMessage">The error message if validation fails</param>
    /// <returns>Success with the value if valid, Error otherwise</returns>
    public static Result<string> Validate(
        this Dictionary<string, string> config,
        string key,
        Func<string, bool> validator,
        string errorMessage
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(errorMessage);
        return config
            .GetOptional(key)
            .ToResult($"Configuration key '{key}' not found")
            .Bind(value =>
                validator(value)
                    ? Result<string>.Success(value)
                    : Result<string>.Error(errorMessage)
            );
    }

    /// <summary>
    /// Validates a configuration value using a predicate with custom error factory.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="validator">The validation predicate</param>
    /// <param name="errorFactory">The error message factory</param>
    /// <returns>Success with the value if valid, Error otherwise</returns>
    public static Result<string> Validate(
        this Dictionary<string, string> config,
        string key,
        Func<string, bool> validator,
        Func<string, string> errorFactory
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(errorFactory);

        return config
            .GetOptional(key)
            .ToResult($"Configuration key '{key}' not found")
            .Bind(value =>
                validator(value)
                    ? Result<string>.Success(value)
                    : Result<string>.Error(errorFactory(value))
            );
    }

    /// <summary>
    /// Gets a required configuration value, returning an error if not found.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <returns>Success with the value if found, Error otherwise</returns>
    public static Result<string> GetRequired(this Dictionary<string, string> config, string key)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);

        return config.GetOptional(key).ToResult($"Required configuration key '{key}' not found");
    }

    /// <summary>
    /// Gets a required configuration value with type conversion, returning an error if not found or conversion fails.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <returns>Success with the converted value if found and conversion succeeds, Error otherwise</returns>
    public static Result<T> GetRequired<T>(this Dictionary<string, string> config, string key)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);

        return config
            .GetOptional(key)
            .ToResult($"Required configuration key '{key}' not found")
            .Bind(value =>
            {
                try
                {
                    var converted = Convert.ChangeType(value, typeof(T));
                    return converted is T typedValue
                        ? Result<T>.Success(typedValue)
                        : Result<T>.Error(
                            $"Failed to convert value '{value}' to type {typeof(T).Name}"
                        );
                }
                catch (Exception ex)
                {
                    return Result<T>.Error(
                        $"Failed to convert value '{value}' to type {typeof(T).Name}: {ex.Message}"
                    );
                }
            });
    }

    /// <summary>
    /// Gets a configuration value with a default if not found.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value to use if the key is not found</param>
    /// <returns>The configuration value or the default value</returns>
    public static string GetOrDefault(
        this Dictionary<string, string> config,
        string key,
        string defaultValue
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(defaultValue);

        return config.GetOptional(key).GetValueOrDefault(defaultValue);
    }

    /// <summary>
    /// Gets a configuration value with type conversion and a default if not found.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value to use if the key is not found</param>
    /// <returns>The converted configuration value or the default value</returns>
    public static T GetOrDefault<T>(
        this Dictionary<string, string> config,
        string key,
        T defaultValue
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);

        return config.GetOptional<T>(key).GetValueOrDefault(defaultValue);
    }

    /// <summary>
    /// Gets a configuration value with a default factory if not found.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultFactory">The factory function to create the default value</param>
    /// <returns>The configuration value or the result of the default factory</returns>
    public static string GetOrDefault(
        this Dictionary<string, string> config,
        string key,
        Func<string> defaultFactory
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(defaultFactory);

        return config.GetOptional(key).GetValueOrDefault(defaultFactory);
    }

    /// <summary>
    /// Gets a configuration value with type conversion and a default factory if not found.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultFactory">The factory function to create the default value</param>
    /// <returns>The converted configuration value or the result of the default factory</returns>
    public static T GetOrDefault<T>(
        this Dictionary<string, string> config,
        string key,
        Func<T> defaultFactory
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(defaultFactory);

        return config.GetOptional<T>(key).GetValueOrDefault(defaultFactory);
    }

    /// <summary>
    /// Gets a configuration value as an Option with type conversion.
    /// Returns None if the key is not found, Some(value) if found and conversion succeeds.
    /// </summary>
    /// <typeparam name="T">The target type</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <returns>Some(converted value) if the key exists and conversion succeeds, None otherwise</returns>
    public static Option<T> GetOption<T>(this Dictionary<string, string> configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!configuration.TryGetValue(key, out var value))
        {
            return Option<T>.None();
        }

        var conversionResult = TypeExtensions.TryConvert<T>(value);
        return conversionResult.Match(success => Option<T>.Some(success), _ => Option<T>.None());
    }

    /// <summary>
    /// Gets a configuration value as an Option with type conversion and validation.
    /// Returns None if the key is not found or validation fails.
    /// </summary>
    /// <typeparam name="T">The target type</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="validator">The validation function</param>
    /// <returns>Some(converted value) if the key exists, conversion succeeds, and validation passes, None otherwise</returns>
    public static Option<T> GetOption<T>(
        this Dictionary<string, string> configuration,
        string key,
        Func<T, bool> validator
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(validator);

        return configuration.GetOption<T>(key).Filter(validator);
    }
}
