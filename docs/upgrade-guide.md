# Upgrade Guide: 0.2.0-rc.5 → 0.3.0-rc.1

Most applications upgrade without code changes. This page lists the changes that can affect existing code, and the new recommended setup. The full list is in the [changelog](../CHANGELOG.md).

## Behaviour changes to check

| Change | What to check |
|---|---|
| **Keys are case-insensitive** | Two sources with keys differing only in case (`Timeout` vs `TIMEOUT`) are now the same key, and the higher-priority source wins. |
| **Invariant culture** | Values must use invariant formats: `1.5`, not `1,5`. The same applies to dates. |
| **Environment and JSON `__` → `:` alias** | Keys from environment variables and JSON files that use `A__B` are now also available as `A:B`. If you already set both, the explicit `A:B` wins. |
| **Disabled / expired Key Vault secrets are skipped** | If you relied on loading an expired or disabled secret, re-enable or renew it. |
| **Errors no longer contain values** | Code or tests that parsed values out of binding error messages need updating. |
| **One binder for the typed APIs** | `BuildAsync<T>()`, `BuildOptionalAsync<T>()`, `Bind<T>()` and the dictionary binding helpers now use `EnhancedConfigurationBinder`. They now also bind collections, dictionaries and records, and they **run Data Annotations validation**: a type with `[Required]` or `[Range]` attributes can now fail to bind where it didn't before. Error messages start with `[BIND ERROR]`. |
| **Nested `:` keys are bound** | The typed APIs now bind nested properties from `Database:Host` as well as `Database__Host`. Nested Key Vault and App Configuration values that were silently ignored before are now bound, and override your class defaults. |
| **Enhanced binder matches full key paths** | `EnhancedConfigurationBinder.Bind<T>()` binds a property only from its exact path (`Database:Name`), no longer from a root key with the same name or a key with the separators elsewhere. Missing keys now keep class defaults instead of resetting them (set `BindingOptions.IgnoreMissingOptional = false` for the old behaviour). |
| **Custom sources that throw** | `BuildAsync()` now returns an error result instead of throwing, so check `IsFailure`. |
| **Analyzers** | FAZ0001–FAZ0004 report warnings. With `TreatWarningsAsErrors`, fix them or set severities in `.editorconfig` ([docs](analyzers.md)). |
| **Trimming/AOT warnings** | Projects with trimming or AOT analyzers enabled now see IL2026/IL3050 on reflection-based binding APIs, which are unsafe in those modes. See [Native AOT](configuration-integration.md#native-aot). |
| **`ConfigurationBinder` is obsolete** | Replace direct calls to `ConfigurationBinder.Bind<T>()` with `EnhancedConfigurationBinder.Bind<T>()`. |
| **Build-once DI registrations are obsolete** | `services.AddFluentAzure<T>()`, `AddFluentAzureAsync<T>()`, `AddFluentAzureOptional<T>()`, `AddFluentAzureWithFallback<T>()`, `AddFluentAzureConditional<T>()` and the `FluentConfig.AddFluentAzure*<T>()` wrappers produce a CS0618 warning and will be removed in 1.0. Use the options pattern (below). |
| **`KeyVaultConfiguration.ReloadFailedSecrets` is obsolete** | Remove it: it never had an effect. |

## Dependency changes

- **Microsoft.Extensions.\* 10.0.x:** the minimum is now 10.0.x on both net8.0 and net10.0, because the current Azure SDK requires it. These packages support net8.0.
- **Transitive packages removed:** FluentAzure no longer brings in `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Caching.Memory` or `Azure.Extensions.AspNetCore.Configuration.Secrets`. If your app used any of them without referencing it directly, add a `PackageReference`. ASP.NET Core and Functions apps already get Hosting from their framework.
- **New dependency:** `Microsoft.Extensions.Diagnostics.HealthChecks`.

## Replacing `AddFluentAzure<T>()`

The build-once registrations are obsolete. For ASP.NET Core, Functions and workers, move to the options pattern. If you load configuration with Microsoft's providers, add the [configuration guard](configuration-guard.md) for startup validation. If you use FluentAzure's own sources, feed them in with the provider, which adds reload, `IOptionsMonitor<T>`, health checks and AOT support:

**Before**
```csharp
builder.Services.AddFluentAzure<AppSettings>(config => config
    .FromEnvironment()
    .FromKeyVault(vaultUrl)
    .Required("Database:ConnectionString"));
```

**After**
```csharp
await builder.Configuration.AddFluentAzureAsync(
    fluent => fluent
        .UseManagedIdentity()
        .FromEnvironment()
        .FromKeyVault(vaultUrl)
        .Required("Database:ConnectionString"),
    reloadInterval: TimeSpan.FromMinutes(15));

builder.Services.AddFluentAzureOptions<AppSettings>(builder.Configuration);   // binds from the root
builder.Services.AddHealthChecks().AddFluentAzure();
```

Consumers then inject `IOptions<AppSettings>`, or `IOptionsMonitor<AppSettings>` to see reloaded values, instead of `AppSettings`.

Options binding also supports collections and dictionaries, which the basic binder used by the obsolete `AddFluentAzure<T>` does not.

## Also worth adopting

- **Identity:** use `UseManagedIdentity()` or `UseWorkloadIdentity()` instead of relying on `DefaultAzureCredential` in production. ([Identity & secrets](identity-and-redaction.md))
- **Debug output:** use `GetRedactedDebugView()` instead of `GetDebugView()` when logging configuration.
- **App Configuration:** store non-secret settings and feature flags there, with Key Vault references for secrets. ([App Configuration](app-configuration-source.md))
