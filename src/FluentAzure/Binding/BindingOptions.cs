using System.Text.Json;

namespace FluentAzure.Binding;

/// <summary>
/// Options for configuration binding.
/// </summary>
public class BindingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether to enable validation using Data Annotations.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether configuration key matching is case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Gets or sets custom JSON serialization options.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether a property with no configuration key keeps its current value, such as a
    /// default set by its initializer. When <c>false</c>, it is reset to <c>null</c> or <c>0</c>.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool IgnoreMissingOptional { get; set; } = true;

    /// <summary>
    /// Gets or sets the configuration dictionary (used for record binding).
    /// </summary>
    public Dictionary<string, string>? Configuration { get; set; }
}
