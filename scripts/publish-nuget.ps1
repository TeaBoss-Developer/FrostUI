# Publish FrostUI to nuget.org
# Usage:
#   $env:NUGET_API_KEY = "..."      # or pass -ApiKey
#   .\scripts\publish-nuget.ps1
param(
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Version = "1.0.0",
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

if (-not $ApiKey) {
    throw "Missing API key. Set `$env:NUGET_API_KEY or pass -ApiKey. Create one at https://www.nuget.org/account/apikeys (scope: Push new packages and package versions, glob: FrostUI)."
}

Write-Host "Packing FrostUI $Version..." -ForegroundColor Cyan
dotnet pack (Join-Path $repo "src/FrostUI/FrostUI.csproj") -c Release -o (Join-Path $repo "artifacts")
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed" }

$nupkg = Join-Path $repo "artifacts/FrostUI.$Version.nupkg"
$snupkg = Join-Path $repo "artifacts/FrostUI.$Version.snupkg"
if (-not (Test-Path $nupkg)) { throw "Package not found: $nupkg" }

Write-Host "Pushing $nupkg ..." -ForegroundColor Cyan
dotnet nuget push $nupkg --api-key $ApiKey --source $Source --skip-duplicate
if ($LASTEXITCODE -ne 0) { throw "dotnet nuget push failed" }

if (Test-Path $snupkg) {
    Write-Host "Pushing symbols $snupkg ..." -ForegroundColor Cyan
    dotnet nuget push $snupkg --api-key $ApiKey --source $Source --skip-duplicate
}

Write-Host "Done. https://www.nuget.org/packages/FrostUI/$Version" -ForegroundColor Green
