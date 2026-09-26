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
    .FromEnvironment()                 // Name, Timeout, Database__Host (or Database:Host), Database__Port
    .Required("Name")
    .BuildAsync<AppSettings>();
```

**What it supports**
- **Types:** classes with a public parameterless constructor and public settable properties.
- **Values:** `string`, numeric types, `bool`, `DateTime`, `TimeSpan`, `Guid`, `Uri`, enums (case-insensitive) and their nullable forms.
- **Keys:** matched case-insensitively.
- **Nesting:** nested objects via either separator: `Database:Host` (Key Vault's `Database--Host` and App Configuration keys) or `Database__Host`. If both forms of a key are present, the `:` key wins.

**Limitations**
- **Collections and dictionaries:** not bound. Use the enhanced binder or the `IConfiguration` provider.

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
| Nested objects, `:` or `__` separators, case-insensitive (unless `CaseSensitive`) | `Database:Host`, `database__port` |
| Records (positional constructor parameters) | `Name`, `Version` for `record AppInfo(string Name, string Version)` |
| Init-only properties | `ApiKey` for `public string ApiKey { get; init; }` |
| Enums (case-insensitive) and nullables (empty string → `null`) | `Level=Warning`, `MaxItems=` |
| Lists and arrays (`List<T>`, `IList<T>`, `ICollection<T>`, `T[]`) of objects or simple values | `Endpoints:0:Name`, `Hosts:0`, `Ports__1` |
| Dictionaries (`Dictionary<,>`, `IDictionary<,>`, `IReadOnlyDictionary<,>`) of objects or simple values | `ConnectionStrings:Primary`, `Databases:Orders:Host` |
| Data Annotations validation | `[Required]`, `[Range]`, `[EmailAddress]`, … |

**Collection details**
- **Order:** elements are bound in index order; gaps in the indices are closed up.
- **Dictionaries:** entries are added to a dictionary the property already holds (so initializer defaults are kept, with that dictionary's own key comparer). A dictionary the binder creates with `string` keys ignores key case. Non-`string` keys (`int`, enums, …) are converted, and a key that can't be converted is reported as an error.
- **Not supported:** nested collections (`List<List<T>>`), sets and immutable collections. Use the `IConfiguration` provider with Microsoft's binder for these.

### Options

```csharp
var options = new BindingOptions
{
    EnableValidation = true,        // Data Annotations (default: true)
    CaseSensitive = false,          // default: false
    IgnoreMissingOptional = true,   // keep a property's current value when it has no key (default: true)
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
