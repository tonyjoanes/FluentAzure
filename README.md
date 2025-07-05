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

## 📁 Project Structure for Cursor IDE

```
FluentAzure/
├── src/
│   └── FluentAzure/
│       ├── Core/                          # Core fluent abstractions
│       │   ├── FluentAzure.cs             # Main entry point
│       │   ├── ConfigurationBuilder.cs    # Fluent builder
│       │   ├── ConfigurationResult.cs     # Result<T> monad
│       │   ├── ConfigurationError.cs      # Error types
│       │   └── ConfigurationValue.cs      # Strongly-typed values
│       ├── Sources/                       # Configuration sources
│       │   ├── IConfigurationSource.cs    # Source abstraction
│       │   ├── EnvironmentSource.cs       # Environment variables
│       │   ├── KeyVaultSource.cs          # Azure Key Vault
│       │   ├── AppConfigurationSource.cs  # Azure App Configuration
│       │   ├── JsonFileSource.cs          # JSON file support
│       │   └── InMemorySource.cs          # For testing
│       ├── Transforms/                    # Value transformations
│       │   ├── ITransform.cs              # Transform abstraction
│       │   ├── EncryptionTransforms.cs    # Encrypt/decrypt values
│       │   ├── ValidationTransforms.cs    # Validation rules
│       │   └── TypeConversionTransforms.cs # String to T conversion
│       ├── Extensions/                    # DI and framework integration
│       │   ├── ServiceCollectionExtensions.cs
│       │   ├── HostBuilderExtensions.cs
│       │   └── WebApplicationExtensions.cs
│       └── FluentAzure.csproj
├── tests/
│   ├── FluentAzure.Tests/
│   │   ├── Core/
│   │   │   ├── FluentAzureTests.cs
│   │   │   ├── ConfigurationBuilderTests.cs
│   │   │   └── ConfigurationResultTests.cs
│   │   ├── Sources/
│   │   │   ├── EnvironmentSourceTests.cs
│   │   │   ├── KeyVaultSourceTests.cs
│   │   │   └── AppConfigurationSourceTests.cs
│   │   └── Integration/
│   │       ├── EndToEndTests.cs
│   │       └── PerformanceTests.cs
│   └── FluentAzure.Tests.csproj
├── examples/
│   ├── WebApi.Example/                    # ASP.NET Core Web API example
│   ├── FunctionApp.Example/               # Azure Functions example
│   ├── Console.Example/                   # Console app example
│   └── Worker.Example/                    # Background service example
├── docs/
│   ├── getting-started.md
│   ├── advanced-scenarios.md
│   ├── api-reference.md
│   └── migration-guide.md
├── .editorconfig                          # Code style for Cursor
├── .gitignore
├── Directory.Build.props                  # Shared MSBuild properties
├── azure-pipelines.yml                   # CI/CD pipeline
└── README.md
```

## 🛠️ Cursor IDE Setup Instructions

### 1. Initialize the Project
```bash
# Create solution
dotnet new sln -n FluentAzure

# Create main library
mkdir -p src/FluentAzure
cd src/FluentAzure
dotnet new classlib -f net8.0

# Create test project
mkdir -p ../../tests/FluentAzure.Tests
cd ../../tests/FluentAzure.Tests
dotnet new xunit -f net8.0

# Add projects to solution
cd ../..
dotnet sln add src/FluentAzure/FluentAzure.csproj
dotnet sln add tests/FluentAzure.Tests/FluentAzure.Tests.csproj

# Add test reference
dotnet add tests/FluentAzure.Tests reference src/FluentAzure
```

### 2. Package Dependencies
Add these to `src/FluentAzure/FluentAzure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageId>FluentAzure</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Fluent, type-safe Azure configuration and secrets management</Description>
    <PackageTags>azure;configuration;fluent;secrets;keyvault</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.3.2" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.6.0" />
    <PackageReference Include="Azure.Data.AppConfiguration" Version="1.4.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.4" />
  </ItemGroup>

</Project>
```

### 3. Cursor AI Prompts for Development

Use these prompts with Cursor's AI assistant:

#### **Initial Core Structure**
```
Create a Result<T> monad in C# with the following requirements:
- Support Success(T value) and Error(IEnumerable<string> errors)
- Include Map, Bind, and Match methods
- Make it immutable and thread-safe
- Add implicit operators for easy creation
- Include comprehensive XML documentation
```

#### **Configuration Pipeline Builder**
```
Create a fluent configuration pipeline builder that:
- Uses the builder pattern with method chaining
- Supports multiple configuration sources (environment, Key Vault, JSON)
- Returns Result<T> for all operations
- Accumulates errors instead of failing fast
- Supports async operations throughout
- Include validation and transformation steps
```

#### **Key Vault Integration**
```
Create an Azure Key Vault configuration source that:
- Uses DefaultAzureCredential for authentication
- Implements retry logic with exponential backoff
- Supports secret versioning
- Caches results to avoid repeated API calls
- Handles Key Vault access errors gracefully
- Maps Key Vault secret names to configuration keys
```

#### **Strongly-Typed Configuration**
```
Create a configuration binding system that:
- Converts flat configuration keys to nested objects
- Supports record types and init-only properties
- Validates required properties and data types
- Provides clear error messages for binding failures
- Supports collections and complex nested types
- Uses System.Text.Json for serialization
```

### 4. Development Workflow with Cursor

#### **Phase 1: Core Abstractions** (Week 1)
1. **Start with Result<T> monad**
   - Use Cursor AI to generate the basic structure
   - Add comprehensive unit tests
   - Refine based on functional programming best practices

2. **Configuration pipeline foundation**
   - Builder pattern implementation
   - Basic source abstraction
   - Error accumulation logic

#### **Phase 2: Configuration Sources** (Week 2)
1. **Environment variables source** (simplest)
2. **JSON file source** (local development)
3. **Key Vault source** (Azure integration)
4. **App Configuration source** (advanced scenarios)

#### **Phase 3: Advanced Features** (Week 3)
1. **Validation pipeline**
2. **Type transformations**
3. **Caching layer**
4. **Hot reload support**

#### **Phase 4: Integration & Polish** (Week 4)
1. **DI container extensions**
2. **ASP.NET Core integration**
3. **Azure Functions support**
4. **Documentation and examples**

### 5. Testing Strategy

#### **Unit Tests Structure**
```
tests/
├── Core/
│   ├── ResultTests.cs              # Test the Result<T> monad
│   ├── ConfigurationPipelineTests.cs # Test pipeline builder
│   └── ErrorAccumulationTests.cs   # Test error handling
├── Sources/
│   ├── EnvironmentSourceTests.cs   # Mock environment variables
│   ├── KeyVaultSourceTests.cs      # Mock Key Vault responses
│   └── JsonFileSourceTests.cs      # Test file parsing
└── Integration/
    ├── EndToEndTests.cs            # Full pipeline tests
    └── PerformanceTests.cs         # Load testing
```

#### **Cursor AI Prompts for Testing**
```
Generate comprehensive unit tests for a Result<T> monad that cover:
- Success and error scenarios
- Map and Bind operations
- Error accumulation
- Thread safety
- Edge cases like null values
Use FluentAssertions for readable assertions
```

### 6. Example Usage Patterns

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
- Cursor IDE with AI assistant enabled

### Quick Start
1. Clone this repository structure
2. Run the Cursor AI prompts in order
3. Start with the Result<T> monad and basic pipeline
4. Add configuration sources incrementally
5. Write tests for each component
6. Create example applications

### Deployment
- Package will be published to NuGet.org
- GitHub Actions for CI/CD
- Semantic versioning
- Comprehensive documentation

## 🎯 Success Metrics

- **Developer Experience**: Reduce configuration boilerplate by 70%
- **Type Safety**: Eliminate runtime configuration errors
- **Performance**: Cache Key Vault calls, < 100ms config load
- **Adoption**: Target 1000+ NuGet downloads in first month

## 📚 Resources

- [Functional Programming in C#](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-9#records)
- [Azure Key Vault Developer Guide](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Result Pattern in C#](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [Monads in C#](https://mikhail.io/2018/07/monads-explained-in-csharp-again/)

---

**Ready to revolutionize Azure configuration management? Let's build something awesome! 🚀**