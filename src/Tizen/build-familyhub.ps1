[CmdletBinding()]
param()

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

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$ProjectRoot = Join-Path $PSScriptRoot "FamilyHub"
$IconSource = Join-Path $RepoRoot "src\tools\icon\monogram.png"
$IconDirectory = Join-Path $ProjectRoot "shared\res"
$ArtifactDirectory = Join-Path $PSScriptRoot "artifacts"

if (-not (Test-Path $IconSource)) {
    throw "MVS application icon was not found: $IconSource"
}

if (-not (Test-Path (Join-Path $ProjectRoot "Mvs.FamilyHub.csproj"))) {
    throw "Family Hub project file was not found."
}

New-Item -ItemType Directory -Path $IconDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
Copy-Item $IconSource (Join-Path $IconDirectory "MvsFamilyHub.png") -Force

Push-Location $ProjectRoot
try {
    & $TizenCli build-cs
    if ($LASTEXITCODE -ne 0) {
        throw "Tizen .NET build failed with exit code $LASTEXITCODE."
    }

    $Package = Get-ChildItem -Path (Join-Path $ProjectRoot "bin") -Filter "*.tpk" -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $Package) {
        throw "Tizen CLI completed but no .tpk package was found under '$ProjectRoot\bin'."
    }

    $Destination = Join-Path $ArtifactDirectory "MVS-FamilyHub.tpk"
    Copy-Item $Package.FullName $Destination -Force
    Write-Host "Family Hub package created: $Destination" -ForegroundColor Green
}
finally {
    Pop-Location
}
