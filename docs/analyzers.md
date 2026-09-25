# FluentAzure Analyzers

The FluentAzure NuGet package includes Roslyn analyzers that catch configuration mistakes at compile time. They run in `dotnet build`, Visual Studio, Rider and VS Code, with no setup.

| Rule | Severity | Category | Summary |
|---|---|---|---|
| [FAZ0001](#faz0001) | Warning | Usage | `Required("key")` does not match any property of the `BuildAsync<T>()` target type |
| [FAZ0002](#faz0002) | Warning | Security | Key Vault / App Configuration endpoint literal is not an absolute HTTPS URI |
| [FAZ0003](#faz0003) | Warning | Security | App Configuration connection string containing a secret is hard-coded |
| [FAZ0004](#faz0004) | Warning | Security | `GetDebugView()` prints secret values; use `GetRedactedDebugView()` |

Change a rule's severity in `.editorconfig`, for example:

```ini
[*.cs]
dotnet_diagnostic.FAZ0001.severity = error
dotnet_diagnostic.FAZ0004.severity = suggestion
```

<a id="faz0001"></a>
## FAZ0001: Required key is not bound by the target type

In a fluent chain ending in `BuildAsync<T>()` or `BuildOptionalAsync<T>()`, every `Required("...")` key should correspond to a property of `T`. When it doesn't, the build still insists the key exists, but its value is never bound. This is almost always a typo.

```csharp
public class AppSettings { public string ConnectionString { get; set; } = ""; }

await FluentConfig.Create()
    .FromEnvironment()
    .Required("ConectionString")   // FAZ0001: typo, never bound to AppSettings.ConnectionString
    .BuildAsync<AppSettings>();
```

Matching is lenient, so correct keys are never flagged:
- Case and separators (`:`, `__`, `_`, `-`, `.`) are ignored. For example, `DATABASE_URL` matches `DatabaseUrl`, and `Database:Host` matches `Database.Host`.
- Nested properties, record constructor parameters, dictionaries and collections are all understood.
- Any key below a dictionary, collection, `object` or `IConfiguration` member is accepted.

Only the chain that ends in `BuildAsync<T>()` is analyzed. Keys that are required but deliberately consumed elsewhere (for example, in a `Validate` callback) can be suppressed with `#pragma warning disable FAZ0001`.

<a id="faz0002"></a>
## FAZ0002: Azure endpoint is not an absolute HTTPS URI

This rule checks string literals passed to `FromKeyVault*()`, `FromAppConfiguration()`, `new KeyVaultSource(...)` and `new AppConfigurationSource(...)`. Endpoints must be absolute `https://` URIs. Plain HTTP would send tokens and secrets unencrypted, and a missing scheme fails at runtime.

```csharp
.FromKeyVault("http://my-vault.vault.azure.net/")   // FAZ0002
.FromKeyVault("my-vault.vault.azure.net")           // FAZ0002
.FromKeyVault("https://my-vault.vault.azure.net/")  // OK
```

Values computed at runtime, for example from configuration, are not checked.

<a id="faz0003"></a>
## FAZ0003: Hard-coded App Configuration connection string with a secret

A connection string containing `Secret=` grants access to the store and ends up in source control. Load it from configuration or an environment variable instead. Better still, use an endpoint with Microsoft Entra ID:

```csharp
.FromAppConfiguration("Endpoint=https://x.azconfig.io;Id=...;Secret=...")   // FAZ0003

FluentConfig.Create()
    .UseManagedIdentity()
    .FromAppConfiguration("https://x.azconfig.io")                          // OK
```

<a id="faz0004"></a>
## FAZ0004: GetDebugView() prints secret values

`IConfigurationRoot.GetDebugView()` includes every value, including Key Vault secrets loaded by FluentAzure. Use `GetRedactedDebugView()`, which masks values FluentAzure knows are secrets. Alternatively, pass your own processor: `GetDebugView(context => ...)` is not flagged.

This rule only runs in projects that reference FluentAzure's `IConfiguration` integration.
