namespace FluentAzure.Core;

/// <summary>
/// A configuration source that can say which of its values are secrets, so they can be masked
/// wherever configuration is displayed (e.g. <c>GetRedactedDebugView()</c>).
/// </summary>
public interface ISensitiveConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Determines whether the value this source provides for a key is a secret.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>True if the value is a secret.</returns>
    bool IsSensitive(string key);
}
