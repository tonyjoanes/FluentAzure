using Azure.Core;
using Azure.Security.KeyVault.Secrets;

namespace FluentAzure.Sources;

/// <summary>
/// Options for the Azure App Configuration source.
/// </summary>
public class AppConfigurationOptions
{
    /// <summary>
    /// The label filter that selects settings with no label.
    /// </summary>
    public const string NoLabel = "\0";

    /// <summary>
    /// Gets or sets the credential used for App Configuration (when connecting by endpoint) and,
    /// unless <see cref="SecretClientFactory"/> is set, for resolving Key Vault references.
    /// Defaults to DefaultAzureCredential. Prefer a managed identity credential in production.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the key filter, e.g. <c>"MyApp:*"</c>. Defaults to all keys.
    /// </summary>
    public string KeyFilter { get; set; } = "*";

    /// <summary>
    /// Gets the labels to load, in order. When a key exists under several labels, the later label wins,
    /// so <c>{ NoLabel, "Production" }</c> loads shared defaults then production overrides.
    /// Defaults to settings with no label.
    /// </summary>
    public IList<string> Labels { get; } = new List<string> { NoLabel };

    /// <summary>
    /// Gets or sets the name of an App Configuration snapshot to load. Snapshots are immutable,
    /// point-in-time sets of settings, ideal for pinning a deployment to a known configuration.
    /// When set, <see cref="KeyFilter"/> and <see cref="Labels"/> are ignored.
    /// </summary>
    public string? SnapshotName { get; set; }

    /// <summary>
    /// Gets the key prefixes to remove from loaded keys, e.g. <c>"MyApp:"</c> so that
    /// <c>MyApp:Database:Host</c> becomes <c>Database:Host</c>. The first matching prefix is removed.
    /// </summary>
    public IList<string> TrimKeyPrefixes { get; } = new List<string>();

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether Key Vault references are resolved to their secret values.
    /// When false, Key Vault references are skipped. Defaults to true.
    /// </summary>
    public bool ResolveKeyVaultReferences { get; set; } = true;

    /// <summary>
    /// Gets or sets a factory that creates the Key Vault client for a vault URI when resolving
    /// Key Vault references. Defaults to a client using <see cref="Credential"/>.
    /// </summary>
    public Func<Uri, SecretClient>? SecretClientFactory { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of Key Vault references resolved concurrently. Defaults to 8.
    /// </summary>
    public int MaxConcurrentSecretResolutions { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether feature flags are loaded. Flags are exposed under <c>FeatureManagement:</c>
    /// in the schema read by Microsoft.FeatureManagement. Defaults to false.
    /// </summary>
    public bool IncludeFeatureFlags { get; set; }

    /// <summary>
    /// Gets or sets a sentinel key. When set, a reload first checks only this key and reloads
    /// everything else only if it changed, so frequent refresh stays cheap. Update the sentinel
    /// after changing other settings to publish them together.
    /// </summary>
    public string? SentinelKey { get; set; }

    /// <summary>
    /// Gets or sets the label of the sentinel key. Defaults to no label.
    /// </summary>
    public string? SentinelLabel { get; set; }
}
