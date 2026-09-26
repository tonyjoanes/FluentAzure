# Configuration Guard

The guard adds startup validation, health checks and secret redaction to the configuration you **already have**. It checks the application's final `IConfiguration`, whichever providers supplied it:
- Microsoft's `AddAzureAppConfiguration()` and `AddAzureKeyVault()`;
- JSON files and environment variables;
- FluentAzure's own provider.

You don't have to change how configuration is loaded.

## Quick start

```csharp
using FluentAzure;

var builder = WebApplication.CreateBuilder(args);

// Load configuration however you already do
builder.Configuration
    .AddAzureAppConfiguration(options => options.Connect(new Uri(appConfigEndpoint), credential))
    .AddAzureKeyVault(new Uri(vaultUrl), credential);

// Guard it
builder.Services.AddFluentAzureGuard(guard => guard
    .Required("Database:ConnectionString", "Jwt:SecretKey", "Jwt:Issuer")
    .Validate("Jwt:SecretKey", v => v.Length >= 32, "must be at least 32 characters")
    .Validate("Api:BaseUrl", v => Uri.TryCreate(v, UriKind.Absolute, out var u) && u.Scheme == "https",
        "must be an absolute https URL")
    .Sensitive("Payments:*"));

builder.Services.AddHealthChecks().AddFluentAzureGuard();
```

If anything is wrong, the app stops at startup, before it serves traffic, with one exception listing **every** problem:

```text
Microsoft.Extensions.Options.OptionsValidationException:
  Configuration key 'Database:ConnectionString' is required but is missing or empty.;
  Configuration key 'Jwt:SecretKey' must be at least 32 characters.
```

Failures name the key and the requirement. They never contain the value.

## Rules

| Method | Checks |
|---|---|
| `Required(params string[] keys)` | Each key is present with a non-empty value, or is a section with children (`Required("ConnectionStrings")`). |
| `Validate(key, isValid, requirement)` | The value satisfies `isValid`. The rule is skipped when the key has no value, so combine it with `Required` if the key is mandatory. If `isValid` throws, the failure names the exception type, not its message, because parser messages can echo the value. |
| `Sensitive(params string[] patterns)` | Marks keys for redaction (see below). A pattern is an exact key or a section followed by `*`. |

Keys are case-insensitive, and `__` is treated as `:` (as in environment variables).

Write the `requirement` text so it reads after the key name ("must be …"). Don't include the value in it.

## When the rules run

| Where | How |
|---|---|
| Host startup (ASP.NET Core, workers, Functions isolated) | Automatically. `AddFluentAzureGuard()` hooks into `ValidateOnStart`, and the host throws `OptionsValidationException` from `StartAsync`. |
| Health checks | `AddHealthChecks().AddFluentAzureGuard()` re-runs the rules against current configuration, so a reload (App Configuration refresh, Key Vault reload) that removes a required key or brings in a bad value is reported. The result is Unhealthy by default, with `failures` (a count) in its data. |
| Anywhere else (console apps, tests) | Call `guard.Check(configuration)` for the list of failures, or `guard.ThrowIfInvalid(configuration)` to throw a `FluentAzureConfigurationException` with them. |

## Redaction

`GetDebugView()` prints every configuration value. `GetRedactedDebugView()` prints the same view with secrets masked:

```csharp
var root = (IConfigurationRoot)builder.Configuration;
logger.LogDebug("{Config}", root.GetRedactedDebugView());

// Also mask the patterns declared with Sensitive(...)
var guard = app.Services.GetRequiredService<ConfigurationGuard>();
logger.LogDebug("{Config}", guard.GetRedactedDebugView(root));
```

A value is masked when:
- it came from **Microsoft's Key Vault provider** (`AddAzureKeyVault`) or FluentAzure's Key Vault source;
- its **key looks like a credential**: the last segment contains `password`, `secret`, `token`, `apikey`, `accesskey`, `privatekey`, `connectionstring`, `credential` or `sas`, or the key is under `ConnectionStrings`;
- its **value looks like one**: it contains `AccountKey=`, `SharedAccessKey=`, `Password=`, `Pwd=`, `Secret=`, `sig=` or a PEM header;
- it matches a pattern declared with **`Sensitive(...)`** (guard view only).

The rules are deliberately broad: masking a harmless value in a debug view costs little. They can't recognise every secret, though. For example, Microsoft's App Configuration provider doesn't say which values it resolved from Key Vault references. Declare anything else sensitive with `Sensitive(...)`, and still avoid logging whole configurations in production.
