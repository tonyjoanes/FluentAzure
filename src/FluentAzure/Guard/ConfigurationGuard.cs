using FluentAzure.Configuration;
using Microsoft.Extensions.Configuration;

namespace FluentAzure.Guard;

/// <summary>
/// Checks the application's final <see cref="IConfiguration"/> against required keys and value rules, and
/// knows which keys are sensitive. It works with any configuration provider, including Microsoft's Azure App
/// Configuration and Key Vault providers. Failures name the key and the requirement, never the value.
/// </summary>
public sealed class ConfigurationGuard
{
    private readonly ConfigurationGuardOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationGuard"/> class.
    /// </summary>
    /// <param name="options">The rules to check.</param>
    public ConfigurationGuard(ConfigurationGuardOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the rules this guard checks.
    /// </summary>
    public ConfigurationGuardOptions Options => _options;

    /// <summary>
    /// Checks the configuration and returns every failure. An empty list means the configuration is valid.
    /// </summary>
    /// <param name="configuration">The configuration to check.</param>
    /// <returns>The failures, each naming the key and what is wrong, never the value.</returns>
    public IReadOnlyList<string> Check(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var failures = new List<string>();

        foreach (var key in _options.RequiredKeys)
        {
            if (string.IsNullOrEmpty(configuration[key]) && !configuration.GetSection(key).GetChildren().Any())
            {
                failures.Add($"Configuration key '{key}' is required but is missing or empty.");
            }
        }

        foreach (var rule in _options.Rules)
        {
            var value = configuration[rule.Key];
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            bool valid;
            try
            {
                valid = rule.IsValid(value);
            }
            catch (Exception ex)
            {
                // The exception message could echo the value, so only report its type
                failures.Add($"Configuration key '{rule.Key}' could not be validated ({ex.GetType().Name}).");
                continue;
            }

            if (!valid)
            {
                failures.Add($"Configuration key '{rule.Key}' {rule.Requirement}.");
            }
        }

        return failures;
    }

    /// <summary>
    /// Checks the configuration and throws if any rule fails. Use this where there is no host to run the
    /// startup validation registered by <c>AddFluentAzureGuard()</c>.
    /// </summary>
    /// <param name="configuration">The configuration to check.</param>
    /// <exception cref="FluentAzureConfigurationException">One or more rules failed; see its errors.</exception>
    public void ThrowIfInvalid(IConfiguration configuration)
    {
        var failures = Check(configuration);
        if (failures.Count > 0)
        {
            throw new FluentAzureConfigurationException(failures);
        }
    }

    /// <summary>
    /// Determines whether a key matches one of the patterns declared with
    /// <see cref="ConfigurationGuardOptions.Sensitive"/>.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns><see langword="true"/> if the key's value should be masked.</returns>
    public bool IsSensitive(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalized = NormalizeKey(key);
        foreach (var pattern in _options.SensitivePatterns)
        {
            if (pattern.EndsWith('*')
                ? normalized.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(normalized, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Produces the same output as <c>GetRedactedDebugView()</c>, and also masks the keys declared sensitive
    /// on this guard.
    /// </summary>
    /// <param name="root">The configuration root.</param>
    /// <param name="mask">The text shown in place of secret values.</param>
    /// <returns>A human-readable view of the configuration with secrets masked.</returns>
    public string GetRedactedDebugView(IConfigurationRoot root, string mask = "***") =>
        ConfigurationRedaction.GetRedactedDebugView(root, this, mask);

    /// <summary>
    /// Converts the "__" separator used by environment variables to ":".
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The key with ":" separators.</returns>
    internal static string NormalizeKey(string key) => key.Replace("__", ":", StringComparison.Ordinal);
}
