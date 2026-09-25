using FluentAzure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentPipeline = FluentAzure.Core.ConfigurationBuilder;

namespace FluentAzure;

/// <summary>
/// Integrates FluentAzure pipelines with Microsoft.Extensions.Configuration and the options pattern.
/// These are available directly when using FluentAzure.
/// </summary>
public static class FluentAzureConfigurationExtensions
{
    /// <summary>
    /// Adds a FluentAzure pipeline as a configuration provider. Its values become part of
    /// <see cref="IConfiguration"/> and can be bound with the options pattern. The pipeline's
    /// required keys and validations are enforced when the configuration is first built.
    /// </summary>
    /// <param name="builder">The configuration builder, e.g. <c>WebApplicationBuilder.Configuration</c>.</param>
    /// <param name="configure">Configures the FluentAzure pipeline.</param>
    /// <param name="reloadInterval">
    /// Optional interval at which the pipeline is re-run (e.g. to pick up rotated Key Vault secrets).
    /// Changes are pushed to <c>IOptionsMonitor&lt;T&gt;</c> consumers.
    /// </param>
    /// <returns>The configuration builder for chaining.</returns>
    /// <exception cref="FluentAzureConfigurationException">Thrown when the initial load fails.</exception>
    public static IConfigurationBuilder AddFluentAzure(
        this IConfigurationBuilder builder,
        Func<FluentPipeline, FluentPipeline> configure,
        TimeSpan? reloadInterval = null
    )
    {
        return builder.AddFluentAzure(configure, source => source.ReloadInterval = reloadInterval);
    }

    /// <summary>
    /// Adds a FluentAzure pipeline as a configuration provider, with full control over the source options.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="configure">Configures the FluentAzure pipeline.</param>
    /// <param name="configureSource">Configures reload behaviour and error callbacks.</param>
    /// <returns>The configuration builder for chaining.</returns>
    /// <exception cref="FluentAzureConfigurationException">Thrown when the initial load fails.</exception>
    public static IConfigurationBuilder AddFluentAzure(
        this IConfigurationBuilder builder,
        Func<FluentPipeline, FluentPipeline> configure,
        Action<FluentAzureConfigurationSource> configureSource
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configureSource);

        var source = new FluentAzureConfigurationSource(configure(new FluentPipeline()));
        configureSource(source);
        return builder.Add(source);
    }

    /// <summary>
    /// Loads a FluentAzure pipeline asynchronously, then adds it as a configuration provider.
    /// Prefer this where the caller can await, so remote sources such as Key Vault load without
    /// blocking a thread during startup.
    /// </summary>
    /// <param name="builder">The configuration builder, e.g. <c>WebApplicationBuilder.Configuration</c>.</param>
    /// <param name="configure">Configures the FluentAzure pipeline.</param>
    /// <param name="reloadInterval">Optional interval at which the pipeline is re-run.</param>
    /// <returns>A task whose result is the configuration builder for chaining.</returns>
    /// <exception cref="FluentAzureConfigurationException">Thrown when the load fails.</exception>
    public static async Task<IConfigurationBuilder> AddFluentAzureAsync(
        this IConfigurationBuilder builder,
        Func<FluentPipeline, FluentPipeline> configure,
        TimeSpan? reloadInterval = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var pipeline = configure(new FluentPipeline());
        var result = await pipeline.BuildAsync().ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new FluentAzureConfigurationException(result.Errors);
        }

        return builder.Add(
            new FluentAzureConfigurationSource(pipeline)
            {
                ReloadInterval = reloadInterval,
                PreloadedValues = result.Value,
            }
        );
    }

    /// <summary>
    /// Registers <typeparamref name="T"/> with the options pattern, bound to a configuration section,
    /// validated with data annotations, and validated when the host starts so misconfiguration fails
    /// fast. Inject <see cref="IOptions{TOptions}"/>, or <see cref="IOptionsMonitor{TOptions}"/> to
    /// receive updates when a reloading FluentAzure provider picks up new values.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <param name="sectionName">The section to bind, or null to bind from the root.</param>
    /// <returns>The options builder, for adding further validation.</returns>
    public static OptionsBuilder<T> AddFluentAzureOptions<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = string.IsNullOrEmpty(sectionName)
            ? configuration
            : configuration.GetSection(sectionName);

        return services
            .AddOptions<T>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
