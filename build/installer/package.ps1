param(
    [switch]$SkipBuild,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$pluginName = "RevitDevLoader"
$root = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $root "release\package-staging\$pluginName"
$outputDir = Join-Path $root "release\packages"
if (-not $SkipBuild) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "build-all.ps1") -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "$pluginName build failed with exit code $LASTEXITCODE"
    }
}

# Versions come from what the build actually produced, not from a literal list.
$releaseRoot = Join-Path $root "release"
$versions = @(Get-ChildItem -Path $releaseRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "^20\d{2}$" } |
    Select-Object -ExpandProperty Name |
    Sort-Object)

if ($versions.Count -eq 0) {
    throw "No build output found under $releaseRoot. Run build-all.ps1 first."
}

$zipPath = Join-Path $outputDir "$pluginName-revit-$($versions[0])-$($versions[-1]).zip"

function Assert-ChildPath {
    param(
        [string]$Parent,
        [string]$Child
    )

    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedChild = [System.IO.Path]::GetFullPath($Child)
    if (-not $resolvedChild.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete outside package root: $resolvedChild"
    }
}

foreach ($version in $versions) {
    $manifestPath = Join-Path $releaseRoot "$version\RevitDevLoader.addin"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing DevLoader manifest for Revit $version. Expected: $manifestPath"
    }
}

Assert-ChildPath -Parent (Join-Path $root "release") -Child $packageRoot
if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$repoRoot = Split-Path -Parent $root
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") -Destination $packageRoot -Force

Copy-Item -Path (Join-Path $PSScriptRoot "README.md") -Destination (Join-Path $packageRoot "README.md") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "install.ps1") -Destination (Join-Path $packageRoot "install.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "uninstall.ps1") -Destination (Join-Path $packageRoot "uninstall.ps1") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "install-all.cmd") -Destination (Join-Path $packageRoot "install-all.cmd") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "uninstall-all.cmd") -Destination (Join-Path $packageRoot "uninstall-all.cmd") -Force

foreach ($version in $versions) {
    $source = Join-Path $root "release\$version"
    if (-not (Test-Path $source)) {
        throw "Missing build output: $source"
    }

    $destination = Join-Path $packageRoot $version
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -Path $source -File |
        Where-Object { $_.Extension -ne ".pdb" } |
        Copy-Item -Destination $destination -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force
Write-Host "Package created: $zipPath" -ForegroundColor Green
