namespace FluentAzure.Core;

/// <summary>
/// Shared messages for trimming and Native AOT annotations.
/// </summary>
internal static class AotMessages
{
    /// <summary>
    /// Explains why reflection-based binding APIs are unsafe in trimmed/AOT apps and what to use instead.
    /// </summary>
    public const string ReflectionBinding =
        "Reflection-based binding is not compatible with trimming or Native AOT. In trimmed or AOT apps, "
        + "add FluentAzure to IConfiguration with AddFluentAzure() and bind options with the configuration "
        + "binding source generator (<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>).";
}
