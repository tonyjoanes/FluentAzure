namespace FluentAzure;

/// <summary>
/// Main entry point for the FluentAzure configuration pipeline.
/// </summary>
public static class FluentAzure
{
    /// <summary>
    /// Starts a new configuration pipeline builder.
    /// </summary>
    /// <returns>A new configuration builder instance.</returns>
    public static ConfigurationBuilder Configuration()
    {
        return new ConfigurationBuilder();
    }
}
