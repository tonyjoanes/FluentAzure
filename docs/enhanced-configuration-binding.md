# Enhanced Configuration Binding System

The FluentAzure enhanced configuration binding system provides a powerful, type-safe way to bind configuration values to strongly-typed objects with support for validation, collections, record types, and JSON serialization.

## Features

- ✅ **Flat to Nested Object Conversion**: Automatically converts flat configuration keys to nested object structures
- ✅ **Record Type Support**: Full support for C# record types with positional and init-only properties
- ✅ **Collection Binding**: Bind arrays, lists, and other collection types
- ✅ **Validation**: Built-in Data Annotations validation with clear error messages
- ✅ **JSON Serialization**: Uses System.Text.Json for complex object binding
- ✅ **Init-Only Properties**: Support for C# 9+ init-only properties
- ✅ **Nullable Types**: Full support for nullable value and reference types
- ✅ **Enum Support**: Automatic enum parsing from string values
- ✅ **Custom Validation**: Extensible validation with custom validation functions
- ✅ **Error Handling**: Comprehensive error reporting with property paths

## Quick Start

### Basic Binding

```csharp
var config = new Dictionary<string, string>
{
    ["Database:Host"] = "localhost",
    ["Database:Port"] = "5432",
    ["Api:BaseUrl"] = "https://api.example.com",
    ["Api:Timeout"] = "30"
};

var result = await FluentAzure
    .Configuration()
    .FromInMemory(config)
    .BuildAsync()
    .Bind<AppConfiguration>();

result.Match(
    success => Console.WriteLine($"Database: {success.Database.Host}:{success.Database.Port}"),
    errors => Console.WriteLine($"Binding failed: {string.Join(", ", errors)}")
);
```

### Record Type Binding

```csharp
public record AppSettings(
    string Name,
    string Version,
    string Environment,
    int MaxConnections,
    bool EnableFeature
);

var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .BindRecord<AppSettings>();
```

## Configuration Classes

### Simple Configuration

```csharp
public class DatabaseConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

### Nested Configuration

```csharp
public class AppConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public ApiConfig Api { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class ApiConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public int Timeout { get; set; }
    public int RetryCount { get; set; }
}
```

### Record Types

```csharp
public record AppSettings(
    string Name,
    string Version,
    string Environment,
    int MaxConnections,
    bool EnableFeature
);
```

### Init-Only Properties

```csharp
public class SecureConfig
{
    public string ApiKey { get; init; } = string.Empty;
    public string SecretToken { get; init; } = string.Empty;
    public int MaxRetries { get; init; } = 3;
}
```

## Binding Options

### Basic Options

```csharp
var options = new BindingOptions
{
    EnableValidation = true,           // Enable Data Annotations validation
    CaseSensitive = false,             // Case-insensitive key matching
    IgnoreMissingOptional = true       // Ignore missing optional properties
};

var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .Bind<AppConfig>(options);
```

### Custom JSON Options

```csharp
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

var options = new BindingOptions
{
    JsonOptions = jsonOptions
};

var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .BindWithJsonOptions<AppConfig>(jsonOptions);
```

## Collection Binding

### List Binding

```csharp
public class Endpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Timeout { get; set; }
}

// Configuration format:
// Endpoints__0__Name = "Primary"
// Endpoints__0__Url = "https://primary.example.com"
// Endpoints__0__Timeout = "30"
// Endpoints__1__Name = "Secondary"
// Endpoints__1__Url = "https://secondary.example.com"
// Endpoints__1__Timeout = "60"

var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .BindList<Endpoint>("Endpoints");
```

### Dictionary Binding

```csharp
public class ServiceConfig
{
    public string Url { get; set; } = string.Empty;
    public int Timeout { get; set; }
}

// Configuration format:
// Services__api__Url = "https://api.example.com"
// Services__api__Timeout = "30"
// Services__database__Url = "https://db.example.com"
// Services__database__Timeout = "60"

var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .BindDictionary<string, ServiceConfig>("Services");
```

## Validation

### Data Annotations Validation

```csharp
public class ValidatedConfig
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(1, 120)]
    public int Age { get; set; }

    [Url]
    public string Website { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;
}
```

### Custom Validation

```csharp
var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .Bind<LoginConfig>()
    .BindWithValidation(login =>
    {
        if (login.Password.Length < 8)
        {
            return Result<string>.Error("Password must be at least 8 characters long");
        }

        if (login.Password != login.ConfirmPassword)
        {
            return Result<string>.Error("Password and confirmation password do not match");
        }

        return Result<string>.Success("Validation passed");
    });
```

## Extension Methods

### Basic Binding

```csharp
// Standard binding with validation
var result = await config.Bind<AppConfig>();

// JSON binding
var result = await config.BindJson<AppConfig>();

// Record binding
var result = await config.BindRecord<AppSettings>();

// Binding without validation
var result = await config.BindWithoutValidation<AppConfig>();

// Case-sensitive binding
var result = await config.BindCaseSensitive<AppConfig>();
```

### Collection Binding

```csharp
// List binding
var result = await config.BindList<Endpoint>("Endpoints");

// Dictionary binding
var result = await config.BindDictionary<string, ServiceConfig>("Services");
```

### Custom Binding

```csharp
// Custom validation
var result = await config.BindWithValidation<Config>(validator);

// Binding with transformation
var result = await config.BindAndTransform<SourceConfig, TargetConfig>(transformer);
```

## Configuration Key Formats

### Nested Objects

```
Database:Host = "localhost"
Database:Port = "5432"
Database:Name = "myapp"
```

### Arrays and Lists

```
Endpoints__0__Name = "Primary"
Endpoints__0__Url = "https://primary.example.com"
Endpoints__1__Name = "Secondary"
Endpoints__1__Url = "https://secondary.example.com"
```

### Dictionaries

```
Services__api__Url = "https://api.example.com"
Services__api__Timeout = "30"
Services__database__Url = "https://db.example.com"
Services__database__Timeout = "60"
```

## Supported Types

### Primitive Types

- `string`
- `int`, `long`, `short`, `byte`
- `double`, `float`, `decimal`
- `bool`
- `DateTime`, `TimeSpan`
- `Guid`
- `Uri`

### Complex Types

- `enum` (from string values)
- `Nullable<T>` (all primitive types)
- `List<T>`, `IList<T>`, `ICollection<T>`
- `T[]` (arrays)
- `Dictionary<TKey, TValue>`
- Custom classes and records

## Error Handling

### Binding Errors

```csharp
result.Match(
    success =>
    {
        Console.WriteLine("✅ Binding successful!");
        // Use the bound configuration
    },
    errors =>
    {
        Console.WriteLine("❌ Binding failed:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
);
```

### Validation Errors

```csharp
// Example validation errors:
// - Email: The Email field is not a valid e-mail address
// - Age: The field Age must be between 1 and 120
// - RequiredField: The RequiredField field is required
```

## Performance Considerations

### Caching

The binding system includes intelligent caching for:
- Type reflection information
- Property metadata
- Validation attributes

### Memory Usage

- Large configurations are processed efficiently
- Collections are bound incrementally
- No unnecessary object allocations

### Async Operations

All binding operations are async-friendly and can be awaited:
```csharp
var result = await config.BindAsync<AppConfig>();
```

## Best Practices

### 1. Use Strongly-Typed Configuration

```csharp
// ✅ Good
public class DatabaseConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public int MaxConnections { get; set; }
}

// ❌ Avoid
var connectionString = config["Database:ConnectionString"];
var maxConnections = int.Parse(config["Database:MaxConnections"]);
```

### 2. Validate Configuration

```csharp
public class AppConfig
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Timeout { get; set; }

    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
```

### 3. Use Record Types for Immutable Configuration

```csharp
public record AppSettings(
    string Name,
    string Version,
    string Environment,
    int MaxConnections
);
```

### 4. Handle Errors Gracefully

```csharp
var result = await config.Bind<AppConfig>();
if (result.IsFailure)
{
    logger.LogError("Configuration binding failed: {Errors}", 
        string.Join(", ", result.Errors));
    // Provide fallback configuration or exit gracefully
}
```

### 5. Use Appropriate Collection Types

```csharp
// For fixed-size collections
public Endpoint[] Endpoints { get; set; } = Array.Empty<Endpoint>();

// For variable-size collections
public List<ServiceConfig> Services { get; set; } = new();

// For key-value mappings
public Dictionary<string, string> Settings { get; set; } = new();
```

## Migration from Traditional Binding

### Before (Traditional)

```csharp
var builder = new ConfigurationBuilder();
builder.AddEnvironmentVariables();
var config = builder.Build();

var connectionString = config["Database:ConnectionString"];
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("ConnectionString is required");
}

var timeout = int.Parse(config["Database:Timeout"] ?? "30");
var maxConnections = int.Parse(config["Database:MaxConnections"] ?? "100");
```

### After (FluentAzure)

```csharp
var result = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .BuildAsync()
    .Bind<DatabaseConfig>();

result.Match(
    success => {
        // Use success.ConnectionString, success.Timeout, etc.
    },
    errors => {
        // Handle validation errors
    }
);
```

## Troubleshooting

### Common Issues

1. **Property not found**: Ensure configuration keys match property names
2. **Type conversion errors**: Check that string values can be converted to target types
3. **Validation failures**: Review Data Annotations on your configuration classes
4. **Collection binding issues**: Verify array index format (e.g., `Items__0__Name`)

### Debug Tips

1. Enable detailed logging to see binding process
2. Use `BindWithoutValidation` to isolate binding vs validation issues
3. Check property paths in error messages
4. Verify configuration key formats match expected patterns

## Examples

See the `examples/Demo/EnhancedBindingExamples.cs` file for comprehensive examples demonstrating all features of the enhanced binding system. 
