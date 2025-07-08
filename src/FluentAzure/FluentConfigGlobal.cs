namespace FluentAzure;

/// <summary>
/// Global static class providing the ultra-clean FluentConfig() API.
/// This class is automatically available when using FluentAzure.
/// </summary>
public static class FluentConfigGlobal
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
