# Versioning and Publishing Guide

This document explains how the automated versioning, packaging, and publishing system works for FluentAzure.

## Overview

The project uses [MinVer](https://github.com/adamralph/minver) for automatic versioning based on Git tags, following Semantic Versioning (SemVer) best practices. The GitHub Actions pipeline handles building, testing, packaging, and publishing to NuGet.org automatically.

## Versioning Strategy

### How MinVer Works

MinVer automatically determines the version number from Git tags:

- **Stable releases**: Tag with `v1.0.0`, `v2.1.3`, etc.
- **Pre-releases**: Tag with `v1.0.0-alpha.1`, `v2.1.0-beta.2`, etc.
- **Build metadata**: Automatically added for commits after the latest tag

### Version Format

The version follows SemVer format: `MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`

Examples:
- `1.0.0` - Stable release
- `1.0.0-alpha.1` - Alpha pre-release
- `1.0.0-beta.2+ci.20231201.123456` - Beta with build metadata

## Publishing Workflow

### Prerequisites

1. **NuGet API Key**: Add `NUGET_API_KEY` to your GitHub repository secrets
   - Go to [NuGet.org](https://www.nuget.org/account/apikeys)
   - Create a new API key
   - Add it to your repository: Settings → Secrets and variables → Actions → New repository secret

2. **Codecov Token** (optional): Add `CODECOV_TOKEN` for coverage reporting

### Release Process

#### 1. Create a Release Tag

```bash
# For a stable release
git tag v1.0.0
git push origin v1.0.0

# For a pre-release
git tag v1.0.0-alpha.1
git push origin v1.0.0-alpha.1
```

#### 2. Automated Pipeline

When you push a tag starting with `v`, the GitHub Actions pipeline will:

1. **Build and Test**: Run on multiple platforms (Ubuntu, Windows, macOS)
2. **Security Scan**: Check for vulnerable dependencies
3. **Code Quality**: Verify formatting and run static analysis
4. **Package**: Create NuGet packages with the correct version
5. **Publish**: Automatically publish to NuGet.org (only for version tags)
6. **Release**: Create a GitHub release (optional)

### Manual Release Steps

If you prefer to create releases manually:

1. **Create and push a tag**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Monitor the pipeline**:
   - Go to Actions tab in GitHub
   - Watch the "CI/CD Pipeline" workflow
   - Ensure all jobs pass

3. **Verify the release**:
   - Check NuGet.org for the new package
   - Verify the version number is correct
   - Test the package in a new project

## Configuration Files

### Directory.Build.props

The main configuration file contains:

```xml
<!-- MinVer configuration -->
<MinVerTagPrefix>v</MinVerTagPrefix>
<MinVerDefaultPreReleasePhase>preview</MinVerDefaultPreReleasePhase>

<!-- Version properties -->
<AssemblyVersion>$(MinVerMajor).$(MinVerMinor).0.0</AssemblyVersion>
<FileVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).$(MinVerBuildMetadata)</FileVersion>
<InformationalVersion>$(MinVerVersion)</InformationalVersion>
<Version>$(MinVerVersion)</Version>
<PackageVersion>$(MinVerVersion)</PackageVersion>
```

### GitHub Actions Workflow (.github/workflows/ci.yml)

The workflow includes:

- **Multi-platform testing**: Ubuntu, Windows, macOS
- **Security scanning**: Vulnerability checks
- **Code quality**: Formatting and analysis
- **Automated packaging**: Creates NuGet packages
- **Conditional publishing**: Only publishes on version tags
- **Artifact uploads**: Packages available for download

## Best Practices

### Tagging Strategy

1. **Use semantic versioning**: `vMAJOR.MINOR.PATCH`
2. **Pre-releases**: Use `-alpha`, `-beta`, `-rc` suffixes
3. **Annotated tags**: Use `git tag -a v1.0.0 -m "Release 1.0.0"`
4. **Consistent naming**: Always use `v` prefix

### Release Process

1. **Feature development**: Work on `main` branch
2. **Testing**: Ensure all tests pass locally
3. **Tag creation**: Create and push version tag
4. **Verification**: Monitor pipeline and verify release
5. **Documentation**: Update release notes if needed

### Troubleshooting

#### Common Issues

1. **Version not updating**: Ensure you're using the latest tag
2. **Build failures**: Check the Actions tab for error details
3. **Publishing fails**: Verify `NUGET_API_KEY` secret is set
4. **Package not found**: Wait for NuGet.org indexing (can take 5-10 minutes)

#### Debugging

- Check the "Display version information" step in the workflow
- Verify Git tags are pushed correctly: `git tag -l`
- Ensure full Git history is available: `fetch-depth: 0`

## Migration from Manual Scripts

The old PowerShell scripts (`version.ps1`, `publish.ps1`) have been removed in favor of the automated GitHub Actions pipeline. This provides:

- **Better reliability**: No local environment dependencies
- **Consistent builds**: Same environment every time
- **Automated testing**: Multi-platform validation
- **Security**: Built-in vulnerability scanning
- **Transparency**: All steps visible in GitHub Actions

## Local Development

For local development and testing:

```bash
# Build with current version
dotnet build --configuration Release

# Create package locally
dotnet pack --configuration Release --output ./packages

# Check version information
dotnet build --configuration Release /p:MinVerVerbosity=detailed
```

The version will be determined from your local Git tags and commits.
