# FluentAzure

A fluent, functional, and type-safe NuGet package for Azure configuration and secrets management.

[![NuGet](https://img.shields.io/nuget/vpre/FluentAzure.svg)](https://www.nuget.org/packages/FluentAzure)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue.svg)
![Native AOT](https://img.shields.io/badge/Native%20AOT-compatible-brightgreen.svg)
![Azure](https://img.shields.io/badge/Azure-Functions%20%7C%20WebApps%20%7C%20Services-orange.svg)
![Fluent](https://img.shields.io/badge/Style-Fluent%20%7C%20Functional-purple.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)

> **What's new in 0.3.0-rc.1:**
> - An `IConfiguration` provider with `IOptionsMonitor<T>` reload.
> - An Azure App Configuration source.
> - Managed/workload identity and secret redaction.
> - OpenTelemetry and health checks.
> - Compile-time analyzers.
> - .NET 10 and Native AOT support.
>
> See the [changelog](https://github.com/tonyjoanes/FluentAzure/blob/main/CHANGELOG.md) and the [upgrade guide](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/upgrade-guide.md).

## 🎯 Problem This Solves

Azure developers constantly struggle with:
- **Multiple configuration sources** (Environment variables, Key Vault, App Configuration, JSON files)
- **Complex error handling** when secrets are missing or invalid
- **No type safety** in configuration access
- **Imperative, verbose code** for simple configuration scenarios
- **Poor testing experience** for configuration-dependent code

## 🚀 Solution: Functional Configuration Pipeline

Instead of this imperative mess:
```csharp
// Traditional approach - verbose and error-prone
var builder = new ConfigurationBuilder();
builder.AddEnvironmentVariables();
builder.AddAzureKeyVault(vaultUrl);
var config = builder.Build();

var connectionString = config["ConnectionString"];
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("ConnectionString is required");
}

var timeout = int.Parse(config["Timeout"] ?? "30");
// ... more boilerplate
```

Write this ultra-clean pipeline:
```csharp
// FluentAzure approach - ultra clean and safe
using FluentAzure; // Single using statement!

var configResult = await FluentConfig
    .Create()  // Ultra clean - just FluentConfig.Create()!
    .FromEnvironment()
    .FromKeyVault("https://myvault.vault.azure.net")
    .Required("ConnectionString")
    .Optional("Timeout", "30")
    .Validate(c => c["ConnectionString"].StartsWith("Server=") ? null : "ConnectionString must start with Server=")
    .BuildAsync();

var config = configResult.Match(
    success => success,
    errors => throw new InvalidOperationException($"Configuration failed: {string.Join(", ", errors)}")
);
```

## 🧭 Where to Start

| You are building… | Use | Docs |
|---|---|---|
| An ASP.NET Core app, Azure Functions (isolated) or a worker service | The **`IConfiguration` provider**: `builder.Configuration.AddFluentAzureAsync(...)` + `AddFluentAzureOptions<T>()`. You get reload, `IOptionsMonitor<T>`, health checks and fail-fast startup | [Configuration integration](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/configuration-integration.md) |
| A console tool, script or test | The **standalone pipeline**: `FluentConfig.Create()...BuildAsync()` returns a `Result` you can `Match` | [Examples below](#-example-usage-patterns) |
| A trimmed or Native AOT app | The **provider** + the configuration binding source generator | [Native AOT](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/configuration-integration.md#native-aot) |

Whichever you choose:
- **Identity:** pick one identity for Azure with `UseManagedIdentity()`. [Identity & secrets](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/identity-and-redaction.md)
- **Where settings live:** put settings and feature flags in App Configuration and secrets in Key Vault. [App Configuration](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/app-configuration-source.md), [Key Vault](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/key-vault-configuration-source.md)

## 📦 Installation

```bash
dotnet add package FluentAzure --prerelease
```

The current version is shown on the NuGet badge above. The package includes the FluentAzure analyzers (see [docs/analyzers.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/analyzers.md)).

## 🔢 Version Information

You can access the current version programmatically:

```csharp
using FluentAzure;

// Get the current version
string version = FluentConfig.CurrentVersion; // e.g. "1.2.0" or "1.3.0-rc.1"

// Or access version details directly
int major = FluentAzure.Version.Major;
int minor = FluentAzure.Version.Minor;
int patch = FluentAzure.Version.Patch;
bool isPreRelease = FluentAzure.Version.IsPreRelease;
```

## 📖 Example Usage Patterns

#### **Standalone Pipeline (console apps, scripts, tests)**
```csharp
using FluentAzure; // Single using statement!

var config = await FluentConfig
    .Create()  // Ultra clean - just FluentConfig.Create()!
    .FromEnvironment()
    .Required("DATABASE_URL")
    .Optional("CACHE_TTL", "300")
    .BuildAsync();
```

#### **Complex Enterprise Setup**
```csharp
using FluentAzure; // Single using statement!

var configResult = await FluentConfig
    .Create()  // Ultra clean - just FluentConfig.Create()!
    .FromJsonFile("appsettings.json")
    .FromEnvironment()
    .FromKeyVault("https://company-prod-kv.vault.azure.net")
    .Transform("ConnectionString", DecryptConnectionString)
    .Validate(c => Uri.IsWellFormedUriString(c["ServiceUrl"], UriKind.Absolute) ? null : "ServiceUrl must be a valid URI")
    .BuildAsync();

var config = configResult.Bind<AppConfiguration>();
```

#### **Dependency Injection**
```csharp
// Program.cs
using FluentAzure;

builder.Services.AddFluentAzure<AppSettings>(config => config
    .FromEnvironment()
    .FromKeyVault(builder.Configuration["KeyVault:Url"]) // or any other sources
);

// Controller
public class ApiController : ControllerBase
{
    private readonly AppSettings _settings;

    public ApiController(AppSettings settings)
    {
        _settings = settings;
    }
}
```

Where you can `await` (e.g. top-level statements in `Program.cs`), prefer the async overload so remote sources such as Key Vault load without blocking a thread:
```csharp
await builder.Services.AddFluentAzureAsync<AppSettings>(config => config
    .FromEnvironment()
    .FromKeyVault(builder.Configuration["KeyVault:Url"])
);
```

Configuration keys are case-insensitive, and environment variables using the standard `__` separator (e.g. `ConnectionStrings__Default`) are also available under their `:` form (`ConnectionStrings:Default`).

> `AddFluentAzure<T>` builds once at startup and registers `T` as a singleton, with no reload. It uses the basic binder, which binds nested objects but not collections or dictionaries. For new code, prefer the provider below with `AddFluentAzureOptions<T>()`. See [Configuration binding](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/enhanced-configuration-binding.md).

#### **Microsoft.Extensions.Configuration + IOptionsMonitor (Recommended for ASP.NET Core / Functions)**
Plug a FluentAzure pipeline into the standard configuration system. Required keys and validations still fail fast at startup, and values become available to `IConfiguration`, `IOptions<T>` and `IOptionsMonitor<T>`:
```csharp
using FluentAzure;

var builder = WebApplication.CreateBuilder(args);

await builder.Configuration.AddFluentAzureAsync(
    fluent => fluent
        .FromKeyVault(builder.Configuration["KeyVault:Url"]!)
        .Required("Database:ConnectionString"),
    reloadInterval: TimeSpan.FromMinutes(5) // pick up rotated secrets without a restart
);

// Binds the "Database" section, validates [Required]/[Range] etc. and fails at startup if invalid
builder.Services.AddFluentAzureOptions<DatabaseOptions>(builder.Configuration, "Database");

// Consumers: inject IOptionsMonitor<DatabaseOptions> to see reloaded values
```
- Keys using the `__` separator (environment variables, FluentAzure JSON flattening) are exposed with the standard `:` separator.
- A failed reload keeps the last good values; observe failures with `options.OnReloadError` via the `AddFluentAzure(configure, configureSource)` overload.
- `builder.Configuration.AddFluentAzure(...)` is the synchronous equivalent.

#### **Azure App Configuration**
Load settings, feature flags and Key Vault references from App Configuration, with label layering, snapshots and sentinel-key refresh:
```csharp
var config = await FluentConfig
    .Create()
    .FromAppConfiguration("https://myconfig.azconfig.io", options =>
    {
        options.Labels.Add("Production");     // overrides unlabelled defaults
        options.IncludeFeatureFlags = true;   // exposed under FeatureManagement:*
        options.SentinelKey = "Sentinel";     // cheap change detection when reloading
    })
    .Required("Database:ConnectionString")    // may be a Key Vault reference; resolved automatically
    .BuildAsync();
```
See [docs/app-configuration-source.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/app-configuration-source.md) for all options.

#### **Identity & Secret Redaction**
Choose one identity for every Azure source in the pipeline (in any order relative to the sources), and keep secrets out of logs:
```csharp
builder.Configuration.AddFluentAzure(fluent => fluent
    .UseManagedIdentity()                  // or UseWorkloadIdentity() / UseCredential(...)
    .FromAppConfiguration("https://myconfig.azconfig.io")
    .FromKeyVault("https://myvault.vault.azure.net")
    .Sensitive("Jwt:SigningKey"));         // mark secrets that come from other sources

logger.LogDebug("{Config}", builder.Configuration.GetRedactedDebugView()); // Key Vault values appear as ***
```
A per-source credential (`KeyVaultConfiguration.Credential`, `AppConfigurationOptions.Credential`) still takes precedence. Binding and conversion errors never include configuration values.

#### **Observability: OpenTelemetry & Health Checks**
FluentAzure emits traces and metrics through `System.Diagnostics` (no extra dependency), ready for OpenTelemetry:
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(FluentAzureDiagnostics.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(FluentAzureDiagnostics.MeterName));

builder.Services.AddHealthChecks().AddFluentAzure(tags: new[] { "ready" });
```
| Signal | Name | Tags |
|---|---|---|
| Span | `FluentAzure.Build`, with a child `FluentAzure.LoadSource` per source | `fluentazure.source`, `fluentazure.outcome`, `fluentazure.reload`, key/error counts |
| Histogram (s) | `fluentazure.build.duration`, `fluentazure.source.load.duration` | `fluentazure.source`, `fluentazure.outcome`, `fluentazure.reload` |
| Counter | `fluentazure.provider.reloads` | `fluentazure.outcome` = `changed` / `unchanged` / `failure` |

Telemetry and health data contain source names, outcomes, durations, counts and timestamps only — never configuration values or error messages.

The health check does not call Azure. It reports what the configuration providers last observed:
- **Healthy:** every load succeeded.
- **Degraded:** the last reload failed, so the last good values are still being served.
- **Unhealthy** (or your `failureStatus`): no FluentAzure provider is registered, or values are older than `staleAfter` (by default, three reload intervals).

#### **Trimming & Native AOT**
FluentAzure targets .NET 8 and .NET 10 and is annotated for trimming and Native AOT. The pipeline, all sources (environment, JSON, Key Vault, App Configuration) and the `IConfiguration` provider are AOT-safe. The reflection-based binders (`BuildAsync<T>()`, `Bind<T>()`, `AddFluentAzure<T>()`, `AddFluentAzureOptions<T>()`) are marked `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`, so the compiler warns if you use them in a trimmed or AOT app. In those apps, feed `IConfiguration` and bind with the configuration binding source generator:
```xml
<PublishAot>true</PublishAot>
<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
```
```csharp
IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddFluentAzure(fluent => fluent.UseManagedIdentity().FromKeyVault(vaultUrl).Required("App:Name"))
    .Build();

services.AddOptions<AppOptions>().Bind(configuration.GetSection("App")).ValidateOnStart(); // source-generated
```
See [examples/Aot.Example](https://github.com/tonyjoanes/FluentAzure/tree/main/examples/Aot.Example). CI publishes it with Native AOT and fails on any trimming or AOT warning.

#### **Analyzers**
The package includes compile-time analyzers ([docs/analyzers.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/analyzers.md)):

- **FAZ0001:** a `Required("key")` that the `BuildAsync<T>()` type never binds (usually a typo).
- **FAZ0002:** a Key Vault or App Configuration endpoint that isn't HTTPS.
- **FAZ0003:** a hard-coded App Configuration connection string containing a secret.
- **FAZ0004:** `GetDebugView()`, which prints secrets; use `GetRedactedDebugView()` instead.

#### **Web API Example**
```csharp
// Program.cs
using FluentAzure;

var builder = WebApplication.CreateBuilder(args);

await builder.Configuration.AddFluentAzureAsync(fluent => fluent
    .UseManagedIdentity()
    .FromKeyVault(builder.Configuration["KeyVault:Url"]!)
    .Required("ConnectionStrings:DefaultConnection")
    .Required("Jwt:SecretKey"));

builder.Services.AddFluentAzureOptions<JwtOptions>(builder.Configuration, "Jwt");
builder.Services.AddHealthChecks().AddFluentAzure();

var app = builder.Build();
app.MapHealthChecks("/health");
```

A missing required key stops startup with a `FluentAzureConfigurationException` that lists every problem. Values are available through `IConfiguration`, `IOptions<JwtOptions>` and `builder.Configuration.GetConnectionString("DefaultConnection")`.

## 🚀 Getting Started

### Prerequisites
- .NET 8 or .NET 10.
- For Azure sources: an identity with access to your Key Vault and/or App Configuration store. No Azure subscription is needed for environment, JSON or in-memory sources. For local work, the [App Configuration emulator](https://github.com/tonyjoanes/FluentAzure/blob/main/CONTRIBUTING.md#app-configuration-emulator-tests) runs in Docker.

### Quick Start
1. Install the package: `dotnet add package FluentAzure --prerelease`.
2. Add `using FluentAzure;`.
3. Choose your starting point from [Where to Start](#-where-to-start).
4. Handle results with `Match`, or let the provider fail fast at startup.

### Features
- **Fluent pipeline:** chain sources, `Required`, `Optional`, `Transform` and `Validate`, and get a `Result` with every error, not just the first.
- **Sources:** environment variables, JSON files, in-memory values, Azure Key Vault, and Azure App Configuration (labels, snapshots, Key Vault references, feature flags).
- **`IConfiguration` provider:** works with the options pattern, reloads into `IOptionsMonitor<T>`, and keeps the last good values when a reload fails.
- **Identity:** one managed/workload identity for every Azure source.
- **Secret safety:** secrets are tracked and redacted, and never appear in errors, telemetry or health data.
- **Observability:** OpenTelemetry traces and metrics, and a health check.
- **Analyzers:** compile-time checks for key typos, insecure endpoints, hard-coded secrets and unredacted debug output.
- **Platforms:** .NET 8 and .NET 10, trimming and Native AOT compatible.

## 📚 Documentation

| Topic | Page |
|---|---|
| `IConfiguration` provider, reload, options, AOT | [docs/configuration-integration.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/configuration-integration.md) |
| Azure App Configuration | [docs/app-configuration-source.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/app-configuration-source.md) |
| Azure Key Vault | [docs/key-vault-configuration-source.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/key-vault-configuration-source.md) |
| Identity & secret redaction | [docs/identity-and-redaction.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/identity-and-redaction.md) |
| OpenTelemetry & health checks | [docs/observability.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/observability.md) |
| Binding to typed objects | [docs/enhanced-configuration-binding.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/enhanced-configuration-binding.md) |
| Analyzers FAZ0001–FAZ0004 | [docs/analyzers.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/analyzers.md) |
| Changelog & upgrading | [CHANGELOG.md](https://github.com/tonyjoanes/FluentAzure/blob/main/CHANGELOG.md), [docs/upgrade-guide.md](https://github.com/tonyjoanes/FluentAzure/blob/main/docs/upgrade-guide.md) |
| Examples | [examples/](https://github.com/tonyjoanes/FluentAzure/blob/main/examples/README.md) |
| Security | [SECURITY.md](https://github.com/tonyjoanes/FluentAzure/blob/main/SECURITY.md) |

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/tonyjoanes/FluentAzure/blob/main/CONTRIBUTING.md) for details on:
- Code style and standards
- Testing requirements
- Pull request process
- Development setup

## 📚 Resources

- [Functional Programming in C#](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-9#records)
- [Azure Key Vault Developer Guide](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Result Pattern in C#](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [Monads in C#](https://mikhail.io/2018/07/monads-explained-in-csharp-again/)

---

**Ready to revolutionize Azure configuration management? Let's build something awesome! 🚀**
