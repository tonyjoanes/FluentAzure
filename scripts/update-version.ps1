param(
    [Parameter(Mandatory=$true)]
    [int]$Major,

    [Parameter(Mandatory=$true)]
    [int]$Minor,

    [Parameter(Mandatory=$true)]
    [int]$Patch,

    [Parameter(Mandatory=$false)]
    [string]$PreRelease
)

# Path to the Version.cs file
$versionFile = "src\FluentAzure\Version.cs"

# Read the current content
$content = Get-Content $versionFile -Raw

# Update the version constants
$content = $content -replace 'public const int Major = \d+;', "public const int Major = $Major;"
$content = $content -replace 'public const int Minor = \d+;', "public const int Minor = $Minor;"
$content = $content -replace 'public const int Patch = \d+;', "public const int Patch = $Patch;"

if ($PreRelease) {
    $content = $content -replace 'public const string\? PreRelease = "[^"]*";', "public const string? PreRelease = `"$PreRelease`";"
} else {
    $content = $content -replace 'public const string\? PreRelease = "[^"]*";', "public const string? PreRelease = null;"
}

# Write the updated content back
Set-Content $versionFile $content -NoNewline

# Update Directory.Build.props
$buildPropsFile = "Directory.Build.props"
$buildPropsContent = Get-Content $buildPropsFile -Raw

$assemblyVersion = "$Major.$Minor.0.0"
$fileVersion = "$Major.$Minor.$Patch.0"
$fullVersion = if ($PreRelease) { "$Major.$Minor.$Patch-$PreRelease" } else { "$Major.$Minor.$Patch" }

$buildPropsContent = $buildPropsContent -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
$buildPropsContent = $buildPropsContent -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$fileVersion</FileVersion>"
$buildPropsContent = $buildPropsContent -replace '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$fullVersion</InformationalVersion>"
$buildPropsContent = $buildPropsContent -replace '<Version>[^<]+</Version>', "<Version>$fullVersion</Version>"
$buildPropsContent = $buildPropsContent -replace '<PackageVersion>[^<]+</PackageVersion>', "<PackageVersion>$fullVersion</PackageVersion>"

Set-Content $buildPropsFile $buildPropsContent -NoNewline

Write-Host "✅ Version updated to $fullVersion" -ForegroundColor Green
Write-Host "📝 Updated files:" -ForegroundColor Yellow
Write-Host "   - $versionFile" -ForegroundColor Cyan
Write-Host "   - $buildPropsFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "🔄 Next steps:" -ForegroundColor Yellow
Write-Host "   1. Review the changes" -ForegroundColor White
Write-Host "   2. Commit the version update" -ForegroundColor White
Write-Host "   3. Create a new Git tag: git tag v$fullVersion" -ForegroundColor White
Write-Host "   4. Push the tag: git push origin v$fullVersion" -ForegroundColor White
