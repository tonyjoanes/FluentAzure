using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAzure.Binding;
using FluentAzure.Core;

namespace FluentAzure.Extensions;

/// <summary>
/// Extension methods for configuration binding using Options.
/// </summary>
public static class BindingExtensions
{
    /// <summary>
    /// Binds configuration to a strongly-typed object and returns an Option.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <returns>Some(bound object) if binding succeeds, None otherwise</returns>
    public static Option<T> BindOptional<T>(this Dictionary<string, string> configuration)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var result = ConfigurationBinder.Bind<T>(configuration);
        return result.ToOption();
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with options and returns an Option.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="options">The binding options</param>
    /// <returns>Some(bound object) if binding succeeds, None otherwise</returns>
    public static Option<T> BindOptional<T>(
        this Dictionary<string, string> configuration,
        BindingOptions options
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var result = EnhancedConfigurationBinder.Bind<T>(configuration, options);
        return result.ToOption();
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with JSON deserialization and returns an Option.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="options">The binding options</param>
    /// <returns>Some(bound object) if binding succeeds, None otherwise</returns>
    public static Option<T> BindJsonOptional<T>(
        this Dictionary<string, string> configuration,
        BindingOptions? options = null
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var result = EnhancedConfigurationBinder.BindJson<T>(configuration, options);
        return result.ToOption();
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with fallback handling.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="fallback">The fallback object if binding fails</param>
    /// <returns>The bound object or the fallback</returns>
    public static T BindWithFallback<T>(this Dictionary<string, string> configuration, T fallback)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fallback);

        return configuration.BindOptional<T>().GetValueOrDefault(fallback);
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with fallback factory.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="fallbackFactory">The fallback factory if binding fails</param>
    /// <returns>The bound object or the result of the fallback factory</returns>
    public static T BindWithFallback<T>(
        this Dictionary<string, string> configuration,
        Func<T> fallbackFactory
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fallbackFactory);

        return configuration.BindOptional<T>().GetValueOrDefault(fallbackFactory);
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with validation.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="validator">The validation function</param>
    /// <returns>Some(bound object) if binding and validation succeed, None otherwise</returns>
    public static Option<T> BindWithValidation<T>(
        this Dictionary<string, string> configuration,
        Func<T, bool> validator
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(validator);

        return configuration.BindOptional<T>().Filter(validator);
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with validation and custom error handling.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="validator">The validation function</param>
    /// <param name="errorFactory">The error message factory</param>
    /// <returns>Success with the bound object if validation succeeds, Error otherwise</returns>
    public static Result<T> BindWithValidation<T>(
        this Dictionary<string, string> configuration,
        Func<T, bool> validator,
        Func<T, string> errorFactory
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(errorFactory);

        return configuration
            .BindOptional<T>()
            .ToResult("Configuration binding failed")
            .Bind(obj =>
                validator(obj) ? Result<T>.Success(obj) : Result<T>.Error(errorFactory(obj))
            );
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with conditional binding.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="condition">The condition that determines whether to bind</param>
    /// <param name="fallback">The fallback object if condition is false or binding fails</param>
    /// <returns>The bound object or the fallback</returns>
    public static T BindConditional<T>(
        this Dictionary<string, string> configuration,
        Func<T, bool> condition,
        T fallback
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(fallback);

        return configuration.BindOptional<T>().Filter(condition).GetValueOrDefault(fallback);
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with transformation.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <typeparam name="TResult">The type of the transformed result</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="transformer">The transformation function</param>
    /// <returns>Some(transformed object) if binding and transformation succeed, None otherwise</returns>
    public static Option<TResult> BindAndTransform<T, TResult>(
        this Dictionary<string, string> configuration,
        Func<T, TResult> transformer
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transformer);

        return configuration.BindOptional<T>().Map(transformer);
    }

    /// <summary>
    /// Binds configuration to a strongly-typed object with transformation and fallback.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <typeparam name="TResult">The type of the transformed result</typeparam>
    /// <param name="configuration">The configuration dictionary</param>
    /// <param name="transformer">The transformation function</param>
    /// <param name="fallback">The fallback value if binding or transformation fails</param>
    /// <returns>The transformed object or the fallback</returns>
    public static TResult BindAndTransformWithFallback<T, TResult>(
        this Dictionary<string, string> configuration,
        Func<T, TResult> transformer,
        TResult fallback
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transformer);

        return configuration.BindOptional<T>().Map(transformer).GetValueOrDefault(fallback);
    }
}
