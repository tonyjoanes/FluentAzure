namespace FluentAzure.Guard;

/// <summary>
/// Recognises keys and values that are almost certainly credentials, so debug views can mask them even when
/// nobody declared them sensitive. Deliberately broad: masking a harmless value in a debug view costs little.
/// </summary>
internal static class SecretHeuristics
{
    // Matched against the last segment of a key, e.g. "SecretKey" in "Jwt:SecretKey"
    private static readonly string[] SensitiveKeyWords =
    {
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "accesskey", "privatekey",
        "connectionstring", "credential", "sas",
    };

    // Fragments that appear in connection strings, SAS URLs and PEM keys
    private static readonly string[] SecretValueMarkers =
    {
        "accountkey=", "sharedaccesskey=", "sharedaccesssignature=", "password=", "pwd=", "secret=",
        "accesskey=", "clientsecret=", "sig=", "-----begin",
    };

    /// <summary>
    /// Determines whether a key looks like it holds a credential: its last segment names one, or it sits
    /// in the conventional <c>ConnectionStrings</c> section.
    /// </summary>
    /// <param name="key">The configuration key, using ":" separators.</param>
    /// <returns><see langword="true"/> if the value should be masked.</returns>
    public static bool IsSensitiveKey(string key)
    {
        if (key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var separator = key.LastIndexOf(':');
        var lastSegment = separator < 0 ? key : key[(separator + 1)..];
        foreach (var word in SensitiveKeyWords)
        {
            if (lastSegment.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a value looks like a credential, such as a connection string with a key or password.
    /// </summary>
    /// <param name="value">The configuration value.</param>
    /// <returns><see langword="true"/> if the value should be masked.</returns>
    public static bool IsSecretValue(string value)
    {
        foreach (var marker in SecretValueMarkers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
