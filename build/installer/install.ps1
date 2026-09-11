param(
    [ValidatePattern("^20\d{2}$")]
    [string]$RevitVersion = "",
    [switch]$AllVersions,
    [string]$AddinsRoot = (Join-Path $env:APPDATA "Autodesk\Revit\Addins"),
    [string]$FeedUrl = "",
    [string]$FeedRepo = "",
    [string]$FeedTag = "",
    [string]$FeedAsset = "feed.json",
    [string]$UpdatesFolder = "",
    [ValidateRange(1, [int]::MaxValue)]
    [int]$RunRetentionCount = 3,
    [switch]$UseLocalUpdatesFallback,
    [switch]$ForceSettings
)

$ErrorActionPreference = "Stop"

$pluginName = "RevitDevLoader"
$packageRoot = $PSScriptRoot
# Years present in the package define what can be installed.
$availableVersions = @(Get-ChildItem -Path $packageRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "^20\d{2}$" } |
    Select-Object -ExpandProperty Name |
    Sort-Object)

if ($availableVersions.Count -eq 0) {
    throw "No Revit version folders found in package: $packageRoot"
}

if ($AllVersions) {
    $versions = $availableVersions
}
elseif ($RevitVersion) {
    if ($availableVersions -notcontains $RevitVersion) {
        throw "Revit $RevitVersion is not present in this package. Available: $($availableVersions -join ', ')"
    }
    $versions = @($RevitVersion)
}
else {
    $versions = @($availableVersions[-1])
}
$runRetentionWasProvided = $PSBoundParameters.ContainsKey("RunRetentionCount")

function Get-ExistingSetting {
    param(
        [string]$Path,
        [string]$Key
    )

    if (-not (Test-Path $Path)) {
        return ""
    }

    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $separator = $line.IndexOf("=")
        if ($separator -le 0) {
            continue
        }

        if ($line.Substring(0, $separator).Trim() -ieq $Key) {
            return $line.Substring($separator + 1).Trim()
        }
    }

    return ""
}

function Set-DevLoaderSettings {
    $settingsPath = Join-Path $env:LOCALAPPDATA "RevitDevLoader\settings.properties"
    $existingFeedUrl = Get-ExistingSetting -Path $settingsPath -Key "feedUrl"
    $existingFeedRepo = Get-ExistingSetting -Path $settingsPath -Key "feedRepo"
    $existingFeedTag = Get-ExistingSetting -Path $settingsPath -Key "feedTag"
    $existingFeedAsset = Get-ExistingSetting -Path $settingsPath -Key "feedAsset"
    $existingUpdatesFolder = Get-ExistingSetting -Path $settingsPath -Key "updatesFolder"
    $existingTestFeedPath = Get-ExistingSetting -Path $settingsPath -Key "testFeedPath"
    $existingRunRetentionCount = Get-ExistingSetting -Path $settingsPath -Key "runRetentionCount"
    $existingFallback = Get-ExistingSetting -Path $settingsPath -Key "useLocalUpdatesFallback"
    if (-not $ForceSettings -and
        [string]::IsNullOrWhiteSpace($UpdatesFolder) -and
        -not $runRetentionWasProvided -and
        (-not [string]::IsNullOrWhiteSpace($existingFeedUrl) -or -not [string]::IsNullOrWhiteSpace($existingFeedRepo))) {
        Write-Host "DevLoader settings kept: $settingsPath" -ForegroundColor Yellow
        return
    }

    $resolvedFeedUrl = $FeedUrl
    $resolvedFeedRepo = $FeedRepo
    $resolvedFeedTag = $FeedTag
    $resolvedFeedAsset = $FeedAsset
    $resolvedUpdatesFolder = $UpdatesFolder
    $resolvedRunRetentionCount = $RunRetentionCount
    if (-not $ForceSettings -and
        [string]::IsNullOrWhiteSpace($resolvedFeedUrl) -and
        [string]::IsNullOrWhiteSpace($resolvedFeedRepo)) {
        $resolvedFeedUrl = $existingFeedUrl
        $resolvedFeedRepo = $existingFeedRepo
        if (-not [string]::IsNullOrWhiteSpace($existingFeedTag)) {
            $resolvedFeedTag = $existingFeedTag
        }
        if (-not [string]::IsNullOrWhiteSpace($existingFeedAsset)) {
            $resolvedFeedAsset = $existingFeedAsset
        }
        if ([string]::IsNullOrWhiteSpace($resolvedUpdatesFolder)) {
            $resolvedUpdatesFolder = $existingUpdatesFolder
        }
        if (-not $runRetentionWasProvided) {
            $parsedRetention = 0
            if ([int]::TryParse($existingRunRetentionCount, [ref]$parsedRetention) -and $parsedRetention -ge 1) {
                $resolvedRunRetentionCount = $parsedRetention
            }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($resolvedFeedRepo) -and [string]::IsNullOrWhiteSpace($resolvedFeedTag)) {
        throw 'FeedTag is required when FeedRepo is provided.'
    }

    $settingsRoot = Split-Path -Parent $settingsPath
    New-Item -ItemType Directory -Force -Path $settingsRoot | Out-Null
    $fallback = if ($UseLocalUpdatesFallback) {
        "true"
    }
    elseif (-not $ForceSettings -and -not [string]::IsNullOrWhiteSpace($existingFallback)) {
        $existingFallback
    }
    else {
        "false"
    }
    $lines = @()
    if (-not [string]::IsNullOrWhiteSpace($resolvedFeedUrl)) {
        $lines += "feedUrl=$($resolvedFeedUrl.Trim())"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($resolvedFeedRepo)) {
        $lines += "feedRepo=$($resolvedFeedRepo.Trim())"
        $lines += "feedTag=$($resolvedFeedTag.Trim())"
        $lines += "feedAsset=$($resolvedFeedAsset.Trim())"
    }
    if (-not [string]::IsNullOrWhiteSpace($resolvedUpdatesFolder)) {
        $lines += "updatesFolder=$($resolvedUpdatesFolder.Trim())"
    }
    if (-not $ForceSettings -and -not [string]::IsNullOrWhiteSpace($existingTestFeedPath)) {
        $lines += "testFeedPath=$($existingTestFeedPath.Trim())"
    }
    $lines += "runRetentionCount=$resolvedRunRetentionCount"
    $lines += "useLocalUpdatesFallback=$fallback"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($settingsPath, $lines, $utf8NoBom)
    Write-Host "DevLoader settings written: $settingsPath" -ForegroundColor Green
    if ([string]::IsNullOrWhiteSpace($resolvedFeedUrl) -and [string]::IsNullOrWhiteSpace($resolvedFeedRepo)) {
        Write-Warning "Feed is not configured. Add feedUrl=github-release://owner/repo/TAG/feed.json or feedRepo=owner/repo to $settingsPath."
    }
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
    $source = Join-Path $packageRoot $version
    if (-not (Test-Path $source)) {
        throw "Missing package folder: $source"
    }

    $targetVersionRoot = Join-Path $AddinsRoot $version
    $targetPluginRoot = Join-Path $targetVersionRoot $pluginName
    New-Item -ItemType Directory -Force -Path $targetVersionRoot | Out-Null
    Assert-ChildPath -Parent $AddinsRoot -Child $targetVersionRoot
    Assert-ChildPath -Parent $AddinsRoot -Child $targetPluginRoot

    Get-ChildItem -Path $targetVersionRoot -File -Filter "$pluginName.*" -ErrorAction SilentlyContinue | Remove-Item -Force
    if (Test-Path $targetPluginRoot) {
        Remove-Item -LiteralPath $targetPluginRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $targetPluginRoot | Out-Null
    Copy-Item -Path (Join-Path $source "$pluginName.addin") -Destination (Join-Path $targetVersionRoot "$pluginName.addin") -Force
    Get-ChildItem -Path $source -File | Where-Object { $_.Extension -ne ".addin" } | Copy-Item -Destination $targetPluginRoot -Force
    Write-Host "Installed $pluginName for Revit $version -> $targetPluginRoot" -ForegroundColor Green
}

Set-DevLoaderSettings
