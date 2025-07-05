# Contributing to FluentAzure

Thank you for your interest in contributing to FluentAzure! This document outlines the development workflow, code standards, and tools we use to maintain high code quality.

## 🔧 Development Setup

### Prerequisites
- .NET 8.0 SDK
- Git
- PowerShell (Windows) or Bash (Linux/Mac)

### Initial Setup
1. Clone the repository
2. Install Git hooks for automatic code quality checks:
   ```powershell
   # Windows
   .\scripts\install-git-hooks.ps1
   ```
   ```bash
   # Linux/Mac
   chmod +x scripts/*.sh
   pwsh -File scripts/install-git-hooks.ps1  # Use PowerShell on Linux/Mac
   ```

## 📋 Code Quality Standards

We maintain high code quality through automated tools and standards:

### 🎯 Tools Used (All Free)

1. **EditorConfig** - Consistent formatting across editors
2. **dotnet format** - Built-in .NET code formatter
3. **StyleCop Analyzers** - C# style and consistency rules
4. **Microsoft.CodeAnalysis.NetAnalyzers** - Code quality analysis
5. **Roslyn Analyzers** - Built-in static analysis

### 📐 Code Style Rules

- **Indentation**: 4 spaces for C#, 2 spaces for XML/JSON
- **Line endings**: CRLF on Windows, LF on Unix
- **Naming**: PascalCase for public members, camelCase with underscore prefix for private fields
- **Documentation**: XML comments required for all public APIs
- **Nullable**: Enabled throughout the project

### 🔍 Code Analysis Levels

- **Errors**: Security, reliability, and critical performance issues
- **Warnings**: Style violations, maintainability issues
- **Suggestions**: Code improvements and modernization

## 🛠️ Development Workflow

### Before Making Changes

1. **Format your code**:
   ```powershell
   # Windows - Format and fix issues
   .\scripts\format.ps1
   
   # Windows - Check formatting only
   .\scripts\format.ps1 -Check
   ```
   ```bash
   # Linux/Mac - Format and fix issues
   ./scripts/format.sh
   
   # Linux/Mac - Check formatting only
   ./scripts/format.sh --check
   ```

2. **The script will**:
   - 📝 Format your code automatically
   - 🔨 Build the project
   - 🧪 Run all tests
   - 📊 Show code analysis results

### During Development

- Your editor should automatically apply formatting rules via EditorConfig
- Real-time analysis feedback from Roslyn analyzers
- IntelliSense shows documentation and suggestions

### Before Committing

- Pre-commit hooks automatically run formatting checks
- If checks fail, the commit is blocked
- Fix issues and try again, or use `git commit --no-verify` (not recommended)

## 📁 Project Structure

```
FluentAzure/
├── .editorconfig              # Editor formatting rules
├── Directory.Build.props      # Shared MSBuild properties
├── FluentAzure.ruleset       # Code analysis rules
├── stylecop.json             # StyleCop configuration
├── scripts/
│   ├── format.ps1            # PowerShell formatting script
│   ├── format.sh             # Bash formatting script
│   └── install-git-hooks.ps1 # Git hooks installer
├── src/FluentAzure/          # Main library
└── tests/FluentAzure.Tests/  # Test project
```

## 🔄 Configuration Files Explained

### `.editorconfig`
- Defines consistent coding styles
- Works with most editors (VS Code, Visual Studio, JetBrains, etc.)
- Covers indentation, line endings, spacing, and C#-specific rules

### `Directory.Build.props`
- Shared MSBuild properties for all projects
- Enables analyzers and code quality tools
- Sets up documentation generation
- Configures deterministic builds

### `FluentAzure.ruleset`
- Custom rule severity configuration
- Enables security, performance, and reliability rules
- Configures StyleCop rules for consistency

### `stylecop.json`
- StyleCop-specific configuration
- Defines documentation requirements
- Sets naming conventions and style preferences

## 🎨 Code Style Examples

### ✅ Good Examples

```csharp
// Good: Proper documentation, naming, and structure
namespace FluentAzure.Core;

/// <summary>
/// Represents a configuration value with validation.
/// </summary>
/// <typeparam name="T">The type of the configuration value</typeparam>
public sealed class ConfigurationValue<T>
{
    private readonly T _value;
    private readonly IReadOnlyList<string> _validationErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValue{T}"/> class.
    /// </summary>
    /// <param name="value">The configuration value</param>
    public ConfigurationValue(T value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
        _validationErrors = Array.Empty<string>();
    }

    /// <summary>
    /// Gets the configuration value.
    /// </summary>
    public T Value => _value;
}
```

### ❌ Bad Examples

```csharp
// Bad: Missing documentation, poor naming, inconsistent style
namespace fluentazure.core
{
    public class configvalue<t>
    {
        public t val;
        private List<string> errors;
        public configvalue(t Value) {
            val=Value;
        }
    }
}
```

## 🧪 Testing Standards

- **100% test coverage** for public APIs
- **Unit tests** for all Result<T> operations
- **Integration tests** for configuration pipelines
- **Thread safety tests** for concurrent scenarios
- **Performance tests** for critical paths

### Test Naming Convention
```csharp
[Fact]
public void MethodName_WhenCondition_ShouldExpectedBehavior()
{
    // Arrange
    var input = CreateTestInput();
    
    // Act
    var result = SystemUnderTest.MethodName(input);
    
    // Assert
    result.Should().BeExpectedValue();
}
```

## 🚀 Performance Guidelines

- Use `readonly struct` for immutable value types
- Prefer `ImmutableList<T>` over `List<T>` for immutable collections
- Use `ArgumentNullException.ThrowIfNull()` for parameter validation
- Implement `IEquatable<T>` for value types
- Use `StringComparison.Ordinal` for non-linguistic comparisons

## 🔐 Security Guidelines

- Never log sensitive configuration values
- Use `SecureString` for passwords when possible
- Validate all external inputs
- Follow principle of least privilege
- Enable all security analyzers

## 📝 Documentation Guidelines

- **XML comments** required for all public APIs
- **Examples** in documentation for complex scenarios
- **Parameter descriptions** must be meaningful
- **Exception documentation** for all thrown exceptions
- **README updates** for new features

## 🐛 Issue Reporting

When reporting issues:
1. Include the output of `dotnet --info`
2. Provide minimal reproduction steps
3. Include relevant configuration
4. Attach logs (with sensitive data removed)

## 🎯 Pull Request Guidelines

1. **Fork and branch** from `main`
2. **Run formatting** scripts before submitting
3. **Update tests** for new functionality
4. **Update documentation** for API changes
5. **Keep PRs focused** on a single feature/fix
6. **Write clear commit messages**

### Commit Message Format
```
type(scope): description

[optional body]

[optional footer]
```

Examples:
- `feat(core): add Result<T> monad implementation`
- `fix(config): handle null configuration values`
- `docs(readme): update installation instructions`
- `test(result): add thread safety tests`

## 🆘 Getting Help

- **Discussions**: Use GitHub Discussions for questions
- **Issues**: Use GitHub Issues for bug reports
- **Documentation**: Check the README and inline docs
- **Code Examples**: See the `examples/` directory

---

Thank you for contributing to FluentAzure! Your efforts help make Azure configuration management better for everyone. 🙏 
