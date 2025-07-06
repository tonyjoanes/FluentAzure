# Enhanced Azure Key Vault Configuration Source

The FluentAzure Key Vault configuration source provides a robust, production-ready solution for loading secrets from Azure Key Vault with advanced features like retry logic, caching, secret versioning, and comprehensive error handling.

## 🎯 Key Features

✅ **DefaultAzureCredential Support** - Seamless authentication across development and production environments  
✅ **Exponential Backoff Retry** - Configurable retry logic with jitter for resilient API calls  
✅ **Secret Versioning** - Support for specific secret versions or latest version  
✅ **In-Memory Caching** - Configurable TTL caching to reduce API calls and improve performance  
✅ **Graceful Error Handling** - Partial success scenarios with detailed error reporting  
✅ **Advanced Key Mapping** - Flexible transformation of Key Vault secret names to configuration keys  
✅ **Prefix Filtering** - Load only secrets matching a specific prefix  
✅ **Thread-Safe Operations** - Concurrent access support with thread-safe caching  
✅ **Comprehensive Logging** - Detailed logging for monitoring and debugging  
✅ **Multiple Authentication Methods** - Support for Managed Identity, Service Principal, and more  

## 🚀 Quick Start

### Basic Usage

```csharp
var config = await FluentAzure
    .Configuration()
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
var config = await FluentAzure
    .Configuration()
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
| `Credential` | `TokenCredential?` | `null` | Azure credential (uses DefaultAzureCredential if null) |
| `MaxRetryAttempts` | `int` | `3` | Maximum number of retry attempts |
| `BaseRetryDelay` | `TimeSpan` | `1 second` | Base delay for exponential backoff |
| `MaxRetryDelay` | `TimeSpan` | `30 seconds` | Maximum delay between retry attempts |
| `CacheDuration` | `TimeSpan` | `5 minutes` | Cache duration for secrets (set to zero to disable) |
| `ContinueOnSecretFailure` | `bool` | `true` | Whether to continue loading other secrets when one fails |
| `KeyMapper` | `Func<string, string>` | `Replace("--", ":")` | Function to transform secret names to config keys |
| `SecretVersion` | `string?` | `null` | Specific secret version to retrieve |
| `SecretNamePrefix` | `string?` | `null` | Prefix filter for secret names |
| `ReloadFailedSecrets` | `bool` | `true` | Whether to reload secrets that failed during initial load |
| `OperationTimeout` | `TimeSpan` | `30 seconds` | Timeout for Key Vault operations |

## 🔐 Authentication Methods

### 1. Default Azure Credential (Recommended)

```csharp
// Uses DefaultAzureCredential - automatically tries multiple credential types
.FromKeyVault("https://your-keyvault.vault.azure.net/")
```

### 2. Managed Identity

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

### 4. Custom Credential

```csharp
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
.FromKeyVault("https://your-keyvault.vault.azure.net/", credential)
```

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
// ✅ Use DefaultAzureCredential for automatic credential detection
.FromKeyVault(vaultUrl)

// ✅ Use Managed Identity in Azure environments
.FromKeyVaultWithManagedIdentity(vaultUrl)

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
var testConfig = await FluentAzure
    .Configuration()
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
var config = await FluentAzure
    .Configuration()
    .FromKeyVault(testVaultUrl, options =>
    {
        options.SecretNamePrefix = "Test-";
        options.CacheDuration = TimeSpan.Zero; // Disable caching for tests
    })
    .BuildAsync();
```

## 📚 Common Scenarios

### 1. ASP.NET Core Integration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

var config = await FluentAzure
    .Configuration()
    .FromJsonFile("appsettings.json")
    .FromEnvironment()
    .FromKeyVault(builder.Configuration["KeyVault:Url"])
    .BuildAsync();

config.Match(
    success => builder.Services.AddSingleton<IConfiguration>(new ConfigurationRoot(success)),
    errors => throw new InvalidOperationException($"Configuration failed: {string.Join(", ", errors)}")
);
```

### 2. Azure Functions

```csharp
[FunctionName("MyFunction")]
public async Task<IActionResult> Run([HttpTrigger] HttpRequest req, ILogger log)
{
    var config = await FluentAzure
        .Configuration()
        .FromEnvironment()
        .FromKeyVaultWithManagedIdentity(Environment.GetEnvironmentVariable("KeyVault:Url"))
        .BuildAsync();

    return config.Match(
        success => new OkObjectResult(success),
        errors => new BadRequestObjectResult(errors)
    );
}
```

### 3. Background Services

```csharp
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private KeyVaultSource _kvSource;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _kvSource = new KeyVaultSource(
            Environment.GetEnvironmentVariable("KeyVault:Url")!, 
            logger: logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Reload configuration periodically
            await _kvSource.ReloadAsync();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## 🔗 Related Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [DefaultAzureCredential Documentation](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Polly Retry Library](https://github.com/App-vNext/Polly)
- [FluentAzure Configuration Builder](./configuration-builder.md)

## 🐛 Troubleshooting

### Common Issues

1. **Authentication Failures**
   - Verify Key Vault access policies
   - Check Azure RBAC permissions
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
   - Check `LoadErrors` property for specific failures
   - Verify secret names and permissions
   - Review retry configuration

### Performance Optimization

- Use prefix filtering to reduce API calls
- Set appropriate cache duration
- Monitor Key Vault throttling limits
- Consider using multiple Key Vaults for high-throughput scenarios

---

*Built with ❤️ by the FluentAzure team* 
