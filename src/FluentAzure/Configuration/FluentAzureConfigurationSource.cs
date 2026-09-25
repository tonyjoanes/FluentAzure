using Microsoft.Extensions.Configuration;
using FluentPipeline = FluentAzure.Core.ConfigurationBuilder;

namespace FluentAzure.Configuration;

/// <summary>
/// Exposes a FluentAzure pipeline (sources, required keys, transforms and validations) as a
/// Microsoft.Extensions.Configuration source, so its values flow into <see cref="IConfiguration"/>,
/// options binding and <c>IOptionsMonitor&lt;T&gt;</c>.
/// </summary>
public class FluentAzureConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentAzureConfigurationSource"/> class.
    /// </summary>
    /// <param name="pipeline">The FluentAzure pipeline to load values from.</param>
    public FluentAzureConfigurationSource(FluentPipeline pipeline)
    {
        Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <summary>
    /// Gets the FluentAzure pipeline that provides the configuration values.
    /// </summary>
    public FluentPipeline Pipeline { get; }

    /// <summary>
    /// Gets or sets how often the pipeline is re-run to pick up changed values, such as rotated
    /// Key Vault secrets. When a reload produces different values, change tokens fire so
    /// <c>IOptionsMonitor&lt;T&gt;</c> consumers see the update. Null (the default) disables periodic reload.
    /// </summary>
    public TimeSpan? ReloadInterval { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked when a reload fails. The last successfully loaded values are kept.
    /// </summary>
    public Action<IReadOnlyList<string>>? OnReloadError { get; set; }

    /// <summary>
    /// Gets or sets values that were already loaded asynchronously, used for the first load
    /// instead of running the pipeline synchronously.
    /// </summary>
    internal Dictionary<string, string>? PreloadedValues { get; set; }

    /// <summary>
    /// Gets or sets the clock used for load timestamps; replaceable in tests.
    /// </summary>
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new FluentAzureConfigurationProvider(this);
}
