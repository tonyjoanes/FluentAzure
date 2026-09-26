# Microsoft.Extensions.Configuration Integration

FluentAzure can run as a standard .NET configuration provider. The pipeline is the same one you build with `FluentConfig.Create()`: sources, `Required`, `Optional`, `Transform` and `Validate`. Its values flow into `IConfiguration`, the options pattern and `IOptionsMonitor<T>`.

**This is the recommended setup for ASP.NET Core, Azure Functions and worker services.** It is also the path to use in trimmed or Native AOT apps (see [Native AOT](#native-aot)).

## Quick start

```csharp
using FluentAzure;

var builder = WebApplication.CreateBuilder(args);

await builder.Configuration.AddFluentAzureAsync(
    fluent => fluent
        .UseManagedIdentity()
        .FromAppConfiguration("https://myconfig.azconfig.io", o => o.SentinelKey = "Sentinel")
        .FromKeyVault("https://myvault.vault.azure.net")
        .Required("Database:ConnectionString"),
    reloadInterval: TimeSpan.FromMinutes(5));

builder.Services.AddFluentAzureOptions<DatabaseOptions>(builder.Configuration, "Database");
```

```csharp
public class DatabaseOptions
{
    [Required]
    public string ConnectionString { get; set; } = "";

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public class OrdersService(IOptionsMonitor<DatabaseOptions> options)
{
    // options.CurrentValue always reflects the latest successful reload
}
```

## Adding the provider

| Method | Use when |
|---|---|
| `AddFluentAzureAsync(configure, reloadInterval?)` | You can `await`, e.g. top-level statements in `Program.cs`. The pipeline loads asynchronously and no thread is blocked. |
| `AddFluentAzure(configure, reloadInterval?)` | You can't `await`. The pipeline loads when the configuration is built, on a thread-pool thread so it can't deadlock. |
| `AddFluentAzure(configure, source => { ... })` | You need full control of the source: `ReloadInterval`, `OnReloadError`. |

Both add a `FluentAzureConfigurationSource` to any `IConfigurationBuilder`, including `WebApplicationBuilder.Configuration` and `HostApplicationBuilder.Configuration`.

### Fail fast on the initial load

If the pipeline reports errors when it first loads, for example a missing `Required` key, a failed `Validate` or an unreachable source, startup throws a `FluentAzureConfigurationException`. Its `Errors` property lists every problem. The messages name keys and sources but never contain configuration values.

```csharp
try
{
    await builder.Configuration.AddFluentAzureAsync(fluent => fluent.FromEnvironment().Required("Api:Key"));
}
catch (FluentAzureConfigurationException ex)
{
    foreach (var error in ex.Errors) Console.Error.WriteLine(error);
    throw;
}
```

### Key format

Keys are case-insensitive. Keys that use the `__` separator (environment variables, FluentAzure's JSON source) are also exposed with the standard `:` separator. `ConnectionStrings__Default` is available as `ConnectionStrings:Default`, so `GetSection("ConnectionStrings")` and options binding work as expected. If both forms exist, the explicit `:` key wins.

## Reloading and IOptionsMonitor

Set `reloadInterval` to re-run the pipeline periodically.

- **Cached sources are refetched.** Key Vault and environment variables implement `IReloadableConfigurationSource`, so a reload fetches them again. App Configuration first checks its sentinel key, if one is configured.
- **Change notifications fire only when values changed.** `IOptionsMonitor<T>.OnChange` and `CurrentValue` pick up the new values; unchanged reloads don't notify.
- **A failed reload keeps the last good values.** The app keeps running on them, and the failure is reported:

```csharp
builder.Configuration.AddFluentAzure(
    fluent => fluent.FromKeyVault(vaultUrl),
    source =>
    {
        source.ReloadInterval = TimeSpan.FromMinutes(5);
        source.OnReloadError = errors => Console.Error.WriteLine($"Config reload failed: {errors.Count} error(s)");
    });
```

You can also trigger a reload yourself, for example from an Event Grid notification when a secret rotates:

```csharp
var provider = ((IConfigurationRoot)app.Configuration).Providers
    .OfType<FluentAzure.Configuration.FluentAzureConfigurationProvider>()
    .Single();

bool succeeded = await provider.ReloadAsync();
```

`FluentAzureConfigurationProvider` exposes:

| Member | Description |
|---|---|
| `ReloadAsync()` | Re-run the pipeline; returns `false` (and keeps old values) on failure |
| `LastReloadErrors` | Errors from the most recent failed reload, empty after a success |
| `LastSuccessfulLoad` / `LastLoadAttempt` | Timestamps, also reported by the [health check](observability.md#health-checks) |
| `ReloadInterval`, `KeyCount` | Current settings and size |
| `IsSensitive(key)` | Whether a key holds a secret (see [Identity & secrets](identity-and-redaction.md)) |

## Options binding and startup validation

`AddFluentAzureOptions<T>(configuration, sectionName)` is shorthand for the recommended options setup:

```csharp
services.AddOptions<T>()
    .Bind(configuration.GetSection(sectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

It returns the `OptionsBuilder<T>`, so you can chain more checks:

```csharp
builder.Services.AddFluentAzureOptions<ApiOptions>(builder.Configuration, "Api")
    .Validate(o => o.BaseUrl.StartsWith("https://"), "Api:BaseUrl must use HTTPS");
```

With `ValidateOnStart`, invalid options stop the host from starting (`OptionsValidationException`) instead of failing on first use.

## Native AOT

The provider, the pipeline and every source are trimming and Native AOT compatible. The reflection-based binders are not: `BuildAsync<T>()`, `Bind<T>()`, `AddFluentAzure<T>()` and `AddFluentAzureOptions<T>()`. In AOT apps, bind with the configuration binding source generator instead:

```xml
<PublishAot>true</PublishAot>
<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
```

```csharp
IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddFluentAzure(fluent => fluent.UseManagedIdentity().FromKeyVault(vaultUrl).Required("App:Name"))
    .Build();

services.AddOptions<AppOptions>().Bind(configuration.GetSection("App")).ValidateOnStart();
```

See [`examples/Aot.Example`](../examples/Aot.Example), which CI publishes with Native AOT and runs.

## Standalone pipeline vs. provider

| | `FluentConfig.Create()...BuildAsync()` | `AddFluentAzure()` provider |
|---|---|---|
| Result | `Result<Dictionary<string, string>>` or `Result<T>` | `IConfiguration`, `IOptions<T>`, `IOptionsMonitor<T>` |
| Errors | Returned as a `Result` | `FluentAzureConfigurationException` at startup |
| Reload | Build again yourself | `reloadInterval`, `ReloadAsync()`, change tokens |
| Health check / telemetry | Telemetry only | Both |
| Native AOT | Non-generic `BuildAsync()` only | Yes, with the binding source generator |
| Best for | Console tools, scripts, tests | ASP.NET Core, Functions, workers |

The older `services.AddFluentAzure<T>(...)` registrations still work. They build once and register `T` as a singleton, with no reload.
