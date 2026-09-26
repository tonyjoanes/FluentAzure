# Observability: OpenTelemetry & Health Checks

## OpenTelemetry

FluentAzure emits traces and metrics through `System.Diagnostics` (`ActivitySource` and `Meter`), so there is no OpenTelemetry package dependency. Enable them in your OpenTelemetry setup:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(FluentAzureDiagnostics.ActivitySourceName))
    .WithMetrics(metrics => metrics.AddMeter(FluentAzureDiagnostics.MeterName));
```

Both names are `"FluentAzure"`.

### Traces

| Span | When | Tags |
|---|---|---|
| `FluentAzure.Build` | Each pipeline build or reload | `fluentazure.reload`, `fluentazure.source.count`, `fluentazure.outcome`, `fluentazure.key.count` (success) or `fluentazure.error.count` (failure) |
| `FluentAzure.LoadSource` | Each source, as a child of `FluentAzure.Build` | `fluentazure.source`, `fluentazure.reload`, `fluentazure.outcome`, `fluentazure.key.count` |

Failed builds and source loads have status `Error`. The status description contains an error count only.

### Metrics

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `fluentazure.build.duration` | Histogram | `s` | `fluentazure.outcome`, `fluentazure.reload` |
| `fluentazure.source.load.duration` | Histogram | `s` | `fluentazure.source`, `fluentazure.outcome`, `fluentazure.reload` |
| `fluentazure.provider.reloads` | Counter | `{reload}` | `fluentazure.outcome` = `changed`, `unchanged` or `failure` |

`fluentazure.outcome` is `success` or `failure` for builds and source loads. `fluentazure.source` is the source's `Name`, e.g. `KeyVault(myvault.vault.azure.net)`, `AppConfiguration`, `Environment` or `JsonFile(appsettings.json)`.

Useful alerts:
- **Reload failures:** `fluentazure.provider.reloads{fluentazure.outcome="failure"}` increasing.
- **Slow Key Vault loads:** p95 of `fluentazure.source.load.duration` for a Key Vault source rising.

Telemetry never contains configuration values or error messages, only names, outcomes, durations and counts.

## Health checks

```csharp
builder.Services.AddHealthChecks()
    .AddFluentAzure(tags: new[] { "ready" });

app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });
```

The check reports on the FluentAzure providers in the application's `IConfiguration`, i.e. those added with `AddFluentAzure()` / `AddFluentAzureAsync()`. **It doesn't call Azure.** It reports what the providers last observed, so it is cheap enough for frequent probes.

| Status | When |
|---|---|
| Healthy | Every provider's last load or reload succeeded |
| Degraded | A reload failed; the last good values are still being served |
| `failureStatus` (default Unhealthy) | No FluentAzure provider is registered, or a provider's values are older than `staleAfter` |

### Options

`AddFluentAzure(name = "fluentazure", failureStatus = null, tags = null, staleAfter = null)`

- **`staleAfter`:** the maximum age of the last successful load. By default this is three reload intervals, for providers with a [reload interval](configuration-integration.md#reloading-and-ioptionsmonitor). Providers without periodic reload are never considered stale.
- **`failureStatus`:** use `HealthStatus.Degraded` if stale configuration shouldn't take the instance out of rotation.

### Reported data

| Key | Meaning |
|---|---|
| `keys` | Number of configuration keys |
| `lastSuccessfulLoad` | ISO 8601 timestamp |
| `lastReloadErrorCount` | Present after a failed reload |
| `staleFor` | Present when stale |

With several providers, keys are prefixed `provider0.`, `provider1.` and so on. Health data never includes values or error messages, because health endpoints are often public.

## Logging

`KeyVaultSource` and `AppConfigurationSource` accept an optional `ILogger` in their constructors. They log source names, secret names and counts, never secret values. For reload failures in the provider, use `OnReloadError` (see [Configuration integration](configuration-integration.md#reloading-and-ioptionsmonitor)).
