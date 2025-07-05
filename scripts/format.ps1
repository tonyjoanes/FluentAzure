#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Formats code and runs style checks for FluentAzure project
.DESCRIPTION
    This script runs dotnet format, builds the project, and runs tests to ensure code quality
.PARAMETER Check
    Run in check mode (verify formatting without making changes)
.PARAMETER Fix
    Automatically fix formatting issues
.PARAMETER Severity
    Minimum severity level for diagnostics (info, warn, error)
.EXAMPLE
    .\scripts\format.ps1 -Fix
    .\scripts\format.ps1 -Check
#>

param(
    [switch]$Check,
    [ValidateSet("info", "warn", "error")]
    [string]$Severity = "warn"
)

$ErrorActionPreference = "Stop"

# Change to project root directory
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptPath
Set-Location $ProjectRoot

Write-Host "FluentAzure Code Formatting and Style Check" -ForegroundColor Cyan
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Gray

# Step 1: Format code
Write-Host "`nStep 1: Formatting code..." -ForegroundColor Yellow

if ($Check) {
    Write-Host "Running in check mode (no changes will be made)..." -ForegroundColor Blue
    & dotnet format --verify-no-changes --severity $Severity --verbosity diagnostic
} else {
    Write-Host "Formatting code..." -ForegroundColor Blue
    & dotnet format --severity $Severity --verbosity diagnostic
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Code formatting failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Code formatting completed successfully!" -ForegroundColor Green

# Step 2: Build the project
Write-Host "`nStep 2: Building project..." -ForegroundColor Yellow

& dotnet build --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build completed successfully!" -ForegroundColor Green

# Step 3: Run tests
Write-Host "`nStep 3: Running tests..." -ForegroundColor Yellow

& dotnet test --configuration Release --no-build --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed!" -ForegroundColor Red
    exit 1
}
Write-Host "All tests passed!" -ForegroundColor Green

Write-Host "`nCode formatting and style check completed successfully!" -ForegroundColor Green
Write-Host "Project is ready for commit!" -ForegroundColor Cyan
