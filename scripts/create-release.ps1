#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a new release tag for FluentAzure

.DESCRIPTION
    This script helps create Git tags for releases. It validates the version format
    and provides guidance for the release process.

.PARAMETER Version
    The version to release (e.g., "1.0.0", "2.1.0-alpha.1")

.PARAMETER Message
    Optional release message (defaults to "Release {version}")

.PARAMETER Push
    Automatically push the tag to remote (default: false)

.EXAMPLE
    .\create-release.ps1 -Version "1.0.0"
    Creates tag v1.0.0 with message "Release 1.0.0"

.EXAMPLE
    .\create-release.ps1 -Version "2.1.0-alpha.1" -Message "Alpha release with new features" -Push
    Creates and pushes tag v2.1.0-alpha.1
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Message,

    [Parameter(Mandatory = $false)]
    [switch]$Push
)

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$') {
    Write-Host "❌ Invalid version format. Use format: MAJOR.MINOR.PATCH[-PRERELEASE]" -ForegroundColor Red
    Write-Host "Examples: 1.0.0, 2.1.0-alpha.1, 3.0.0-beta.2" -ForegroundColor Yellow
    exit 1
}

# Set default message if not provided
if (-not $Message) {
    $Message = "Release $Version"
}

# Create tag name
$TagName = "v$Version"

Write-Host "Creating release tag: $TagName" -ForegroundColor Cyan
Write-Host "Message: $Message" -ForegroundColor Cyan

# Check if tag already exists
$ExistingTag = git tag -l $TagName
if ($ExistingTag) {
    Write-Host "❌ Tag $TagName already exists!" -ForegroundColor Red
    exit 1
}

# Check if working directory is clean
$Status = git status --porcelain
if ($Status) {
    Write-Host "❌ Working directory is not clean. Please commit or stash changes first." -ForegroundColor Red
    Write-Host "Uncommitted changes:" -ForegroundColor Yellow
    Write-Host $Status
    exit 1
}

# Create annotated tag
try {
    git tag -a $TagName -m $Message
    Write-Host "✅ Tag $TagName created successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create tag: $_" -ForegroundColor Red
    exit 1
}

# Push tag if requested
if ($Push) {
    Write-Host "Pushing tag to remote..." -ForegroundColor Yellow
    try {
        git push origin $TagName
        Write-Host "✅ Tag pushed successfully" -ForegroundColor Green
        Write-Host "🚀 GitHub Actions will automatically build and publish the package" -ForegroundColor Cyan
    } catch {
        Write-Host "❌ Failed to push tag: $_" -ForegroundColor Red
        Write-Host "You can push manually with: git push origin $TagName" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "To push the tag and trigger the release pipeline:" -ForegroundColor Yellow
    Write-Host "  git push origin $TagName" -ForegroundColor White
}

Write-Host "`n📋 Next steps:" -ForegroundColor Cyan
Write-Host "1. Push the tag: git push origin $TagName" -ForegroundColor White
Write-Host "2. Monitor the GitHub Actions pipeline" -ForegroundColor White
Write-Host "3. Verify the package on NuGet.org (may take 5-10 minutes)" -ForegroundColor White
