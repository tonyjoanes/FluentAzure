namespace FluentAzure;

/// <summary>
/// Global methods that provide ultra-clean API access.
/// These methods can be used when 'using FluentAzure;' is imported.
/// </summary>
public static class GlobalMethods
{
    /// <summary>
    /// Starts a new Azure configuration pipeline builder.
    /// Ultra clean API - just FluentConfig()
    /// This method provides the cleanest possible API surface.
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static Core.ConfigurationBuilder FluentConfig()
    {
        return Core.FluentAzure.Configuration();
    }
}
