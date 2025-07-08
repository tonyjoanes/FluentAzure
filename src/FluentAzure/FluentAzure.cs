namespace FluentAzure;

/// <summary>
/// Main entry point for the FluentAzure configuration pipeline.
/// This is a facade that provides a cleaner API for consumers.
///
/// With 'using FluentAzure;' you can use:
/// - FluentConfig() directly (ultra clean)
/// - FluentAzure.Configuration() (clean)
/// </summary>
public static class FluentAzure
{
    /// <summary>
    /// Starts a new Azure configuration pipeline builder.
    /// Clean API - FluentAzure.FluentConfig()
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static Core.ConfigurationBuilder FluentConfig()
    {
        return Core.FluentAzure.Configuration();
    }

    /// <summary>
    /// Starts a new Azure configuration pipeline builder.
    /// Alternative API - FluentAzure.Configuration()
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static Core.ConfigurationBuilder Configuration()
    {
        return Core.FluentAzure.Configuration();
    }
}

/// <summary>
/// Global static methods for ultra-clean API
/// These methods are available directly when using FluentAzure
/// </summary>
public static class FluentAzureGlobalMethods
{
    /// <summary>
    /// Starts a new Azure configuration pipeline builder.
    /// Ultra clean API - just FluentConfig()
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static Core.ConfigurationBuilder FluentConfig()
    {
        return Core.FluentAzure.Configuration();
    }
}
