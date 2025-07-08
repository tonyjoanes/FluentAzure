# Automated Versioning and Publishing Setup

This document summarizes the complete automated versioning, packaging, and publishing setup for FluentAzure using GitHub Actions and MinVer.

## âœ… What's Been Implemented

### 1. Removed Local Scripts
- âŒ `scripts/publish.ps1` - Removed (replaced by GitHub Actions)
- âŒ `scripts/version.ps1` - Removed (replaced by MinVer)

### 2. Updated Configuration Files

#### Directory.Build.props
- âœ… Added MinVer package reference
- âœ… Configured automatic versioning from Git tags
- âœ… Set up SemVer-compliant version properties

#### GitHub Actions Workflow (.github/workflows/ci.yml)
- âœ… Multi-platform build and test (Ubuntu, Windows, macOS)
- âœ… Security vulnerability scanning
- âœ… Code quality checks (formatting, static analysis)
- âœ… Automated packaging with MinVer versioning
- âœ… Conditional publishing to NuGet.org on version tags
- âœ… GitHub release creation
- âœ… Comprehensive comments explaining each step

### 3. Added Helper Scripts
- âœ… `scripts/create-release.ps1` - Simplified release tag creation
- âœ… `docs/versioning-and-publishing.md` - Complete documentation

## ðŸš€ How It Works

### Versioning with MinVer
1. **Git Tags**: Create tags like `v1.0.0`, `v2.1.0-alpha.1`
2. **Automatic Detection**: MinVer reads Git history to determine version
3. **SemVer Compliance**: Follows `MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]` format

### Publishing Pipeline
1. **Tag Push**: Push a version tag (e.g., `v1.0.0`)
2. **Automated Build**: GitHub Actions builds on multiple platforms
3. **Quality Gates**: Security scan, code quality checks
4. **Packaging**: Creates NuGet package with correct version
5. **Publishing**: Automatically publishes to NuGet.org
6. **Release**: Creates GitHub release with notes

## ðŸ“‹ Setup Requirements

### GitHub Repository Secrets
Add these secrets to your repository (Settings â†’ Secrets and variables â†’ Actions):

1. **NUGET_API_KEY** (Required)
   - Get from: https://www.nuget.org/account/apikeys
   - Used for publishing packages to NuGet.org

2. **CODECOV_TOKEN** (Optional)
   - Used for coverage reporting
   - Can be added later if needed

## ðŸŽ¯ Usage Examples

### Creating a Stable Release
```bash
# Using the helper script
.\scripts\create-release.ps1 -Version "1.0.0" -Push

# Or manually
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

### Creating a Pre-release
```bash
# Alpha release
.\scripts\create-release.ps1 -Version "1.0.0-alpha.1" -Push

# Beta release
.\scripts\create-release.ps1 -Version "1.0.0-beta.2" -Push
```

### Local Development
```bash
# Build with current version
dotnet build --configuration Release

# Create package locally
dotnet pack --configuration Release --output ./packages
```

## ðŸ”§ Configuration Details

### MinVer Settings
```xml
<MinVerTagPrefix>v</MinVerTagPrefix>
<MinVerDefaultPreReleasePhase>preview</MinVerDefaultPreReleasePhase>
```

### Version Properties
```xml
<AssemblyVersion>$(MinVerMajor).$(MinVerMinor).0.0</AssemblyVersion>
<FileVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).$(MinVerBuildMetadata)</FileVersion>
<InformationalVersion>$(MinVerVersion)</InformationalVersion>
<Version>$(MinVerVersion)</Version>
<PackageVersion>$(MinVerVersion)</PackageVersion>
```

## ðŸ“Š Workflow Jobs

1. **build-and-test**: Multi-platform testing with coverage
2. **security-scan**: Vulnerability checking
3. **code-quality**: Formatting and static analysis
4. **package**: Creates NuGet packages (runs on main branch and tags)
5. **publish**: Publishes to NuGet.org (runs only on version tags)

## ðŸ›¡ï¸ Security Features

- **Vulnerability Scanning**: Checks for vulnerable dependencies
- **Code Quality**: Static analysis and formatting checks
- **Multi-platform Testing**: Ensures compatibility
- **Deterministic Builds**: Reproducible builds in CI

## ðŸ“ˆ Benefits

### Developer Experience
- âœ… No manual version management
- âœ… Automated testing on multiple platforms
- âœ… Consistent build environment
- âœ… Simplified release process

### Reliability
- âœ… Automated quality gates
- âœ… Security scanning
- âœ… Multi-platform validation
- âœ… Deterministic builds

### Transparency
- âœ… All steps visible in GitHub Actions
- âœ… Detailed logging and artifacts
- âœ… Clear success/failure indicators

## ðŸ” Monitoring

### GitHub Actions
- Go to Actions tab to monitor pipeline
- Check job logs for detailed information
- Download artifacts for inspection

### NuGet.org
- Packages appear within 5-10 minutes after successful publish
- Search for "FluentAzure" to find your package

## ðŸš¨ Troubleshooting

### Common Issues
1. **Version not updating**: Ensure you're using the latest tag
2. **Build failures**: Check Actions tab for error details
3. **Publishing fails**: Verify `NUGET_API_KEY` secret is set
4. **Package not found**: Wait for NuGet.org indexing

### Debugging
- Check "Display version information" step in workflow
- Verify Git tags: `git tag -l`
- Ensure full history: `fetch-depth: 0` in workflow

## ðŸ“š Additional Resources

- [MinVer Documentation](https://github.com/adamralph/minver)
- [Semantic Versioning](https://semver.org/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [NuGet Publishing Guide](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)

---

**Next Steps**: Set up your `NUGET_API_KEY` secret and create your first release tag to test the pipeline! 
