# Configuration Binding

FluentAzure can bind the flat key/value output of a pipeline to strongly typed objects. There are two binders with different capabilities; this page describes what each one actually supports.

> **ASP.NET Core, Functions and workers:** prefer the [`IConfiguration` provider](configuration-integration.md) with `AddFluentAzureOptions<T>()` or `services.AddOptions<T>().Bind(...)`. That uses Microsoft's binder, which supports every standard shape (collections, dictionaries, nested objects), validation on start and reload through `IOptionsMonitor<T>`, and it works with Native AOT through the binding source generator.
>
> Both FluentAzure binders use reflection. They are annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`, so the compiler warns if you use them in trimmed or Native AOT apps.

## Which binder am I using?

| API | Binder |
|---|---|
| `pipeline.BuildAsync<T>()`, `BuildOptionalAsync<T>()` | Basic |
| `result.Bind<T>()` on a `Result<Dictionary<string, string>>` | Basic |
| `dictionary.BindOptional<T>()`, `BindWithFallback`, `BindWithValidation`, `BindConditional`, `BindAndTransform` | Basic |
| `EnhancedConfigurationBinder.Bind<T>(dictionary, options?)`, `dictionary.BindOptional<T>(options)` | Enhanced |
| `EnhancedConfigurationBinder.BindJson<T>(dictionary, options?)`, `dictionary.BindJsonOptional<T>(options?)` | Enhanced (JSON) |

## Basic binder

```csharp
public class AppSettings
{
    public string Name { get; set; } = "";
    public int Timeout { get; set; }
    public DatabaseSettings Database { get; set; } = new();
}

public class DatabaseSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
}

var result = await FluentConfig.Create()
    .FromEnvironment()                 // Name, Timeout, Database__Host, Database__Port
    .Required("Name")
    .BuildAsync<AppSettings>();
```

**What it supports**
- **Types:** classes with a public parameterless constructor and public settable properties.
- **Values:** `string`, numeric types, `bool`, `DateTime`, `TimeSpan`, `Guid`, `Uri`, enums (case-insensitive) and their nullable forms.
- **Keys:** matched case-insensitively.
- **Nesting:** nested objects via the `__` separator (`Database__Host`).

**Limitations**
- **`:` keys:** nested keys that use `:` (e.g. `Database:Host`) are **not** bound. This matters for Key Vault, whose `--` secret names are mapped to `:`, and for App Configuration. Either use the enhanced binder, or use the `IConfiguration` provider, which accepts both separators.
- **Collections and dictionaries:** not bound.

## Enhanced binder

```csharp
using FluentAzure.Binding;

var build = await FluentConfig.Create()
    .FromAppConfiguration("https://myconfig.azconfig.io")
    .BuildAsync();

var settings = build.Bind(values => EnhancedConfigurationBinder.Bind<AppSettings>(values));

settings.Match(
    ok => Console.WriteLine($"Database: {ok.Database.Host}:{ok.Database.Port}"),
    errors => Console.WriteLine(string.Join(Environment.NewLine, errors)));
```

**What it supports**

| Shape | Example keys |
|---|---|
| Nested objects, `:` or `__` separators, case-insensitive | `Database:Host`, `database__port` |
| Records (positional constructor parameters) | `Name`, `Version` for `record AppInfo(string Name, string Version)` |
| Init-only properties | `ApiKey` for `public string ApiKey { get; init; }` |
| Enums (case-insensitive) and nullables (empty string → `null`) | `Level=Warning`, `MaxItems=` |
| Lists of objects (`List<T>`, `IList<T>`, `ICollection<T>` where `T` is a class) | `Endpoints:0:Name`, `Endpoints:1:Name` |
| Data Annotations validation | `[Required]`, `[Range]`, `[EmailAddress]`, … |

**Known limitations**
- **Lists or arrays of strings** (`List<string>`, `string[]`): the whole bind fails with `Failed to create instance of type 'String'`.
- **Arrays of numbers** (`int[]`): the elements are not bound (they stay `0`).
- **Dictionaries:** not bound (they stay empty).

For these shapes, use the `IConfiguration` provider with Microsoft's binder.

### Options

```csharp
var options = new BindingOptions
{
    EnableValidation = true,        // Data Annotations (default: true)
    CaseSensitive = false,          // default: false
    IgnoreMissingOptional = true,   // default: true
};

var settings = EnhancedConfigurationBinder.Bind<AppSettings>(values, options);
```

### Validation

With `EnableValidation`, Data Annotations are checked after binding, and every failure is returned:

```csharp
public class ContactSettings
{
    [EmailAddress]
    public string Email { get; set; } = "";

    [Range(1, 120)]
    public int RetentionDays { get; set; }
}

var result = EnhancedConfigurationBinder.Bind<ContactSettings>(
    new Dictionary<string, string> { ["Email"] = "not-an-email", ["RetentionDays"] = "500" });

// result.Errors:
//   [BIND ERROR] [validation] The Email field is not a valid e-mail address.
//   [BIND ERROR] [validation] The field RetentionDays must be between 1 and 120.
```

Conversion errors name the property and target type but never include the configured value, because it may be a secret.

### JSON binding

`BindJson<T>` turns the flat keys into a JSON document and deserializes it with `System.Text.Json`, using camelCase naming and case-insensitive properties, or your own `BindingOptions.JsonOptions`. Enums must be numeric unless you add a `JsonStringEnumConverter`:

```csharp
var options = new BindingOptions
{
    JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    },
};

var result = EnhancedConfigurationBinder.BindJson<AppSettings>(values, options);
```

## Dictionary helper methods

These extension methods in `FluentAzure.Extensions` work on the `Dictionary<string, string>` from `BuildAsync()`. They use the basic binder unless you pass `BindingOptions`.

| Method | Returns |
|---|---|
| `BindOptional<T>()` / `BindOptional<T>(options)` | `Option<T>` (`None` on failure) |
| `BindJsonOptional<T>(options?)` | `Option<T>` |
| `BindWithFallback<T>(fallback)` | `T` (the fallback on failure) |
| `BindWithValidation<T>(predicate)` | `Option<T>` |
| `BindWithValidation<T>(predicate, errorFactory)` | `Result<T>` |
| `BindConditional<T>(condition, fallback)` | `T` |
| `BindAndTransform<T, TResult>(transform)` | `Option<TResult>` |

```csharp
var values = (await FluentConfig.Create().FromEnvironment().BuildAsync()).Value;

var settings = values.BindWithValidation<AppSettings>(
    s => s.Timeout > 0,
    s => "Timeout must be positive");
```
