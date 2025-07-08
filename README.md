# FluentAzure

A fluent, functional, and type-safe NuGet package for Azure configuration and secrets management.

![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)
![Azure](https://img.shields.io/badge/Azure-Functions%20%7C%20WebApps%20%7C%20Services-orange.svg)
![Fluent](https://img.shields.io/badge/Style-Fluent%20%7C%20Functional-purple.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)

## 🎯 Problem This Solves

Azure developers constantly struggle with:
- **Multiple configuration sources** (Environment variables, Key Vault, App Configuration, JSON files)
- **Complex error handling** when secrets are missing or invalid
- **No type safety** in configuration access
- **Imperative, verbose code** for simple configuration scenarios
- **Poor testing experience** for configuration-dependent code

## 🚀 Solution: Functional Configuration Pipeline

Instead of this imperative mess:
```csharp
// Traditional approach - verbose and error-prone
var builder = new ConfigurationBuilder();
builder.AddEnvironmentVariables();
builder.AddAzureKeyVault(vaultUrl);
var config = builder.Build();

var connectionString = config["ConnectionString"];
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("ConnectionString is required");
}

var timeout = int.Parse(config["Timeout"] ?? "30");
// ... more boilerplate
```

Write this fluent pipeline:
```csharp
// FluentAzure approach - clean and safe
var config = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .FromKeyVault("https://myvault.vault.azure.net")
    .Required("ConnectionString")
    .Optional("Timeout", 30)
    .Validate(c => c.ConnectionString.StartsWith("Server="))
    .BuildAsync()
    .Match(
        success => success,
        errors => throw new ConfigurationException(errors)
    );
```

## 📦 Installation

```bash
dotnet add package FluentAzure
```

Or add to your `.csproj`:
```xml
<PackageReference Include="FluentAzure" Version="1.0.0" />
```

## 📖 Example Usage Patterns

#### **Simple Configuration**
```csharp
var config = await FluentAzure
    .Configuration()
    .FromEnvironment()
    .Required("DATABASE_URL")
    .Optional("CACHE_TTL", "300")
    .BuildAsync();
```

#### **Complex Enterprise Setup**
```csharp
var config = await FluentAzure
    .Configuration()
    .ForEnvironment(Environment.Production)
    .FromJsonFile("appsettings.json")
    .FromEnvironment()
    .FromKeyVault("https://company-prod-kv.vault.azure.net")
    .FromAppConfiguration("company-prod-appconfig")
    .Transform("ConnectionString", DecryptConnectionString)
    .Validate(c => Uri.IsWellFormedUriString(c.ServiceUrl, UriKind.Absolute))
    .Bind<AppConfiguration>()
    .WithRefreshInterval(TimeSpan.FromMinutes(5))
    .BuildAsync();
```

#### **Dependency Injection**
```csharp
// Program.cs
builder.Services.AddFluentAzure(config => config
    .FromEnvironment()
    .FromKeyVault(builder.Configuration["KeyVault:Url"])
    .Bind<AppSettings>()
);

// Controller
public class ApiController : ControllerBase
{
    private readonly AppSettings _settings;

    public ApiController(AppSettings settings)
    {
        _settings = settings;
    }
}
```

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Azure subscription (for Key Vault/App Configuration)

### Quick Start
1. Install the package: `dotnet add package FluentAzure`
2. Add using statement: `using FluentAzure;`
3. Use the fluent API to build your configuration
4. Handle results with the `Match` method for type-safe error handling

### Features
- **Fluent API**: Chain configuration sources with readable syntax
- **Type Safety**: Compile-time validation and runtime error handling
- **Multiple Sources**: Environment variables, Key Vault, JSON files, and more
- **Dependency Injection**: Seamless integration with .NET DI container
- **Performance**: Intelligent caching and lazy loading

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on:
- Code style and standards
- Testing requirements
- Pull request process
- Development setup

## 📚 Resources

- [Functional Programming in C#](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-9#records)
- [Azure Key Vault Developer Guide](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Result Pattern in C#](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [Monads in C#](https://mikhail.io/2018/07/monads-explained-in-csharp-again/)

---

**Ready to revolutionize Azure configuration management? Let's build something awesome! 🚀**
