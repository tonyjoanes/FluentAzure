namespace FluentAzure.Guard;

/// <summary>
/// The rules a <see cref="ConfigurationGuard"/> checks against the application's final
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>, whichever providers supplied it:
/// Microsoft's Azure App Configuration and Key Vault providers, JSON, environment variables or FluentAzure's own.
/// </summary>
public sealed class ConfigurationGuardOptions
{
    private readonly List<string> _required = new();
    private readonly List<ValueRule> _rules = new();
    private readonly List<string> _sensitive = new();

    /// <summary>
    /// Gets the keys that must be present with a non-empty value, or as a section with children.
    /// </summary>
    public IReadOnlyList<string> RequiredKeys => _required;

    /// <summary>
    /// Gets the key patterns whose values are masked by <c>GetRedactedDebugView()</c>.
    /// </summary>
    public IReadOnlyList<string> SensitivePatterns => _sensitive;

    /// <summary>
    /// Gets the value rules to check.
    /// </summary>
    internal IReadOnlyList<ValueRule> Rules => _rules;

    /// <summary>
    /// Requires each key to be present with a non-empty value, or as a section with children.
    /// Keys may use ":" or "__" as the separator and are matched case-insensitively.
    /// </summary>
    /// <param name="keys">The required keys, e.g. <c>Database:ConnectionString</c>.</param>
    /// <returns>These options, for chaining.</returns>
    public ConfigurationGuardOptions Required(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            _required.Add(ConfigurationGuard.NormalizeKey(key));
        }

        return this;
    }

    /// <summary>
    /// Adds a rule for a key's value. The rule is checked only when the key has a value; combine it with
    /// <see cref="Required"/> to also require the key.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <param name="isValid">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="requirement">
    /// What the value must satisfy, e.g. "must be an absolute https URI". It is reported with the key name.
    /// Never include the value itself: failures are shown at startup and in logs.
    /// </param>
    /// <returns>These options, for chaining.</returns>
    public ConfigurationGuardOptions Validate(string key, Func<string, bool> isValid, string requirement)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(isValid);
        ArgumentException.ThrowIfNullOrEmpty(requirement);
        _rules.Add(new ValueRule(ConfigurationGuard.NormalizeKey(key), isValid, requirement));
        return this;
    }

    /// <summary>
    /// Marks keys as sensitive, so <c>GetRedactedDebugView()</c> masks their values. A pattern is either an
    /// exact key or a section followed by <c>*</c> (e.g. <c>Payments:*</c>), matched case-insensitively.
    /// </summary>
    /// <param name="patterns">The key patterns.</param>
    /// <returns>These options, for chaining.</returns>
    public ConfigurationGuardOptions Sensitive(params string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        foreach (var pattern in patterns)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern);
            _sensitive.Add(ConfigurationGuard.NormalizeKey(pattern));
        }

        return this;
    }

    /// <summary>
    /// A rule for one key's value.
    /// </summary>
    /// <param name="Key">The normalized key.</param>
    /// <param name="IsValid">The predicate.</param>
    /// <param name="Requirement">What the value must satisfy.</param>
    internal sealed record ValueRule(string Key, Func<string, bool> IsValid, string Requirement);
}
