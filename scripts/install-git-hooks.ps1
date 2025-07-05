#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Installs Git pre-commit hooks for FluentAzure project
.DESCRIPTION
    This script sets up Git hooks to automatically run code formatting and style checks before commits
#>

$ErrorActionPreference = "Stop"

# Get project root
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptPath
$GitHooksDir = Join-Path $ProjectRoot ".git/hooks"

Write-Host "🔧 Installing Git pre-commit hooks for FluentAzure..." -ForegroundColor Cyan

# Check if .git directory exists
if (-not (Test-Path ".git")) {
    Write-Host "❌ Error: Not in a Git repository. Run 'git init' first." -ForegroundColor Red
    exit 1
}

# Create hooks directory if it doesn't exist
if (-not (Test-Path $GitHooksDir)) {
    New-Item -ItemType Directory -Path $GitHooksDir -Force | Out-Null
}

# Create pre-commit hook content
$PreCommitHook = @"
#!/bin/sh
# FluentAzure pre-commit hook
# Runs code formatting and style checks before allowing commits

echo "🔍 Running FluentAzure pre-commit checks..."

# Determine which script to run based on OS
if command -v pwsh >/dev/null 2>&1; then
    # PowerShell is available
    pwsh -File ./scripts/format.ps1 -Check
elif command -v powershell >/dev/null 2>&1; then
    # Windows PowerShell is available
    powershell -File ./scripts/format.ps1 -Check
else
    # Use bash script
    ./scripts/format.sh --check
fi

exit_code=$?

if [ $exit_code -ne 0 ]; then
    echo ""
    echo "❌ Pre-commit checks failed!"
    echo "💡 To fix formatting issues, run:"
    echo "   Windows: .\scripts\format.ps1"
    echo "   Linux/Mac: ./scripts/format.sh"
    echo ""
    echo "To skip this check (not recommended), use: git commit --no-verify"
    exit 1
fi

echo "✅ Pre-commit checks passed!"
"@

# Write pre-commit hook
$PreCommitPath = Join-Path $GitHooksDir "pre-commit"
$PreCommitHook | Out-File -FilePath $PreCommitPath -Encoding ASCII

# Make the hook executable (important for Unix systems)
if ($IsLinux -or $IsMacOS) {
    chmod +x $PreCommitPath
}

Write-Host "✅ Pre-commit hook installed successfully!" -ForegroundColor Green
Write-Host "🎯 The hook will run automatically before each commit." -ForegroundColor Blue
Write-Host "💡 To bypass the hook (not recommended), use: git commit --no-verify" -ForegroundColor Yellow

# Test the hook
Write-Host "`n🧪 Testing the pre-commit hook..." -ForegroundColor Cyan
try {
    if ($IsWindows) {
        & pwsh -File "./scripts/format.ps1" -Check
    } else {
        & ./scripts/format.sh --check
    }
    Write-Host "✅ Pre-commit hook test passed!" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Pre-commit hook test failed. Please fix any issues before committing." -ForegroundColor Yellow
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} 