using FluentAzure.Core;
using Microsoft.Extensions.DependencyInjection;

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
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new ConfigurationBuilder());
        var result = RunSync(builder.BuildAsync<T>);

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
    /// Asynchronously adds FluentAzure configuration to the service collection with a strongly-typed
    /// configuration object. Prefer this over <see cref="AddFluentAzure{T}(IServiceCollection, Func{ConfigurationBuilder, ConfigurationBuilder})"/>
    /// where the caller can await (e.g. top-level statements in Program.cs), as it loads remote sources
    /// such as Key Vault without blocking a thread.
    /// Always registers as singleton.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object to bind.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration builder action.</param>
    /// <returns>A task whose result is the service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration binding fails.</exception>
    public static async Task<IServiceCollection> AddFluentAzureAsync<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new ConfigurationBuilder());
        var result = await builder.BuildAsync<T>().ConfigureAwait(false);

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
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(factory);

        var builder = configure(new ConfigurationBuilder());
        var result = RunSync(builder.BuildAsync<T>);

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

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with Option-based error handling.
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">The configuration builder action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFluentAzureOptional<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new ConfigurationBuilder());
        var option = RunSync(builder.BuildOptionalAsync<T>);

        return option.Match(
            some =>
            {
                services.AddSingleton(some);
                return services;
            },
            () =>
            {
                // Log warning but don't throw - this allows for graceful degradation
                // The application can handle missing configuration at runtime
                return services;
            }
        );
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with Option-based error handling and factory.
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">The configuration builder action</param>
    /// <param name="factory">Factory method to create the configuration object</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFluentAzureOptional<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure,
        Func<T, T> factory
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(factory);

        var builder = configure(new ConfigurationBuilder());
        var option = RunSync(builder.BuildOptionalAsync<T>);

        return option.Match(
            some =>
            {
                var configuredInstance = factory(some);
                services.AddSingleton(configuredInstance);
                return services;
            },
            () =>
            {
                // Log warning but don't throw - this allows for graceful degradation
                return services;
            }
        );
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with fallback handling.
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">The configuration builder action</param>
    /// <param name="fallback">The fallback configuration if binding fails</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFluentAzureWithFallback<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure,
        T fallback
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(fallback);

        var builder = configure(new ConfigurationBuilder());
        var option = RunSync(builder.BuildOptionalAsync<T>);

        var configuration = option.GetValueOrDefault(fallback);
        services.AddSingleton(configuration);
        return services;
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with fallback factory.
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">The configuration builder action</param>
    /// <param name="fallbackFactory">The fallback factory if binding fails</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFluentAzureWithFallback<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure,
        Func<T> fallbackFactory
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(fallbackFactory);

        var builder = configure(new ConfigurationBuilder());
        var option = RunSync(builder.BuildOptionalAsync<T>);

        var configuration = option.GetValueOrDefault(fallbackFactory);
        services.AddSingleton(configuration);
        return services;
    }

    /// <summary>
    /// Adds FluentAzure configuration to the service collection with conditional registration.
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">The configuration builder action</param>
    /// <param name="condition">The condition that determines whether to register the configuration</param>
    /// <param name="fallback">The fallback configuration if condition is false or binding fails</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFluentAzureConditional<T>(
        this IServiceCollection services,
        Func<ConfigurationBuilder, ConfigurationBuilder> configure,
        Func<T, bool> condition,
        T fallback
    )
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(fallback);

        var builder = configure(new ConfigurationBuilder());
        var option = RunSync(builder.BuildOptionalAsync<T>);

        var configuration = option.Filter(condition).GetValueOrDefault(fallback);

        services.AddSingleton(configuration);
        return services;
    }

    /// <summary>
    /// Runs an async build to completion from a synchronous registration method.
    /// The work is started on the thread pool so that awaits inside the pipeline never try to
    /// resume on a captured synchronization context, which would deadlock the blocked caller.
    /// </summary>
    private static TResult RunSync<TResult>(Func<Task<TResult>> operation) =>
        Task.Run(operation).GetAwaiter().GetResult();
}
