param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'FrostUI.Designer\PlayerTemplates')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = Join-Path $repo 'player\FrostUI.Player\FrostUI.Player.csproj'
$work = Join-Path $env:TEMP ('frostui-templates-' + [Guid]::NewGuid().ToString('N'))

function Publish-Template([string]$name, [bool]$singleFile) {
    $out = Join-Path $work $name
    $singleValue = if ($singleFile) { 'true' } else { 'false' }
    $publishArgs = @(
        'publish', $player,
        '-c', 'Release',
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false',
        ('-p:PublishSingleFile=' + $singleValue)
    )
    if ($singleFile) {
        $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
    }
    $publishArgs += @('-o', $out, '--nologo')
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "publish failed: $name"
    }
    Remove-Item (Join-Path $out 'design.frostui.json') -Force -ErrorAction SilentlyContinue
    Get-ChildItem $out -Filter '*.log' | Remove-Item -Force -ErrorAction SilentlyContinue
    $target = Join-Path $OutputRoot $name
    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item (Join-Path $out '*') $target -Recurse -Force
    [pscustomobject]@{
        Variant = $name
        SingleFile = $singleFile
        Files = (Get-ChildItem $target -Recurse -File).Count
        Bytes = (Get-ChildItem $target -Recurse -File | Measure-Object Length -Sum).Sum
    } | ConvertTo-Json | Set-Content (Join-Path $target 'manifest.json') -Encoding UTF8
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
Publish-Template 'win-x64-single' $true
Publish-Template 'win-x64-folder' $false
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Player templates written to $OutputRoot"
