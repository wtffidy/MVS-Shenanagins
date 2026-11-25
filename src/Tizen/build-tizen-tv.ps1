[CmdletBinding()]
param(
    [string]$CertificateProfile = $env:TIZEN_CERT_PROFILE,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$TizenCommand = Get-Command "tizen" -ErrorAction SilentlyContinue
if ($null -ne $TizenCommand) {
    $TizenCli = $TizenCommand.Source
}
elseif (-not [string]::IsNullOrWhiteSpace($env:TIZEN_STUDIO)) {
    $TizenCli = Join-Path $env:TIZEN_STUDIO "tools\ide\bin\tizen.bat"
}
else {
    throw "Tizen CLI was not found. Add Tizen Studio's tools/ide/bin folder to PATH or set TIZEN_STUDIO."
}

if (-not (Test-Path $TizenCli)) {
    throw "Tizen CLI does not exist at '$TizenCli'."
}

if ([string]::IsNullOrWhiteSpace($CertificateProfile)) {
    throw "Set TIZEN_CERT_PROFILE or pass -CertificateProfile with a configured Tizen signing profile."
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$AssetPackProject = Join-Path $RepoRoot "src\tools\RuffleAssetPack\RuffleAssetPack.csproj"
$SharedWebRoot = Join-Path $PSScriptRoot "Shared"
$ProfileConfig = Join-Path $PSScriptRoot "TV\config.xml"
$BuildRoot = Join-Path $PSScriptRoot ".build\TV"
$WebRoot = Join-Path $BuildRoot "www"
$ArtifactRoot = Join-Path $PSScriptRoot "artifacts"

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK was not found."
}

if (-not (Test-Path $AssetPackProject)) {
    throw "Ruffle asset pack project was not found: $AssetPackProject"
}

if (-not (Test-Path $ProfileConfig)) {
    throw "Samsung TV config.xml was not found: $ProfileConfig"
}

if (Test-Path $BuildRoot) {
    Remove-Item $BuildRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $WebRoot -Force | Out-Null
New-Item -ItemType Directory -Path $ArtifactRoot -Force | Out-Null
Copy-Item (Join-Path $SharedWebRoot "*") $WebRoot -Recurse -Force
Copy-Item $ProfileConfig (Join-Path $WebRoot "config.xml") -Force

$IconPath = Join-Path $RepoRoot "src\tools\icon\monogram.png"
if (-not (Test-Path $IconPath)) {
    throw "MVS icon was not found: $IconPath"
}
Copy-Item $IconPath (Join-Path $WebRoot "icon.png") -Force

& dotnet run --project $AssetPackProject --configuration $Configuration -- (Join-Path $WebRoot "engine")
if ($LASTEXITCODE -ne 0) {
    throw "Ruffle asset export failed with exit code $LASTEXITCODE."
}

Push-Location $WebRoot
try {
    & $TizenCli build-web -- $WebRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Tizen web build failed with exit code $LASTEXITCODE."
    }

    $BuiltDirectory = Join-Path $WebRoot ".buildResult"
    if (-not (Test-Path $BuiltDirectory)) {
        throw "Tizen CLI did not produce the expected build directory: $BuiltDirectory"
    }

    & $TizenCli package -t wgt -s $CertificateProfile -- $BuiltDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Tizen WGT packaging failed with exit code $LASTEXITCODE."
    }

    $Package = Get-ChildItem -Path $WebRoot -Filter "*.wgt" -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $Package) {
        throw "Tizen CLI completed but no .wgt package was found."
    }

    $Destination = Join-Path $ArtifactRoot "MVS-Tizen-TV.wgt"
    Copy-Item $Package.FullName $Destination -Force
    Write-Host "Tizen package created: $Destination" -ForegroundColor Green
}
finally {
    Pop-Location
}
