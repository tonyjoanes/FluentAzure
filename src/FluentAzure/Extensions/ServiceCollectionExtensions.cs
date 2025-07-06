using Microsoft.Extensions.DependencyInjection;
using FluentAzure.Core;

namespace FluentAzure.Extensions;

/// <summary>
/// Extension methods for integrating FluentAzure with Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FluentAzure configuration to the service collection with a strongly-typed configuration object.
    /// Always registers as singleton.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to bind.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration builder action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration binding fails.</exception>
    public static IServiceCollection AddFluentAzure<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure
    ) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new ConfigurationBuilder());
        var result = builder.BuildAsync<T>().GetAwaiter().GetResult();

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to bind configuration of type '{typeof(T).Name}': {string.Join("; ", result.Errors)}"
            );
        }

        services.AddSingleton(result.Value!);
        return services;
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with a factory method.
    /// Always registers as singleton.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to bind.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration builder action.</param>
    /// <param name="factory">Factory method to create the configuration object.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration binding fails.</exception>
    public static IServiceCollection AddFluentAzure<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure,
        Func<T, T> factory
    ) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(factory);

        var builder = configure(new ConfigurationBuilder());
        var result = builder.BuildAsync<T>().GetAwaiter().GetResult();

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to bind configuration of type '{typeof(T).Name}': {string.Join("; ", result.Errors)}"
            );
        }

        var configuredInstance = factory(result.Value!);
        services.AddSingleton(configuredInstance);
        return services;
    }
}
