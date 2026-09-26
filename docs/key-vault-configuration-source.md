# Enhanced Azure Key Vault Configuration Source

The FluentAzure Key Vault configuration source provides a robust, production-ready solution for loading secrets from Azure Key Vault with advanced features like retry logic, caching, secret versioning, and comprehensive error handling.

## 🎯 Key Features

✅ **Pipeline Identity** - One credential for every Azure source: `UseManagedIdentity()`, `UseWorkloadIdentity()`, `UseCredential(...)`, or `DefaultAzureCredential` by default  
✅ **Exponential Backoff Retry** - Configurable retry logic with jitter for resilient API calls  
✅ **Secret Versioning** - Support for specific secret versions or latest version  
✅ **In-Memory Caching** - Configurable TTL caching to reduce API calls and improve performance  
✅ **Graceful Error Handling** - Partial success scenarios with detailed error reporting  
✅ **Advanced Key Mapping** - Flexible transformation of Key Vault secret names to configuration keys  
✅ **Prefix Filtering** - Load only secrets matching a specific prefix  
✅ **Thread-Safe Operations** - Concurrent access support with thread-safe caching  
✅ **Comprehensive Logging** - Detailed logging for monitoring and debugging  
✅ **Multiple Authentication Methods** - Support for Managed Identity, Service Principal, and more  
✅ **Active Secrets Only** - Disabled, expired and not-yet-active secrets are skipped  
✅ **Throttling-Aware** - Secrets are fetched in parallel with a concurrency cap (`MaxConcurrentSecretLoads`)  
✅ **Reload & Rotation** - `ReloadAsync()`, or periodic reload into `IOptionsMonitor<T>` via the [IConfiguration provider](configuration-integration.md)  
✅ **Secrets Stay Secret** - Values are tracked as sensitive, masked by `GetRedactedDebugView()` and never included in errors or telemetry  

## 🚀 Quick Start

### Basic Usage

```csharp
var config = await FluentConfig
    .Create()
    .FromEnvironment()
    .FromKeyVault("https://your-keyvault.vault.azure.net/")
    .BuildAsync();

config.Match(
    success => Console.WriteLine($"Loaded {success.Count} configuration values"),
    errors => Console.WriteLine($"Failed to load: {string.Join(", ", errors)}")
);
```

### Advanced Configuration

```csharp
var config = await FluentConfig
    .Create()
    .FromKeyVault("https://your-keyvault.vault.azure.net/", options =>
    {
        options.CacheDuration = TimeSpan.FromMinutes(10);
        options.MaxRetryAttempts = 5;
        options.BaseRetryDelay = TimeSpan.FromSeconds(2);
        options.MaxRetryDelay = TimeSpan.FromMinutes(1);
        options.ContinueOnSecretFailure = true;
        options.SecretNamePrefix = "MyApp-";
        options.KeyMapper = secretName => secretName.Replace("-", ":");
    })
    .BuildAsync();
```

## 🔧 Configuration Options

### KeyVaultConfiguration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Credential` | `TokenCredential?` | `null` | Credential for this vault. When `null`, the pipeline credential is used (see [Authentication](#-authentication-methods)) |
| `MaxRetryAttempts` | `int` | `3` | Maximum number of retry attempts |
| `BaseRetryDelay` | `TimeSpan` | `1 second` | Base delay for exponential backoff |
| `MaxRetryDelay` | `TimeSpan` | `30 seconds` | Maximum delay between retry attempts |
| `CacheDuration` | `TimeSpan` | `5 minutes` | Cache duration for secrets (set to zero to disable) |
| `ContinueOnSecretFailure` | `bool` | `true` | Whether to continue loading other secrets when one fails |
| `KeyMapper` | `Func<string, string>` | `Replace("--", ":")` | Function to transform secret names to config keys |
| `SecretVersion` | `string?` | `null` | Specific secret version to retrieve |
| `SecretNamePrefix` | `string?` | `null` | Prefix filter for secret names |
| `ReloadFailedSecrets` | `bool` | `true` | Obsolete; has no effect, because every reload fetches all secrets. Will be removed in 1.0 |
| `OperationTimeout` | `TimeSpan` | `30 seconds` | Timeout for Key Vault operations |
| `MaxConcurrentSecretLoads` | `int` | `8` | Maximum secrets fetched concurrently during a load, to stay under Key Vault throttling limits |

## 🔐 Authentication Methods

### 1. Pipeline identity (recommended)

Set one identity for every Azure source in the pipeline. It can be set before or after `FromKeyVault`:

```csharp
FluentConfig.Create()
    .UseManagedIdentity()                  // or UseManagedIdentity("<client-id>"), UseWorkloadIdentity(), UseCredential(cred)
    .FromKeyVault("https://your-keyvault.vault.azure.net/");
```

Without any of these, `DefaultAzureCredential` is used, with one shared instance per pipeline. If you rely on it in production, set `AZURE_TOKEN_CREDENTIALS=prod` so it doesn't fall back to developer credentials. See [Identity & secrets](identity-and-redaction.md).

### 2. Per-vault managed identity

```csharp
// System-assigned managed identity
.FromKeyVaultWithManagedIdentity("https://your-keyvault.vault.azure.net/")

// User-assigned managed identity
.FromKeyVaultWithManagedIdentity("https://your-keyvault.vault.azure.net/", "client-id")
```

### 3. Service Principal

```csharp
.FromKeyVaultWithServicePrincipal(
    "https://your-keyvault.vault.azure.net/",
    clientId: "your-client-id",
    clientSecret: "your-client-secret",
    tenantId: "your-tenant-id"
)
```

Load the client secret from configuration or the environment; never hard-code it.

### 4. Custom credential or client

```csharp
.FromKeyVault("https://your-keyvault.vault.azure.net/", credential)

// Or bring a fully configured SecretClient (custom SecretClientOptions, a test double, ...)
var client = new SecretClient(new Uri(vaultUrl), credential, new SecretClientOptions { /* ... */ });
FluentConfig.Create().AddSource(new KeyVaultSource(client));
```

A per-vault credential always takes precedence over the pipeline credential. When you pass a `SecretClient`, its own credential and retry options are used.

### Required permissions

Grant the identity the **Key Vault Secrets User** role (Azure RBAC). FluentAzure lists secret properties and reads secret values; it never writes.

## 📥 Which Secrets Are Loaded

A load lists the vault's secrets and reads the value of each one that:
- is **enabled**;
- has not **expired** (`ExpiresOn`);
- is **active** (`NotBefore` has passed);
- matches `SecretNamePrefix`, if one is set.

Values are read in parallel, at most `MaxConcurrentSecretLoads` (default 8) at a time, to stay under Key Vault's throttling limits. Concurrent `LoadAsync` calls share a single load.

To avoid reading secrets the application doesn't need, keep application secrets in their own vault or behind a `SecretNamePrefix`.

## 🔁 Reloading and Rotation

`LoadAsync` caches its result. `ReloadAsync()` clears the cache and reads the vault again:

```csharp
var source = new KeyVaultSource(vaultUrl);
await source.LoadAsync();
// ... a secret is rotated ...
await source.ReloadAsync();
```

In applications, let the [IConfiguration provider](configuration-integration.md#reloading-and-ioptionsmonitor) do this for you. With `reloadInterval`, the pipeline reloads Key Vault periodically, pushes changed values to `IOptionsMonitor<T>`, and keeps the last good values if a reload fails:

```csharp
await builder.Configuration.AddFluentAzureAsync(
    fluent => fluent.UseManagedIdentity().FromKeyVault(vaultUrl),
    reloadInterval: TimeSpan.FromMinutes(15));
```

Every value loaded from Key Vault is treated as a secret. It is masked by `GetRedactedDebugView()` and never appears in FluentAzure's errors, telemetry or health data.

## 🗂️ Key Mapping Examples

### Default Mapping (Hierarchical Configuration)

Key Vault secret names with `--` are converted to hierarchical configuration keys:

```
Key Vault Secret    →    Configuration Key
Database--Host      →    Database:Host
Database--Port      →    Database:Port
Api--Key            →    Api:Key
```

### Custom Key Mapping

```csharp
.FromKeyVault(vaultUrl, secretName => 
{
    // Custom mapping logic
    return secretName.ToLower().Replace("-", ":");
})
```

### Environment-Specific Prefixes

```csharp
// Only load secrets starting with "Prod-"
.FromKeyVaultWithPrefix("https://your-keyvault.vault.azure.net/", "Prod-")
```

## 📊 Caching and Performance

### Cache Configuration

```csharp
// Custom cache duration
.FromKeyVaultWithCaching(vaultUrl, TimeSpan.FromMinutes(15))

// Disable caching
.FromKeyVault(vaultUrl, options => options.CacheDuration = TimeSpan.Zero)
```

### Cache Statistics

```csharp
var kvSource = new KeyVaultSource(vaultUrl);
await kvSource.LoadAsync();

var stats = kvSource.CacheStatistics;
// Returns: TotalEntries, ValidEntries, ExpiredEntries, CacheHitRate
```

### Manual Cache Management

```csharp
var kvSource = new KeyVaultSource(vaultUrl);
await kvSource.LoadAsync();

// Clear cache
kvSource.ClearCache();

// Reload from Key Vault
await kvSource.ReloadAsync();
```

## 🔄 Retry Logic and Error Handling

### Retry Configuration

```csharp
.FromKeyVaultWithRetry(
    vaultUrl,
    maxRetryAttempts: 5,
    baseRetryDelay: TimeSpan.FromSeconds(2),
    maxRetryDelay: TimeSpan.FromMinutes(1)
)
```

### Error Handling Strategies

```csharp
// Fail fast - stop on first secret failure
.FromKeyVault(vaultUrl, options => options.ContinueOnSecretFailure = false)

// Partial success - continue loading other secrets when one fails
.FromKeyVault(vaultUrl, options => options.ContinueOnSecretFailure = true)
```

### Accessing Load Errors

```csharp
var kvSource = new KeyVaultSource(vaultUrl);
var result = await kvSource.LoadAsync();

if (kvSource.LoadErrors.Count > 0)
{
    foreach (var error in kvSource.LoadErrors)
    {
        Console.WriteLine($"Error: {error}");
    }
}
```

## 🏷️ Secret Versioning

### Latest Version (Default)

```csharp
.FromKeyVault(vaultUrl) // Gets latest version of all secrets
```

### Specific Version

```csharp
.FromKeyVault(vaultUrl, "version-id") // Gets specific version of all secrets
```

### Per-Secret Version Control

```csharp
var kvSource = new KeyVaultSource(vaultUrl);
var specificSecret = await kvSource.GetSecretAsync("MySecret", "version-123");
```

## 🔍 Monitoring and Diagnostics

### Logging Integration

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

.FromKeyVault(vaultUrl, options => { /* config */ }, logger)
```

### Log Levels

- **Information**: Successful operations, cache hits, configuration summary
- **Warning**: Retry attempts, partial failures, cache misses
- **Error**: Authentication failures, network errors, configuration failures
- **Debug**: Detailed cache operations, individual secret loading

## 📋 Best Practices

### 1. Authentication

```csharp
// ✅ Use a managed identity in Azure environments
FluentConfig.Create().UseManagedIdentity().FromKeyVault(vaultUrl)

// ✅ If you keep DefaultAzureCredential, set AZURE_TOKEN_CREDENTIALS=prod in production
FluentConfig.Create().FromKeyVault(vaultUrl)

// ❌ Avoid hardcoding credentials
// Don't: new ClientSecretCredential("tenant", "client", "hardcoded-secret")
```

### 2. Naming Conventions

```csharp
// ✅ Use hierarchical naming with '--' separator
// Key Vault: "MyApp--Database--ConnectionString"
// Config Key: "MyApp:Database:ConnectionString"

// ✅ Use environment prefixes
// "Prod--Database--Host", "Dev--Database--Host"
```

### 3. Error Handling

```csharp
// ✅ Handle both success and failure cases
config.Match(
    success => ConfigureApplication(success),
    errors => LogErrorsAndUseDefaults(errors)
);

// ✅ Use partial success for non-critical secrets
options.ContinueOnSecretFailure = true;
```

### 4. Performance Optimization

```csharp
// ✅ Use appropriate cache duration
options.CacheDuration = TimeSpan.FromMinutes(5); // Balance between performance and freshness

// ✅ Use prefix filtering to reduce API calls
options.SecretNamePrefix = "MyApp-";

// ✅ Configure reasonable retry settings
options.MaxRetryAttempts = 3;
options.BaseRetryDelay = TimeSpan.FromSeconds(1);
```

### 5. Security Considerations

```csharp
// ✅ Use least privilege access policies in Key Vault
// ✅ Rotate secrets regularly
// ✅ Monitor Key Vault access logs
// ✅ Use Azure RBAC for fine-grained permissions

// ❌ Don't log sensitive values
// Don't: logger.LogInformation("Secret value: {Value}", secretValue);
```

## 🧪 Testing

### Unit Testing with Mock Data

```csharp
// Use InMemorySource for testing
var testConfig = await FluentConfig
    .Create()
    .FromInMemory(new Dictionary<string, string>
    {
        ["Database:Host"] = "localhost",
        ["Database:Port"] = "5432"
    })
    .BuildAsync();
```

### Integration Testing

```csharp
// Use a test Key Vault with non-sensitive data
var testVaultUrl = "https://test-keyvault.vault.azure.net/";
var config = await FluentConfig
    .Create()
    .FromKeyVault(testVaultUrl, options =>
    {
        options.SecretNamePrefix = "Test-";
        options.CacheDuration = TimeSpan.Zero; // Disable caching for tests
    })
    .BuildAsync();
```

## 📚 Common Scenarios

### 1. ASP.NET Core

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

await builder.Configuration.AddFluentAzureAsync(
    fluent => fluent
        .UseManagedIdentity()
        .FromKeyVault(builder.Configuration["KeyVault:Url"]!)
        .Required("Database:ConnectionString"),
    reloadInterval: TimeSpan.FromMinutes(15));

builder.Services.AddFluentAzureOptions<DatabaseOptions>(builder.Configuration, "Database");
```

A missing `Database:ConnectionString` stops startup with a `FluentAzureConfigurationException` that lists every error.

### 2. Azure Functions (isolated worker)

Load configuration once at startup, not per invocation:

```csharp
// Program.cs
var builder = FunctionsApplication.CreateBuilder(args);

await builder.Configuration.AddFluentAzureAsync(fluent => fluent
    .UseManagedIdentity()
    .FromKeyVault(Environment.GetEnvironmentVariable("KeyVault__Url")!));

builder.Services.AddFluentAzureOptions<ServiceBusOptions>(builder.Configuration, "ServiceBus");

builder.Build().Run();
```

Functions then take `IOptions<ServiceBusOptions>` or `IOptionsMonitor<ServiceBusOptions>` through dependency injection.

### 3. Background services with rotated secrets

Use a reload interval and read the current value through `IOptionsMonitor<T>` on each use, rather than reloading the source yourself:

```csharp
public class Worker(IOptionsMonitor<ApiOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var api = options.CurrentValue;   // reflects the latest successful reload
            // ... call the API with api.Key ...
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## 🔗 Related Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [DefaultAzureCredential Documentation](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Polly Retry Library](https://github.com/App-vNext/Polly)
- [IConfiguration integration & reload](configuration-integration.md)
- [Identity & secrets](identity-and-redaction.md)
- [Observability: telemetry & health checks](observability.md)

## 🐛 Troubleshooting

### Common Issues

1. **Authentication Failures**
   - Grant the identity the **Key Vault Secrets User** role (or the equivalent access policy on vaults that still use them)
   - Check which identity is in use: set it explicitly with `UseManagedIdentity()` / `UseCredential(...)`
   - Ensure correct credential configuration

2. **Timeout Errors**
   - Increase `OperationTimeout` setting
   - Check network connectivity
   - Verify Key Vault endpoint is accessible

3. **Cache Issues**
   - Clear cache manually: `kvSource.ClearCache()`
   - Adjust cache duration based on secret change frequency
   - Monitor cache statistics for optimization

4. **Partial Loading**
   - Check the `LoadErrors` property for specific failures
   - Disabled, expired and not-yet-active secrets are skipped by design
   - Verify secret names and permissions
   - Review retry configuration

### Performance Optimization

- Use prefix filtering to reduce API calls
- Set appropriate cache duration
- Monitor Key Vault throttling limits
- Consider using multiple Key Vaults for high-throughput scenarios

---

*Built with ❤️ by the FluentAzure team* 
