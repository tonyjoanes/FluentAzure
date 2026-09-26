namespace FluentAzure;

/// <summary>
/// Version information for FluentAzure.
/// Update these constants when releasing new versions.
/// </summary>
public static class Version
{
    /// <summary>
    /// Current major version number.
    /// </summary>
    public const int Major = 0;

    /// <summary>
    /// Current minor version number.
    /// </summary>
    public const int Minor = 3;

    /// <summary>
    /// Current patch version number.
    /// </summary>
    public const int Patch = 0;

    /// <summary>
    /// Current pre-release identifier (e.g., "rc.4", "beta.1", etc.).
    /// Set to null for stable releases.
    /// </summary>
    public const string? PreRelease = "rc.1";

    /// <summary>
    /// Gets the full version string in semantic versioning format.
    /// </summary>
    public static string Full => PreRelease != null ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Gets the version string for NuGet package.
    /// </summary>
    public static string Package => Full;

    /// <summary>
    /// Gets the assembly version (major.minor.0.0 for compatibility).
    /// </summary>
    public static string Assembly => $"{Major}.{Minor}.0.0";

    /// <summary>
    /// Gets the file version (full version for debugging).
    /// </summary>
    public static string File => $"{Major}.{Minor}.{Patch}.0";

    /// <summary>
    /// Gets the informational version (full version with metadata).
    /// </summary>
    public static string Informational => Full;

    /// <summary>
    /// Gets the version as a System.Version object.
    /// </summary>
    public static System.Version AsVersion => new System.Version(Major, Minor, Patch);

    /// <summary>
    /// Gets a value indicating whether this is a pre-release version.
    /// </summary>
    public static bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>
    /// Gets a value indicating whether this is a stable release.
    /// </summary>
    public static bool IsStable => string.IsNullOrEmpty(PreRelease);
}
