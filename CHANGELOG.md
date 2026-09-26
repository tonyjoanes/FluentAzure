# Changelog

All notable changes to FluentAzure are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

Upgrading from 0.2.0-rc.5? See the [upgrade guide](docs/upgrade-guide.md).

## [Unreleased]

### Added

- **Microsoft.Extensions.Configuration provider:** use any FluentAzure pipeline as a configuration source with `IConfigurationBuilder.AddFluentAzure(...)` / `AddFluentAzureAsync(...)`. Values flow into `IConfiguration`, `IOptions<T>` and `IOptionsMonitor<T>`. [Docs](docs/configuration-integration.md)
  - **Fail fast:** the initial load throws `FluentAzureConfigurationException`, listing every error.
  - **Reload:** periodic reload via `reloadInterval`. Change tokens fire only when values change, and a failed reload keeps the last good values (`OnReloadError`, `LastReloadErrors`).
  - **Options:** `services.AddFluentAzureOptions<T>(configuration, section)` binds a section, with data-annotation validation and `ValidateOnStart`.
- **Azure App Configuration source:** `FromAppConfiguration(...)` supports label layering, key filters and prefix trimming, snapshots, Key Vault reference resolution, feature flags (Microsoft.FeatureManagement schema), JSON content-type flattening, and sentinel-key refresh. [Docs](docs/app-configuration-source.md)
- **Pipeline identity:** `UseManagedIdentity()`, `UseWorkloadIdentity()` and `UseCredential(...)` set one shared credential for all Azure sources. [Docs](docs/identity-and-redaction.md)
- **Secret tracking and redaction:** Key Vault values and resolved Key Vault references are tracked as sensitive, `.Sensitive("Key")` marks others, and `IConfigurationRoot.GetRedactedDebugView()` masks them.
- **OpenTelemetry:** traces and metrics through an `ActivitySource` and `Meter` named `FluentAzure`. [Docs](docs/observability.md)
- **Health check:** `IHealthChecksBuilder.AddFluentAzure()` reports failed reloads and stale configuration.
- **Roslyn analyzers, shipped in the package:** [Docs](docs/analyzers.md)
  - FAZ0001: a `Required` key the `BuildAsync<T>()` type never binds.
  - FAZ0002: a non-HTTPS endpoint.
  - FAZ0003: a hard-coded connection-string secret.
  - FAZ0004: `GetDebugView()`.
- **Key Vault additions:**
  - `new KeyVaultSource(SecretClient)`, to use a custom or test client.
  - `KeyVaultConfiguration.MaxConcurrentSecretLoads`.
- **Asynchronous DI registration:** `services.AddFluentAzureAsync<T>(...)`.
- **New source interfaces:** `IReloadableConfigurationSource` and `ISensitiveConfigurationSource`, for custom sources.
- **.NET 10:** the package targets both `net8.0` and `net10.0`.
- **Native AOT:** the library is trimming and Native AOT compatible, with `examples/Aot.Example`.
- **IntelliSense:** XML documentation now ships in the package.

### Changed

- **Case-insensitive keys:** configuration keys are now case-insensitive throughout the pipeline.
- **Environment variables:** variables using `__` are also exposed with `:` (`ConnectionStrings__Default` → `ConnectionStrings:Default`).
- **Invariant culture:** typed `Optional<T>` defaults and all binders parse and format numbers and dates with the invariant culture.
- **Key Vault loading:**
  - Disabled, expired and not-yet-active secrets are skipped.
  - Secrets are fetched with bounded concurrency.
  - Concurrent loads share a single load.
- **No values in errors:** binding and conversion errors no longer include configuration values.
- **Custom sources that throw:** exceptions from custom sources become pipeline errors naming the source and exception type, instead of propagating out of `BuildAsync`.
- **Shared credential:** Azure sources share one `DefaultAzureCredential` per pipeline instead of creating one each.
- **AOT annotations:** reflection-based binding APIs (`BuildAsync<T>`, `Bind<T>`, `AddFluentAzure<T>`, `AddFluentAzureOptions<T>`, the binder classes) are annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.
- **Dependencies:** Azure.Identity 1.21.0, Azure.Security.KeyVault.Secrets 4.11.1, Azure.Data.AppConfiguration 1.11.1, Polly 8.8.0 and Microsoft.Extensions.* 10.0.12. The current Azure SDK requires the 10.x Extensions packages, even on net8.0.
- **New dependency:** `Microsoft.Extensions.Diagnostics.HealthChecks`.

### Removed

- **Unused transitive dependencies:** `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Caching.Memory` and `Azure.Extensions.AspNetCore.Configuration.Secrets`.
- **"Secure overwrite" disposal code and claims:** this code had no effect, because .NET strings are immutable. Disposal now just drops references.

### Fixed

- **Deadlock:** the synchronous `AddFluentAzure*` registrations could deadlock under a synchronization context.
- **Key Vault race:** concurrent `KeyVaultSource.LoadAsync` / `ReloadAsync` calls could both run a full load.
- **Culture:** values such as `1.5` were misread in cultures that use a decimal comma.
- **XML docs:** the XML documentation file was generated as `.xml` and so was missing from the package.

### Security

- **Vulnerable packages:** SourceLink updated to avoid a vulnerable `Microsoft.Build.Tasks.Git`, and EF Core in the WebApi example updated to pull in a patched `Microsoft.Extensions.Caching.Memory` (GHSA-qj66-m88j-hmgj).

### Known issues

- **Basic binder:** `BuildAsync<T>()` / `Bind<T>()` bind nested properties only from `__` keys, not `:` keys.
- **Enhanced binder:** it can't bind string lists/arrays (the bind fails), numeric arrays or dictionaries.
- **Workaround:** for both issues, use the `IConfiguration` provider with options binding. [Details](docs/enhanced-configuration-binding.md)

## [0.2.0-rc.5]

Baseline for this changelog: the fluent pipeline (environment, JSON, Key Vault and in-memory sources), `Result<T>` / `Option<T>`, strongly typed binding, and DI registration.
