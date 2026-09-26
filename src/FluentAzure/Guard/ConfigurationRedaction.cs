using FluentAzure.Configuration;
using Microsoft.Extensions.Configuration;

namespace FluentAzure.Guard;

/// <summary>
/// Builds debug views of configuration with secret values masked.
/// </summary>
internal static class ConfigurationRedaction
{
    public static string GetRedactedDebugView(IConfigurationRoot root, ConfigurationGuard? guard, string mask)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(mask);

        return root.GetDebugView(context =>
            context.Value is null ? string.Empty
            : IsSecret(context.Path, context.Value, context.ConfigurationProvider, guard) ? mask
            : context.Value
        );
    }

    private static bool IsSecret(string path, string value, IConfigurationProvider provider, ConfigurationGuard? guard) =>
        (provider is FluentAzureConfigurationProvider fluentProvider && fluentProvider.IsSensitive(path))
        || IsMicrosoftKeyVaultProvider(provider)
        || (guard?.IsSensitive(path) ?? false)
        || SecretHeuristics.IsSensitiveKey(path)
        || SecretHeuristics.IsSecretValue(value);

    // Matched by name so FluentAzure does not depend on Azure.Extensions.AspNetCore.Configuration.Secrets
    private static bool IsMicrosoftKeyVaultProvider(IConfigurationProvider provider) =>
        string.Equals(
            provider.GetType().FullName,
            "Azure.Extensions.AspNetCore.Configuration.Secrets.AzureKeyVaultConfigurationProvider",
            StringComparison.Ordinal
        );
}
