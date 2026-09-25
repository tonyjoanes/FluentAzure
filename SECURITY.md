# Security Policy

## Overview

FluentAzure takes security seriously and implements multiple layers of protection for handling sensitive configuration data, especially Azure Key Vault secrets.

## Security Features

### 1. Secret Handling

- **No values in errors**: Binding and conversion errors name the key and target type, never the configured value (parser messages that echo input are not passed through)
- **Sensitive key tracking**: Values from Key Vault and resolved Key Vault references are tracked as sensitive; add others with `.Sensitive("Key")`
- **Redacted debug view**: `configuration.GetRedactedDebugView()` masks sensitive values; the standard `GetDebugView()` does not
- **Disposal**: `KeyVaultSource` and `KeyVaultSecretCache` drop their references to secret values on disposal. .NET strings are immutable, so this does not scrub memory; use OS-level protections if your threat model requires it

### 2. Async Best Practices

- **ConfigureAwait(false)**: All async operations use `ConfigureAwait(false)` to prevent deadlocks
- **Proper Exception Handling**: Comprehensive error handling without exposing sensitive information

### 3. Retry and Resilience

- **Polly Integration**: Uses Polly for retry policies with exponential backoff
- **Secure Failure Modes**: Fails securely without exposing sensitive data in error messages

### 4. Code Analysis

- **Static Analysis**: Comprehensive static code analysis rules enabled
- **Security Rules**: Specific security-focused analyzers activated
- **Continuous Monitoring**: CI/CD pipeline includes security scanning

## Security Considerations

### For Developers

1. **Never log secret values** - The library is designed to prevent accidental logging of secrets
2. **Use secure disposal** - Always dispose of configuration sources when done
3. **ConfigureAwait usage** - The library uses `ConfigureAwait(false)` internally for better performance

### For Production Use

1. **Managed Identity**: Call `.UseManagedIdentity()` (or `.UseWorkloadIdentity()` on AKS) so every Azure source uses a deterministic identity. If you keep `DefaultAzureCredential`, set `AZURE_TOKEN_CREDENTIALS=prod` to exclude developer credentials from its chain
2. **Network Security**: Configure Key Vault network access rules appropriately
3. **Audit Logging**: Enable Key Vault audit logging for compliance
4. **Secret Rotation**: Implement regular secret rotation policies

## Reporting Security Issues

If you discover a security vulnerability, please report it to the maintainers privately:

1. **Do not** create a public GitHub issue
2. Contact the maintainers directly via email
3. Provide detailed reproduction steps if possible

## Security Best Practices

### Configuration

```csharp
// ✅ Good - Secure configuration
var config = await FluentConfig
    .Create()
    .FromEnvironment()
    .FromKeyVault("https://myvault.vault.azure.net")
    .Required("DatabaseConnectionString")
    .BuildAsync();

// Use configuration
var connectionString = config.Match(
    success => success["DatabaseConnectionString"],
    errors => throw new SecurityException("Configuration failed")
);
```

### Disposal

```csharp
// ✅ Good - Proper disposal
using var keyVaultSource = new KeyVaultSource(vaultUrl);
var result = await keyVaultSource.LoadAsync();
// Disposal drops references to secret values
```

## Compliance

- **SOC 2**: Security controls align with SOC 2 Type II requirements
- **PCI DSS**: Suitable for PCI DSS environments when properly configured
- **GDPR**: Implements data minimization and secure deletion principles

---

**Last Updated**: 2024-12-19
**Security Review**: Complete