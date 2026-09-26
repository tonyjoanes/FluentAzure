# Identity & Secrets

This page covers how FluentAzure authenticates to Azure, and how it keeps secret values out of errors, logs, telemetry and debug output.

## Choosing an identity

Every Azure source in a pipeline (Key Vault and App Configuration, including resolving Key Vault references) uses one shared **pipeline credential** unless the source is given its own.

```csharp
FluentConfig.Create()
    .UseManagedIdentity()                     // system-assigned
    .FromAppConfiguration("https://myconfig.azconfig.io")
    .FromKeyVault("https://myvault.vault.azure.net");
```

| Method | Credential |
|---|---|
| `UseManagedIdentity()` | System-assigned managed identity |
| `UseManagedIdentity("<client-id>")` | User-assigned managed identity |
| `UseWorkloadIdentity()` | Microsoft Entra Workload ID (AKS), from the standard `AZURE_*` environment variables |
| `UseCredential(credential)` | Any `TokenCredential`, e.g. `AzureCliCredential` for local development |
| *(none)* | `DefaultAzureCredential` |

These methods can be called before or after `FromKeyVault` / `FromAppConfiguration`. The credential is resolved on first use, and all sources share one instance, so they share a token cache. The credential in effect is available as `ConfigurationBuilder.Credential`. It can't be changed after the first token has been requested.

**Precedence:** a credential set on a source wins over the pipeline credential. That means `KeyVaultConfiguration.Credential` for `FromKeyVault(url, c => c.Credential = ...)`, and `AppConfigurationOptions.Credential` for App Configuration.

### Recommendations

- **Production:** use `UseManagedIdentity()` on App Service, Functions, Container Apps and VMs, or `UseWorkloadIdentity()` on AKS. These are deterministic and never fall back to developer credentials.
- **If you keep `DefaultAzureCredential`:** set the environment variable `AZURE_TOKEN_CREDENTIALS=prod` in production, so it only tries the production credential types.
- **Access keys:** prefer App Configuration endpoints (`https://...azconfig.io`) with Entra ID over connection strings that contain secrets. The FAZ0003 analyzer warns about hard-coded ones.
- **Key Vault:** grant the identity the `Key Vault Secrets User` role (Azure RBAC). Listing and reading secrets is all FluentAzure needs.

## Secret values

### Which values are treated as secrets

| Source | Treated as secret |
|---|---|
| Key Vault (`FromKeyVault`) | Every value |
| App Configuration | Values resolved from Key Vault references |
| Anything else | Keys you mark with `.Sensitive("Key")` |

```csharp
var pipeline = FluentConfig.Create()
    .FromEnvironment()
    .FromKeyVault(vaultUrl)
    .Sensitive("Jwt:SigningKey", "Smtp:Password");   // secrets that arrive via environment variables

pipeline.IsSensitive("Jwt:SigningKey");   // true
```

Custom sources can take part by implementing `ISensitiveConfigurationSource`.

After a build, `ConfigurationBuilder.IsSensitive(key)` reports whether a key's winning value came from a secret source, or was marked. If a higher-priority, non-secret source supplied the value, the key is not treated as secret. The configuration provider exposes the same check as `FluentAzureConfigurationProvider.IsSensitive(key)`, accepting `:` or `__` separators.

### Redacted debug view

`IConfigurationRoot.GetDebugView()` prints every value, including secrets. Use `GetRedactedDebugView()` instead:

```csharp
logger.LogDebug("{Config}", builder.Configuration.GetRedactedDebugView());        // secrets shown as ***
logger.LogDebug("{Config}", builder.Configuration.GetRedactedDebugView("[hidden]"));
```

The FAZ0004 analyzer warns wherever `GetDebugView()` is used in a project that uses FluentAzure's provider.

> Redaction only masks keys FluentAzure *knows* are secrets. `FromEnvironment()` loads every environment variable on the machine or in the container, and those are not masked unless you mark them with `.Sensitive(...)`. Avoid dumping the whole configuration in production logs.

### What FluentAzure never outputs

- **Errors:** binding and conversion errors name the key and target type, not the value. Parser messages that echo their input (e.g. `int.Parse` on .NET 8+) are not passed through.
- **Key Vault reference failures:** these name the setting key and the secret identifier, never a secret value.
- **Telemetry and health checks:** these report source names, outcomes, durations, counts and timestamps only. See [Observability](observability.md).
- **Custom sources that throw:** the pipeline reports the source name and exception type, not the exception message.

### What is not covered

- **Your own code:** values you read and then log or put in `Validate` error messages yourself.
- **`Result<T>` and `Option<T>`:** their `ToString()` includes the contained value.
- **Memory scrubbing:** disposing `KeyVaultSource` drops references to secret values. .NET strings are immutable, so memory is not scrubbed.

See also [SECURITY.md](../SECURITY.md).
