# Azure App Configuration Source

FluentAzure can load settings from [Azure App Configuration](https://learn.microsoft.com/azure/azure-app-configuration/overview). The recommended setup is to keep ordinary settings and feature flags in App Configuration and keep secrets in Key Vault. App Configuration then stores only *references* to those secrets.

## Quick start

```csharp
using FluentAzure;

var config = await FluentConfig
    .Create()
    .FromEnvironment()
    .FromAppConfiguration("https://myconfig.azconfig.io", options =>
    {
        options.KeyFilter = "MyApp:*";
        options.TrimKeyPrefixes.Add("MyApp:");
        options.Labels.Add("Production");      // after the default no-label settings, so it overrides them
        options.IncludeFeatureFlags = true;
        options.SentinelKey = "MyApp:Sentinel";
    })
    .Required("Database:ConnectionString")
    .BuildAsync();
```

Passing an `https://` endpoint authenticates with Microsoft Entra ID (`options.Credential`, which defaults to `DefaultAzureCredential`; prefer `ManagedIdentityCredential` in production). You can also pass a connection string, but access keys are discouraged.

The default priority is `150`. That is above environment variables (`100`) and below Key Vault (`200`).

## Options

| Option | Default | Description |
|---|---|---|
| `Credential` | `DefaultAzureCredential` | Used for App Configuration and for resolving Key Vault references |
| `KeyFilter` | `*` | Key filter, e.g. `MyApp:*` |
| `Labels` | `[NoLabel]` | Labels loaded in order; later labels override earlier ones |
| `SnapshotName` | `null` | Load an immutable snapshot instead of key/label filters |
| `TrimKeyPrefixes` | empty | Prefixes removed from loaded keys |
| `ResolveKeyVaultReferences` | `true` | Resolve Key Vault references to secret values; when `false` they are skipped |
| `SecretClientFactory` | `null` | Custom `SecretClient` per vault URI (e.g. a per-vault credential) |
| `MaxConcurrentSecretResolutions` | `8` | Maximum number of Key Vault references resolved in parallel |
| `IncludeFeatureFlags` | `false` | Load feature flags into the `FeatureManagement` section |
| `SentinelKey` / `SentinelLabel` | `null` | Reload everything only when this key changes |

## Behaviour

- **Labels:** settings are loaded label by label. If a key exists under more than one label, the last label listed wins. Use `AppConfigurationOptions.NoLabel` for unlabelled settings.
- **Snapshots:** set `SnapshotName` to pin a deployment to an immutable, point-in-time set of settings.
- **JSON values:** a setting whose content type is `application/json` is flattened into hierarchical keys (`App:Cors:Origins:0`). Invalid JSON is kept as the raw string.
- **Key Vault references:** these are resolved in parallel, and a pinned secret version is honoured. If any reference cannot be resolved, the load fails. The error names the key and the secret identifier, never a secret value.
- **Feature flags:** flags are mapped to the schema Microsoft.FeatureManagement reads:
  - `FeatureManagement:{id}` is `true` or `false`.
  - A conditional flag becomes `FeatureManagement:{id}:EnabledFor:{n}:Name` plus `Parameters:*`.

## Refresh

Combine the source with the Microsoft.Extensions.Configuration integration to push changes to `IOptionsMonitor<T>`:

```csharp
await builder.Configuration.AddFluentAzureAsync(
    fluent => fluent.FromAppConfiguration(endpoint, o => o.SentinelKey = "MyApp:Sentinel"),
    reloadInterval: TimeSpan.FromSeconds(30));
```

When a sentinel key is set, each reload makes one request to check whether the sentinel has changed. Everything else, including Key Vault references, is reloaded only after the sentinel changes. Update the sentinel last, after changing other settings, so the changes are applied together.

Without a sentinel, every reload fetches all selected settings and resolves all Key Vault references again. This picks up rotated secrets, but costs more requests.
