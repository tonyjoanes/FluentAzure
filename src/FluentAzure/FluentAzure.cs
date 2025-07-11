using FluentAzure.Binding;
using FluentAzure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAzure;

/// <summary>
/// Main entry point for the FluentAzure configuration pipeline.
/// This provides a clean, unified API surface that requires only 'using FluentAzure;'.
/// </summary>
public static class FluentConfig
{
    /// <summary>
    /// Gets the current version information for FluentAzure.
    /// </summary>
    public static string CurrentVersion => Version.Full;
    /// <summary>
    /// Starts a new Azure configuration pipeline builder.
    /// Main entry point - FluentConfig.Create()
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static Core.ConfigurationBuilder Create()
    {
        return new Core.ConfigurationBuilder();
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with a strongly-typed configuration object.
    /// This method is available directly when using FluentAzure.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to bind.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration builder action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration binding fails.</exception>
    public static IServiceCollection AddFluentAzure<T>(
        this IServiceCollection services,
        Func<Core.ConfigurationBuilder, Core.ConfigurationBuilder> configure
    )
        where T : class, new()
    {
        return Extensions.ServiceCollectionExtensions.AddFluentAzure<T>(services, configure);
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with a factory method.
    /// This method is available directly when using FluentAzure.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to bind.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration builder action.</param>
    /// <param name="factory">Factory method to create the configuration object.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration binding fails.</exception>
    public static IServiceCollection AddFluentAzure<T>(
        this IServiceCollection services,
        Func<Core.ConfigurationBuilder, Core.ConfigurationBuilder> configure,
        Func<T, T> factory
    )
        where T : class, new()
    {
        return Extensions.ServiceCollectionExtensions.AddFluentAzure<T>(
            services,
            configure,
            factory
        );
    }
}

/// <summary>
/// Extension methods for binding configuration results.
/// These are available directly when using FluentAzure.
/// </summary>
public static class BindingExtensions
{
    /// <summary>
    /// Binds configuration to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <param name="result">The configuration result</param>
    /// <returns>A result containing the bound object or errors</returns>
    public static Result<T> Bind<T>(this Result<Dictionary<string, string>> result)
        where T : class, new()
    {
        return result.Bind(config => ConfigurationBinder.Bind<T>(config));
    }
}
