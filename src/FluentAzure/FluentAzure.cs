namespace FluentAzure;

/// <summary>
/// Main entry point for the FluentAzure configuration pipeline.
/// This is a facade that provides a cleaner API for consumers.
/// </summary>
public static class FluentAzure
{
    /// <summary>
    /// Starts a new configuration pipeline builder.
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static Core.ConfigurationBuilder Configuration()
    {
        return Core.FluentAzure.Configuration();
    }
}
