# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository.

## What this is

FluentAzure is a NuGet library that provides a fluent, `Result<T>`-based pipeline for .NET configuration. It loads from:
- environment variables;
- JSON files;
- Azure Key Vault;
- Azure App Configuration (labels, snapshots, Key Vault references, feature flags, sentinel refresh);
- custom sources.

The pipeline enforces required keys, transforms and validations. It can be used on its own (`FluentConfig.Create()...BuildAsync()`) or as a `Microsoft.Extensions.Configuration` provider (`AddFluentAzure()`), which enables `IOptionsMonitor<T>` reload. It targets **net8.0 and net10.0** and is **trimming/Native AOT compatible**.

## Layout

| Path | Contents |
|---|---|
| `src/FluentAzure/Core` | `ConfigurationBuilder` (the pipeline), `Result<T>`, `Option<T>`, source interfaces, `PipelineCredential` |
| `src/FluentAzure/Sources` | Environment, JSON, in-memory, Key Vault and App Configuration sources |
| `src/FluentAzure/Configuration` | `IConfiguration` provider/source, `FluentAzureHealthCheck` |
| `src/FluentAzure/Binding` | Reflection-based binders (annotated `RequiresUnreferencedCode`/`RequiresDynamicCode`) |
| `src/FluentAzure/FluentAzureDiagnostics.cs` | OpenTelemetry `ActivitySource`/`Meter` named `FluentAzure` |
| `src/FluentAzure.Analyzers` | Roslyn analyzers FAZ0001–FAZ0004 (netstandard2.0, Roslyn 4.8), packed into the FluentAzure package |
| `tests/FluentAzure.Tests` | xUnit tests (net8.0 + net10.0); fakes in `TestDoubles/`; emulator tests in `Integration/` |
| `tests/FluentAzure.Analyzers.Tests` | Analyzer tests using `Microsoft.CodeAnalysis.Testing` |
| `examples/` | Demo, WebApi, Azure Functions, and `Aot.Example` (published with Native AOT in CI) |

## Build and test

```bash
dotnet restore --source https://api.nuget.org/v3/index.json   # see note below
dotnet build FluentAzure.sln -c Release
dotnet test FluentAzure.sln -c Release
```

- `nuget.config` also lists a GitHub Packages feed. Where that feed is unreachable (e.g. sandboxes), a plain `dotnet restore` can hang. Pass `--source https://api.nuget.org/v3/index.json` to avoid it.
- Both the .NET 8 and .NET 10 SDKs/runtimes are needed; tests run on both targets.
- App Configuration emulator tests are skipped unless `FLUENTAZURE_APPCONFIG_EMULATOR` is set. See `CONTRIBUTING.md` for the `docker run` command. The emulator needs an HMAC access key (`Tenant__AccessKeys__0__*`); anonymous mode does not work with SDK connection strings.
- Native AOT check (as in CI): `dotnet publish examples/Aot.Example -c Release -r linux-x64` must produce no `warning IL....`.
- Before adding or updating packages, run `dotnet list <project> package --vulnerable --include-transitive`. Snyk scans every project, including the examples.

## Rules that must hold

- **Never put configuration values in errors, logs, telemetry or health data.** Name the key and the type instead. Parser exception messages can echo the input (`int.Parse` does on .NET 8+), so don't pass `ex.Message` through for conversion failures. Tests assert this with a fake secret; keep them passing.
- **Keys are case-insensitive.** Use `StringComparer.OrdinalIgnoreCase` for configuration dictionaries.
- **Hierarchy separators:** keys use `:` in the `IConfiguration` provider. `__` (from environment variables and the JSON source) is also accepted and mapped to `:`.
- **Parsing and formatting use `CultureInfo.InvariantCulture`.**
- **Async:** library code uses `ConfigureAwait(false)`. Sync-over-async entry points go through `Task.Run(...)` so they cannot deadlock.
- **AOT:** the library builds with `IsAotCompatible`. New reflection must be annotated (`RequiresUnreferencedCode`/`RequiresDynamicCode` using `AotMessages.ReflectionBinding`) or avoided. Never use reflection-based `System.Text.Json` serialization in sources.
- **Identity:** Azure sources default to the pipeline credential (`ConfigurationBuilder.Credential`); per-source credentials take precedence. Use `ManagedIdentityId`, not the obsolete string constructor.
- **Failure model:** sources return `Result<T>` errors rather than throwing. The pipeline also converts exceptions from custom sources into errors. The initial provider load fails fast (`FluentAzureConfigurationException`); failed reloads keep the last good values.

## Adding a configuration source

1. Implement `FluentAzure.Core.IConfigurationSource`. `Name` is used as a telemetry tag, so keep it low-cardinality and free of secrets.
2. Optionally implement `IReloadableConfigurationSource` (the source caches values and can refetch them) and/or `ISensitiveConfigurationSource` (values are secrets and should be masked by `GetRedactedDebugView()`).
3. Add a `From...()` method on `ConfigurationBuilder`. If it talks to Azure, default its credential to the pipeline credential.
4. Test it with fakes of the Azure SDK client (see `tests/FluentAzure.Tests/TestDoubles/AzureFakes.cs`).

## Testing gotchas

- `Azure.Security.KeyVault.Secrets` 4.11 has two `GetSecretAsync` overloads, and one-argument calls bind to the newer one. Fakes must override both (`FakeVaultClient` does).
- The test project has a `FluentAzure.Tests.Core` namespace, which shadows `Core.ConfigurationBuilder`. Alias it instead: `using FluentPipeline = FluentAzure.Core.ConfigurationBuilder;` (and `MsConfigurationBuilder` for Microsoft's builder).
- Tests run in parallel. Telemetry tests must filter captured activities and measurements by a unique source name.
- Analyzer tests compile snippets against .NET 10 reference assemblies plus the real FluentAzure, Azure SDK and Microsoft.Extensions assemblies. Expected diagnostics are marked `{|FAZ0001:...|}`.

## Style

- StyleCop and .NET analyzers are enabled (`Directory.Build.props`, `FluentAzure.ruleset`).
- Follow the surrounding code: XML docs on public APIs and Arrange/Act/Assert comments in tests.
- Update `README.md` and `docs/` when public behaviour changes, and list breaking or behaviour changes in the PR description for release notes.
