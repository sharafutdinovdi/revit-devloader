[CmdletBinding()]
param(
    [ValidateSet('2022', '2023', '2024', '2025', '2026')]
    [string[]]$Versions = @('2022', '2023', '2024', '2025', '2026'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipRestore,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testsProject = Join-Path $root 'tests\RevitDevLoader.Core.Tests'

# Revit-independent unit tests run once, not per Revit version.
if (-not $SkipTests) {
    Push-Location $root
    try {
        Write-Host "Running DevLoader unit tests" -ForegroundColor Yellow
        & dotnet test $testsProject -c $Configuration --nologo -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "unit tests failed" }
    }
    finally {
        Pop-Location
    }
}

foreach ($version in $Versions) {
    $kind = if ([int]$version -le 2024) { 'Legacy' } else { 'Modern' }
    $projectName = "RevitDevLoader.Addin.$kind"
    $project = Join-Path $root "src\$projectName\$projectName.csproj"
    $projectBin = Join-Path $root "src\$projectName\bin"
    $addinManifest = Join-Path $root "src\$projectName\$projectName.addin"
    $short = 'R' + $version.Substring(2)
    # Name differs from the $Configuration parameter by more than case: PowerShell
    # is case-insensitive for variable names, so a same-name variable would clobber it.
    $buildConfig = "$Configuration.$short"
    $outDir = Join-Path $root "build\release\$version"

    Write-Host "Building DevLoader for Revit $version ($buildConfig)" -ForegroundColor Yellow

    if (Test-Path $outDir) {
        Remove-Item $outDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    # dotnet is invoked from the solution directory on purpose: global.json is
    # resolved from the current directory.
    Push-Location $root
    try {
        if (-not $SkipRestore) {
            & dotnet restore $project -p:Configuration=$buildConfig --nologo
            if ($LASTEXITCODE -ne 0) { throw "restore failed for $buildConfig" }
        }

        # DeployAddin is disabled here: this script produces a release payload,
        # it must not touch the local Revit Addins folder.
        & dotnet build $project -c $buildConfig -p:DeployAddin=false --no-restore --nologo -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "build failed for $buildConfig" }
    }
    finally {
        Pop-Location
    }

    $buildOutput = Join-Path $projectBin $buildConfig
    if (-not (Test-Path $buildOutput)) {
        throw "Build output not found: $buildOutput"
    }

    Copy-Item (Join-Path $buildOutput '*') $outDir -Recurse -Force
    # DeployAddin stays disabled so builds never write to the local Revit Addins
    # folder; copy the manifest explicitly into the release payload instead.
    Copy-Item $addinManifest (Join-Path $outDir 'RevitDevLoader.addin') -Force
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $outDir -Force
    Write-Host "  -> $outDir" -ForegroundColor Green
}

Write-Host "DevLoader build matrix completed." -ForegroundColor Green
