# FluentAzure Real-World Examples

This directory contains comprehensive, production-ready examples demonstrating how to use FluentAzure's strongly-typed configuration in real-world scenarios.

## 🚀 Examples Overview

### 1. Azure Functions Example
**Location**: `AzureFunctions.Example/`

A complete Azure Functions v4 application demonstrating:
- ✅ **Strongly-typed configuration** with validation
- ✅ **Key Vault integration** for secrets management
- ✅ **Health check function** with comprehensive service monitoring
- ✅ **Dependency injection** with configuration-driven services
- ✅ **Telemetry and logging** with Application Insights
- ✅ **Error handling** and graceful degradation

### 2. Web API Example
**Location**: `WebApi.Example/`

A full-featured ASP.NET Core Web API demonstrating:
- ✅ **Enterprise-level configuration** with multiple sources
- ✅ **JWT authentication** with strongly-typed settings
- ✅ **Entity Framework** integration with configuration
- ✅ **Rate limiting** and security features
- ✅ **File upload** with Azure Storage
- ✅ **CORS and security** configuration
- ✅ **Swagger/OpenAPI** documentation

## 🛠️ Getting Started

### Prerequisites
- .NET 8.0 SDK
- Azure subscription (for Key Vault, Storage, Service Bus)
- Azure Storage Emulator (for local development)
- SQL Server (local or Azure)

### Quick Start

#### 1. Azure Functions Example

```bash
cd examples/AzureFunctions.Example

# Update local.settings.json with your Azure resources
# Set your Key Vault URL, connection strings, etc.

# Run the function app
func start
```

**Key Features Demonstrated:**
- Configuration loading from multiple sources
- Health check endpoint with service monitoring
- Dependency injection with strongly-typed config
- Error handling and logging

#### 2. Web API Example

```bash
cd examples/WebApi.Example

# Update appsettings.json with your configuration
# Set connection strings, JWT settings, etc.

# Run the API
dotnet run
```

**Key Features Demonstrated:**
- JWT authentication with configuration
- User management with CRUD operations
- File upload to Azure Storage
- Rate limiting and security
- Swagger documentation

## 📋 Configuration Examples

### Azure Functions Configuration

```csharp
// Program.cs - Configuration setup
var configResult = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .FromKeyVault(Environment.GetEnvironmentVariable("KeyVaultUrl"))
    .Required("DatabaseConnectionString")
    .Required("ServiceBusConnectionString")
    .Required("StorageConnectionString")
    .Optional("MaxRetryCount", "3")
    .Optional("TimeoutSeconds", "30")
    .BuildAsync()
    .Bind<FunctionConfiguration>();
```

### Web API Configuration

```csharp
// Program.cs - Configuration setup
var configResult = await FluentAzure
    .Configuration()
    .FromJsonFile("appsettings.json")
    .FromEnvironment()
    .FromKeyVault(builder.Configuration["KeyVault:Url"])
    .Required("ConnectionStrings:DefaultConnection")
    .Required("Jwt:SecretKey")
    .Required("Jwt:Issuer")
    .Required("Jwt:Audience")
    .BuildAsync()
    .Bind<WebApiConfiguration>();
```

## 🏗️ Architecture Patterns

### 1. Configuration-Driven Services

```csharp
// Service constructor with configuration injection
public class DatabaseService : IDatabaseService
{
    private readonly DatabaseConfig _config;
    private readonly RetryConfig _retryConfig;

    public DatabaseService(FunctionConfiguration config, ILogger<DatabaseService> logger)
    {
        _config = config.Database;
        _retryConfig = config.Retry;
        // Use configuration for service setup
    }
}
```

### 2. Validation and Error Handling

```csharp
// Configuration validation with clear error messages
var config = configResult.Match(
    success =>
    {
        logger.LogInformation("✅ Configuration loaded successfully");
        return success;
    },
    errors =>
    {
        logger.LogError("❌ Configuration failed: {Errors}", string.Join(", ", errors));
        throw new InvalidOperationException($"Configuration failed: {string.Join(", ", errors)}");
    }
);
```

### 3. Feature Flags and Conditional Logic

```csharp
// Use configuration to enable/disable features
if (_config.Telemetry.EnableTelemetry)
{
    await _telemetryService.TrackEventAsync("HealthCheck", properties);
}

if (_config.Security.EnableAuditLogging)
{
    await _auditService.LogUserCreatedAsync(user.Id, currentUser);
}
```

## 🔧 Configuration Sources

### Environment Variables
```bash
# Azure Functions
DatabaseConnectionString="Server=..."
ServiceBusConnectionString="Endpoint=..."
StorageConnectionString="DefaultEndpointsProtocol=..."

# Web API
ConnectionStrings__DefaultConnection="Server=..."
Jwt__SecretKey="your-secret-key"
Jwt__Issuer="https://your-api.com"
```

### Key Vault Integration
```csharp
// Secrets stored in Azure Key Vault
.FromKeyVault("https://your-keyvault.vault.azure.net/")
.Required("DatabaseConnectionString")  // Fetched from Key Vault
.Required("JwtSecretKey")             // Fetched from Key Vault
```

### JSON Configuration
```json
{
  "Database": {
    "MaxRetryCount": 3,
    "CommandTimeoutSeconds": 30
  },
  "Security": {
    "MinPasswordLength": 12,
    "RequireSpecialCharacters": true
  }
}
```

## 🧪 Testing the Examples

### Azure Functions Testing

1. **Health Check Endpoint**
   ```bash
   curl http://localhost:7071/api/healthcheck
   ```

2. **Expected Response**
   ```json
   {
     "status": "Healthy",
     "timestamp": "2024-01-15T10:30:00Z",
     "configuration": {
       "database": {
         "host": "localhost",
         "port": 1433,
         "database": "FunctionExample"
       }
     },
     "services": [
       {
         "service": "Database",
         "isHealthy": true,
         "details": { ... }
       }
     ]
   }
   ```

### Web API Testing

1. **Swagger Documentation**
   - Navigate to `http://localhost:5000` for Swagger UI
   - Test endpoints with authentication

2. **User Management**
   ```bash
   # Get users (requires admin role)
   curl -H "Authorization: Bearer <token>" \
        http://localhost:5000/api/users

   # Create user (requires admin role)
   curl -X POST -H "Authorization: Bearer <token>" \
        -H "Content-Type: application/json" \
        -d '{"name":"John Doe","email":"john@example.com","password":"SecurePass123!"}' \
        http://localhost:5000/api/users
   ```

## 🔒 Security Features

### JWT Authentication
```csharp
// Strongly-typed JWT configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = config.Jwt.ValidateIssuer,
            ValidateAudience = config.Jwt.ValidateAudience,
            ValidIssuer = config.Jwt.Issuer,
            ValidAudience = config.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config.Jwt.SecretKey))
        };
    });
```

### Rate Limiting
```csharp
// Configuration-driven rate limiting
if (_config.RateLimit.EnableRateLimiting)
{
    var isRateLimited = await CheckRateLimitAsync(clientId);
    if (isRateLimited)
    {
        return StatusCode(429, new { Message = "Rate limit exceeded" });
    }
}
```

### Password Validation
```csharp
// Security configuration validation
private bool ValidatePasswordStrength(string password)
{
    if (password.Length < _config.Security.MinPasswordLength)
        return false;

    if (_config.Security.RequireSpecialCharacters && 
        !password.Any(c => !char.IsLetterOrDigit(c)))
        return false;

    return true;
}
```

## 📊 Monitoring and Telemetry

### Application Insights Integration
```csharp
// Configuration-driven telemetry
if (config.Telemetry.EnableTelemetry)
{
    logging.AddApplicationInsights();
    
    await _telemetryService.TrackEventAsync("HealthCheck", properties);
    await _telemetryService.TrackMetricAsync("HealthCheck.Duration", duration);
}
```

### Structured Logging
```csharp
// Configuration-aware logging
logger.LogInformation("🚀 Web API started with configuration:");
logger.LogInformation("Database: {Database}", config.Database.Name);
logger.LogInformation("Storage: {Storage}", config.Storage.AccountName);
logger.LogInformation("Service Bus: {ServiceBus}", config.ServiceBus.Namespace);
```

## 🚀 Production Deployment

### Azure Functions Deployment
```bash
# Deploy to Azure
az functionapp deployment source config-zip \
    --resource-group your-rg \
    --name your-function-app \
    --src ./publish.zip
```

### Web API Deployment
```bash
# Deploy to Azure App Service
az webapp deployment source config-zip \
    --resource-group your-rg \
    --name your-web-app \
    --src ./publish.zip
```

### Environment-Specific Configuration
```csharp
// Use different configuration sources per environment
var configBuilder = FluentAzure.Configuration();

if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
{
    configBuilder.FromKeyVault(keyVaultUrl);
}
else
{
    configBuilder.FromJsonFile("appsettings.Development.json");
}
```

## 🎯 Best Practices Demonstrated

1. **Configuration Validation**: All configuration is validated at startup
2. **Error Handling**: Graceful handling of configuration errors
3. **Security**: Secrets managed through Key Vault
4. **Monitoring**: Comprehensive logging and telemetry
5. **Performance**: Efficient configuration loading and caching
6. **Maintainability**: Clean separation of concerns
7. **Testability**: Dependency injection for easy testing

## 📚 Additional Resources

- [FluentAzure Documentation](../docs/)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)

---

**Ready to build robust, configuration-driven applications? These examples show you how! 🚀** 
