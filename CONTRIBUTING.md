# Contributing to FluentAzure

Thank you for your interest in contributing to FluentAzure! This guide will help you get started with development.

## 🚀 Development Setup

### Prerequisites
- .NET 8.0 SDK
- Azure subscription (for Key Vault/App Configuration testing)
- Git

### Quick Start
1. Clone this repository: `git clone https://github.com/yourusername/FluentAzure.git`
2. Navigate to the project: `cd FluentAzure`
3. Restore dependencies: `dotnet restore`
4. Build the project: `dotnet build`
5. Run tests: `dotnet test`

## 🧪 Testing Strategy

### Unit Tests Structure
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

### Running Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/FluentAzure.Tests/
```

## 📋 Code Style and Standards

### C# Coding Standards
- Follow Microsoft C# coding conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and under 20 lines when possible

### Code Quality
- All code must pass StyleCop analysis
- Maintain 90%+ code coverage
- Use nullable reference types
- Prefer functional programming patterns

## 🔄 Pull Request Process

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-feature`
3. **Make** your changes following the coding standards
4. **Test** your changes thoroughly
5. **Commit** with descriptive messages: `git commit -m "Add amazing feature"`
6. **Push** to your fork: `git push origin feature/amazing-feature`
7. **Create** a Pull Request with detailed description

### PR Requirements
- [ ] Code follows style guidelines
- [ ] Tests pass and coverage is maintained
- [ ] Documentation is updated
- [ ] No breaking changes (or clearly documented)

## 🎯 Success Metrics

Our development goals include:
- **Developer Experience**: Reduce configuration boilerplate by 70%
- **Type Safety**: Eliminate runtime configuration errors
- **Performance**: Cache Key Vault calls, < 100ms config load
- **Adoption**: Target 1000+ NuGet downloads in first month

## 📚 Resources

- [Functional Programming in C#](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-9#records)
- [Azure Key Vault Developer Guide](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Result Pattern in C#](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [Monads in C#](https://mikhail.io/2018/07/monads-explained-in-csharp-again/)

## 📞 Getting Help

- Open an issue for bugs or feature requests
- Join our discussions for design questions
- Check existing issues before creating new ones

---

Thank you for contributing to FluentAzure! 🚀
