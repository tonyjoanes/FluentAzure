namespace FluentAzure.Configuration;

/// <summary>
/// Thrown when a FluentAzure pipeline used as a Microsoft.Extensions.Configuration provider
/// fails its initial load, e.g. a required key is missing or a validation rule fails.
/// </summary>
public class FluentAzureConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentAzureConfigurationException"/> class.
    /// </summary>
    /// <param name="errors">The errors reported by the configuration pipeline.</param>
    public FluentAzureConfigurationException(IReadOnlyList<string> errors)
        : base($"FluentAzure configuration failed to load: {string.Join("; ", errors)}")
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the errors reported by the configuration pipeline.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
