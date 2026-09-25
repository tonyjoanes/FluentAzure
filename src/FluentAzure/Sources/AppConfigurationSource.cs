using System.Collections.Concurrent;
using System.Text.Json;
using Azure;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FluentAzure.Core;
using Microsoft.Extensions.Logging;

namespace FluentAzure.Sources;

/// <summary>
/// Configuration source that loads settings from Azure App Configuration, with label layering,
/// snapshots, Key Vault reference resolution, feature flags and sentinel-key based refresh.
/// </summary>
public class AppConfigurationSource : IReloadableConfigurationSource, ISensitiveConfigurationSource
{
    private const string FeatureFlagKeyFilter = ".appconfig.featureflag/*";
    private const string FeatureManagementSection = "FeatureManagement";

    private readonly ConfigurationClient _client;
    private readonly AppConfigurationOptions _options;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly ConcurrentDictionary<Uri, SecretClient> _secretClients = new();
    private volatile Dictionary<string, string>? _values;
    private volatile HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase);
    private ETag? _sentinelETag;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppConfigurationSource"/> class using an endpoint
    /// and Microsoft Entra ID authentication (recommended).
    /// </summary>
    /// <param name="endpoint">The App Configuration endpoint, e.g. <c>https://myconfig.azconfig.io</c>.</param>
    /// <param name="options">The source options.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="logger">Optional logger.</param>
    public AppConfigurationSource(
        Uri endpoint,
        AppConfigurationOptions? options = null,
        int priority = 150,
        ILogger? logger = null
    )
        : this(
            new ConfigurationClient(
                endpoint ?? throw new ArgumentNullException(nameof(endpoint)),
                options?.Credential ?? new DefaultAzureCredential()
            ),
            options,
            priority,
            logger
        ) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppConfigurationSource"/> class using a connection string.
    /// Prefer the endpoint overload with a managed identity in production.
    /// </summary>
    /// <param name="connectionString">The App Configuration connection string.</param>
    /// <param name="options">The source options.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="logger">Optional logger.</param>
    public AppConfigurationSource(
        string connectionString,
        AppConfigurationOptions? options = null,
        int priority = 150,
        ILogger? logger = null
    )
        : this(new ConfigurationClient(connectionString), options, priority, logger) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppConfigurationSource"/> class with an existing client.
    /// </summary>
    /// <param name="client">The App Configuration client.</param>
    /// <param name="options">The source options.</param>
    /// <param name="priority">The priority of this configuration source.</param>
    /// <param name="logger">Optional logger.</param>
    public AppConfigurationSource(
        ConfigurationClient client,
        AppConfigurationOptions? options = null,
        int priority = 150,
        ILogger? logger = null
    )
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new AppConfigurationOptions();
        _logger = logger;
        Priority = priority;
    }

    /// <inheritdoc />
    public string Name => "AppConfiguration";

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> LoadAsync()
    {
        if (_values is { } cached)
        {
            return Result<Dictionary<string, string>>.Success(Copy(cached));
        }

        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _values is { } loaded
                ? Result<Dictionary<string, string>>.Success(Copy(loaded))
                : await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Reloads settings from App Configuration. When a sentinel key is configured and has not
    /// changed, the previously loaded values are returned without reloading everything.
    /// </summary>
    /// <returns>A task that represents the asynchronous reload operation. The task result contains the configuration values.</returns>
    public async Task<Result<Dictionary<string, string>>> ReloadAsync()
    {
        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_values is { } current && _options.SentinelKey is not null)
            {
                var sentinel = await GetSentinelETagAsync().ConfigureAwait(false);
                if (sentinel == _sentinelETag)
                {
                    _logger?.LogDebug("App Configuration sentinel unchanged; skipping reload");
                    return Result<Dictionary<string, string>>.Success(Copy(current));
                }
            }

            return await LoadCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<Dictionary<string, string>>.Error(
                $"Failed to reload Azure App Configuration: {ex.Message}"
            );
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets the options this source was created with.
    /// </summary>
    internal AppConfigurationOptions Options => _options;

    /// <summary>
    /// Values resolved from Key Vault references are secrets; plain settings and feature flags are not.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>True if the key's value was resolved from a Key Vault reference.</returns>
    public bool IsSensitive(string key) => _sensitiveKeys.Contains(key);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _values?.ContainsKey(key) ?? false;

    /// <inheritdoc />
    public string? GetValue(string key) =>
        _values is { } values && values.TryGetValue(key, out var value) ? value : null;

    private async Task<Result<Dictionary<string, string>>> LoadCoreAsync()
    {
        try
        {
            // Read the sentinel first, so a change made while loading is caught by the next reload
            var sentinelETag = _options.SentinelKey is null
                ? (ETag?)null
                : await GetSentinelETagAsync().ConfigureAwait(false);

            var winners = await SelectSettingsAsync().ConfigureAwait(false);

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var secretReferences = new List<(string Key, Uri SecretId)>();

            foreach (var setting in winners)
            {
                switch (setting)
                {
                    case SecretReferenceConfigurationSetting secretReference:
                        if (_options.ResolveKeyVaultReferences)
                        {
                            secretReferences.Add((TrimKey(setting.Key), secretReference.SecretId));
                        }
                        break;

                    case FeatureFlagConfigurationSetting featureFlag:
                        AddFeatureFlag(values, featureFlag);
                        break;

                    default:
                        AddSetting(values, TrimKey(setting.Key), setting);
                        break;
                }
            }

            var errors = await ResolveSecretReferencesAsync(values, secretReferences)
                .ConfigureAwait(false);
            if (errors.Count > 0)
            {
                return Result<Dictionary<string, string>>.Error(errors);
            }

            _values = values;
            _sensitiveKeys = new HashSet<string>(
                secretReferences.Select(r => r.Key),
                StringComparer.OrdinalIgnoreCase
            );
            _sentinelETag = sentinelETag;
            _logger?.LogInformation(
                "Loaded {Count} values from Azure App Configuration",
                values.Count
            );
            return Result<Dictionary<string, string>>.Success(Copy(values));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Azure App Configuration load failed");
            return Result<Dictionary<string, string>>.Error(
                $"Failed to load Azure App Configuration: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Fetches the selected settings, keeping only the last one seen per key so later labels override earlier ones.
    /// </summary>
    private async Task<IEnumerable<ConfigurationSetting>> SelectSettingsAsync()
    {
        var winners = new Dictionary<string, ConfigurationSetting>(StringComparer.Ordinal);

        async Task CollectAsync(AsyncPageable<ConfigurationSetting> settings)
        {
            await foreach (var setting in settings.ConfigureAwait(false))
            {
                if (setting is FeatureFlagConfigurationSetting && !_options.IncludeFeatureFlags)
                {
                    continue;
                }

                winners[setting.Key] = setting;
            }
        }

        if (!string.IsNullOrEmpty(_options.SnapshotName))
        {
            await CollectAsync(_client.GetConfigurationSettingsForSnapshotAsync(_options.SnapshotName))
                .ConfigureAwait(false);
            return winners.Values;
        }

        var labels = _options.Labels.Count > 0 ? _options.Labels : new[] { AppConfigurationOptions.NoLabel };
        foreach (var label in labels)
        {
            await CollectAsync(
                    _client.GetConfigurationSettingsAsync(
                        new SettingSelector { KeyFilter = _options.KeyFilter, LabelFilter = label }
                    )
                )
                .ConfigureAwait(false);

            if (_options.IncludeFeatureFlags)
            {
                await CollectAsync(
                        _client.GetConfigurationSettingsAsync(
                            new SettingSelector { KeyFilter = FeatureFlagKeyFilter, LabelFilter = label }
                        )
                    )
                    .ConfigureAwait(false);
            }
        }

        return winners.Values;
    }

    private async Task<List<string>> ResolveSecretReferencesAsync(
        Dictionary<string, string> values,
        List<(string Key, Uri SecretId)> references
    )
    {
        var errors = new ConcurrentBag<string>();
        var resolved = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await Parallel
            .ForEachAsync(
                references,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrentSecretResolutions),
                },
                async (reference, cancellationToken) =>
                {
                    try
                    {
                        var identifier = new KeyVaultSecretIdentifier(reference.SecretId);
                        var client = _secretClients.GetOrAdd(identifier.VaultUri, CreateSecretClient);
                        var secret = await client
                            .GetSecretAsync(identifier.Name, identifier.Version, cancellationToken)
                            .ConfigureAwait(false);
                        resolved[reference.Key] = secret.Value.Value ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        // Report the secret identifier, never a value
                        errors.Add(
                            $"Failed to resolve Key Vault reference for '{reference.Key}' ({reference.SecretId}): {ex.Message}"
                        );
                    }
                }
            )
            .ConfigureAwait(false);

        foreach (var kvp in resolved)
        {
            values[kvp.Key] = kvp.Value;
        }

        return errors.OrderBy(e => e, StringComparer.Ordinal).ToList();
    }

    private SecretClient CreateSecretClient(Uri vaultUri) =>
        _options.SecretClientFactory?.Invoke(vaultUri)
        ?? new SecretClient(vaultUri, _options.Credential ?? new DefaultAzureCredential());

    private async Task<ETag?> GetSentinelETagAsync()
    {
        try
        {
            var response = await _client
                .GetConfigurationSettingAsync(_options.SentinelKey!, _options.SentinelLabel)
                .ConfigureAwait(false);
            return response.Value.ETag;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // A missing sentinel is a valid state; creating it later triggers a reload
            return null;
        }
    }

    private string TrimKey(string key)
    {
        foreach (var prefix in _options.TrimKeyPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return key[prefix.Length..];
            }
        }

        return key;
    }

    private static void AddSetting(
        Dictionary<string, string> values,
        string key,
        ConfigurationSetting setting
    )
    {
        var value = setting.Value ?? string.Empty;
        if (IsJsonContentType(setting.ContentType))
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                Flatten(values, key, document.RootElement);
                return;
            }
            catch (JsonException)
            {
                // Not valid JSON despite the content type; fall back to the raw value
            }
        }

        values[key] = value;
    }

    /// <summary>
    /// Maps a feature flag to the Microsoft.FeatureManagement configuration schema:
    /// <c>FeatureManagement:{id}</c> = true/false, or <c>EnabledFor</c> filters when conditional.
    /// </summary>
    private static void AddFeatureFlag(
        Dictionary<string, string> values,
        FeatureFlagConfigurationSetting featureFlag
    )
    {
        var flagKey = $"{FeatureManagementSection}:{featureFlag.FeatureId}";
        if (!featureFlag.IsEnabled || featureFlag.ClientFilters.Count == 0)
        {
            values[flagKey] = featureFlag.IsEnabled ? "true" : "false";
            return;
        }

        for (var i = 0; i < featureFlag.ClientFilters.Count; i++)
        {
            var filter = featureFlag.ClientFilters[i];
            var filterKey = $"{flagKey}:EnabledFor:{i}";
            values[$"{filterKey}:Name"] = filter.Name;

            foreach (var parameter in filter.Parameters)
            {
                FlattenObject(values, $"{filterKey}:Parameters:{parameter.Key}", parameter.Value);
            }
        }
    }

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';')[0].Trim();
        if (mediaType.StartsWith("application/vnd.microsoft.appconfig.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static void Flatten(Dictionary<string, string> values, string prefix, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(values, $"{prefix}:{property.Name}", property.Value);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(values, $"{prefix}:{index++}", item);
                }
                break;

            case JsonValueKind.String:
                values[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.True:
                values[prefix] = "true";
                break;

            case JsonValueKind.False:
                values[prefix] = "false";
                break;

            case JsonValueKind.Null:
                values[prefix] = string.Empty;
                break;

            default:
                values[prefix] = element.GetRawText();
                break;
        }
    }

    /// <summary>
    /// Flattens a feature filter parameter value (a <see cref="JsonElement"/>, primitive, dictionary or list)
    /// without reflection-based serialization, so the source stays trimming and Native AOT safe.
    /// </summary>
    private static void FlattenObject(Dictionary<string, string> values, string prefix, object? value)
    {
        switch (value)
        {
            case null:
                values[prefix] = string.Empty;
                break;

            case JsonElement element:
                Flatten(values, prefix, element);
                break;

            case string text:
                values[prefix] = text;
                break;

            case bool flag:
                values[prefix] = flag ? "true" : "false";
                break;

            case IDictionary<string, object?> dictionary:
                foreach (var kvp in dictionary)
                {
                    FlattenObject(values, $"{prefix}:{kvp.Key}", kvp.Value);
                }
                break;

            case System.Collections.IEnumerable items:
                var index = 0;
                foreach (var item in items)
                {
                    FlattenObject(values, $"{prefix}:{index++}", item);
                }
                break;

            case IFormattable formattable:
                values[prefix] = formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
                break;

            default:
                values[prefix] = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static Dictionary<string, string> Copy(Dictionary<string, string> values) =>
        new(values, StringComparer.OrdinalIgnoreCase);
}
