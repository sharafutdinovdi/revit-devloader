param(
    [ValidatePattern("^20\d{2}$")]
    [string]$RevitVersion = "",
    [switch]$AllVersions,
    [string]$AddinsRoot = (Join-Path $env:APPDATA "Autodesk\Revit\Addins")
)

$ErrorActionPreference = "Stop"

$pluginName = "RevitDevLoader"
# Uninstall works against whatever is actually installed under AddinsRoot.
$installedVersions = @(Get-ChildItem -Path $AddinsRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "^20\d{2}$" -and (Test-Path (Join-Path $_.FullName "$pluginName.addin")) } |
    Select-Object -ExpandProperty Name |
    Sort-Object)

if ($AllVersions) {
    $versions = $installedVersions
}
elseif ($RevitVersion) {
    $versions = @($RevitVersion)
}
else {
    $versions = $installedVersions
}

if ($versions.Count -eq 0) {
    Write-Host "$pluginName is not installed for any Revit version." -ForegroundColor Yellow
    return
}

function Assert-ChildPath {
    param(
        [string]$Parent,
        [string]$Child
    )

    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedChild = [System.IO.Path]::GetFullPath($Child)
    if (-not $resolvedChild.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify outside addins root: $resolvedChild"
    }
}

foreach ($version in $versions) {
    $targetVersionRoot = Join-Path $AddinsRoot $version
    $targetPluginRoot = Join-Path $targetVersionRoot $pluginName
    $targetManifest = Join-Path $targetVersionRoot "$pluginName.addin"
    Assert-ChildPath -Parent $AddinsRoot -Child $targetVersionRoot
    Assert-ChildPath -Parent $AddinsRoot -Child $targetPluginRoot

    if (Test-Path $targetManifest) {
        Remove-Item -LiteralPath $targetManifest -Force
    }

    if (Test-Path $targetPluginRoot) {
        Remove-Item -LiteralPath $targetPluginRoot -Recurse -Force
    }

    Write-Host "Uninstalled $pluginName for Revit $version" -ForegroundColor Yellow
}
