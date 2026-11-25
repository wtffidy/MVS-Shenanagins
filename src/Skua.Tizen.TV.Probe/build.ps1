[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CertificateProfile,

    [string]$TizenCli = "tizen"
)

$ErrorActionPreference = "Stop"

$TizenCommand = Get-Command $TizenCli -ErrorAction SilentlyContinue
if ($null -eq $TizenCommand) {
    throw "Tizen CLI was not found. Install Tizen Studio with the TV Extension and Web CLI, then add the CLI directory to PATH."
}

$ProjectDirectory = $PSScriptRoot
$BuildDirectory = Join-Path $ProjectDirectory ".buildResult"
$ArtifactDirectory = Join-Path $ProjectDirectory "artifacts"

Push-Location $ProjectDirectory
try {
    Write-Host "[Tizen] Building web project..."
    & $TizenCommand.Source build-web -- $ProjectDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "tizen build-web failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $BuildDirectory -PathType Container)) {
        throw "Tizen build completed without creating .buildResult."
    }

    Write-Host "[Tizen] Packaging signed WGT..."
    & $TizenCommand.Source package -t wgt -s $CertificateProfile -- $BuildDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "tizen package failed with exit code $LASTEXITCODE."
    }

    $Package = Get-ChildItem -LiteralPath $BuildDirectory -Filter "*.wgt" -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $Package) {
        throw "Tizen packaging completed but no .wgt package was found under .buildResult."
    }

    New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
    $Destination = Join-Path $ArtifactDirectory "MVS-TV-Probe.wgt"
    Copy-Item -LiteralPath $Package.FullName -Destination $Destination -Force

    Write-Host "[Tizen] WGT created: $Destination"
}
finally {
    Pop-Location
}