[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$InputDir,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$')] [string]$Repo,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._/-]*$')] [string]$Tag,
    [string]$FeedRoot = (Join-Path $PWD 'artifacts/feed'),
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @(Get-ChildItem -LiteralPath $InputDir -File -Filter '*DevPayload*.zip' | Sort-Object Name)
if ($packages.Count -eq 0) { throw 'InputDir contains no DevPayload packages.' }
New-Item -ItemType Directory -Force -Path $FeedRoot | Out-Null
$icons = @{}
$plugins = [ordered]@{}
$releaseKeys = @{}
foreach ($package in $packages) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entry = $archive.GetEntry('release-info.properties')
        if ($null -eq $entry) { throw "Missing release-info.properties in $($package.Name)." }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { $metadata = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $values = @{}
        foreach ($line in ($metadata -split '\r?\n')) {
            if ($line.Trim().StartsWith('#')) { continue }
            $separator = $line.IndexOf('=')
            if ($separator -gt 0) { $values[$line.Substring(0, $separator).Trim()] = $line.Substring($separator + 1).Trim() }
        }
        foreach ($key in @('pluginId', 'releaseId', 'assemblyVersion', 'createdUtc', 'mainAssembly', 'versions')) {
            if (-not $values[$key]) { throw "Missing $key in $($package.Name)." }
        }
        if ($values['schemaVersion'] -ne '2') { throw "Expected payload schema 2 in $($package.Name)." }
        $pluginType = if ($values['pluginType']) { $values['pluginType'] } else { 'command' }
        if ($pluginType -notin @('command', 'application')) { throw "Invalid pluginType in $($package.Name)." }
        $entryKey = if ($pluginType -eq 'application') { 'applicationClass' } else { 'commandType' }
        if (-not $values[$entryKey]) { throw "Missing $entryKey in $($package.Name)." }
        $versions = @($values['versions'] -split '[,;]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        foreach ($year in $versions) {
            if ($null -eq $archive.GetEntry("payload/$year/$($values['mainAssembly'])")) {
                throw "Missing main assembly for Revit $year in $($package.Name)."
            }
        }
        $pluginId = $values['pluginId']
        if ($pluginId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "Unsafe pluginId: $pluginId" }
        $iconEntry = $archive.GetEntry('icon.png')
        if ($null -ne $iconEntry) {
            $iconName = "$pluginId-icon.png"
            $iconPath = Join-Path (Resolve-Path -LiteralPath $FeedRoot).Path $iconName
            if (-not $icons.ContainsKey($pluginId) -or [datetime]$values['createdUtc'] -gt $icons[$pluginId].Date) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($iconEntry, $iconPath, $true)
                $icons[$pluginId] = @{ Path = $iconPath; Name = $iconName; Date = [datetime]$values['createdUtc'] }
            }
        }
        $releaseKey = "$pluginId|$($values['releaseId'])"
        if ($releaseKeys.ContainsKey($releaseKey)) { throw "Duplicate release: $releaseKey" }
        $releaseKeys[$releaseKey] = $true
        if (-not $plugins.Contains($pluginId)) {
            $displayName = if ($values['displayName']) { $values['displayName'] } else { $pluginId }
            $plugins[$pluginId] = [ordered]@{ pluginId = $pluginId; displayName = $displayName; versions = @() }
        }
        if ($icons.ContainsKey($pluginId)) { $plugins[$pluginId]['icon'] = $icons[$pluginId].Name }
        $version = [ordered]@{
            releaseId = $values['releaseId']; assemblyVersion = $values['assemblyVersion']
            createdUtc = $values['createdUtc']; supportedRevit = $versions
            url = $package.Name; sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
            size = $package.Length; pluginType = $pluginType; mainAssembly = $values['mainAssembly']
        }
        $version[$entryKey] = $values[$entryKey]
        $plugins[$pluginId].versions += $version
    }
    finally { $archive.Dispose() }
}

New-Item -ItemType Directory -Force -Path $FeedRoot | Out-Null
$feedPath = Join-Path (Resolve-Path -LiteralPath $FeedRoot).Path 'feed.json'
$feed = [ordered]@{ schemaVersion = 1; channel = $Tag; generatedUtc = [datetime]::UtcNow.ToString('o'); plugins = @($plugins.Values) }
[System.IO.File]::WriteAllText($feedPath, ($feed | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
$uploadArgs = @('release', 'upload', $Tag, '--repo', $Repo, '--clobber') + @($packages.FullName) + @($icons.Values | ForEach-Object { $_.Path })
if ($DryRun) {
    Write-Host ('gh ' + (($uploadArgs | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '))
    Write-Host ('gh release upload {0} --repo {1} --clobber "{2}"' -f $Tag, $Repo, $feedPath)
}
else {
    & gh release view $Tag --repo $Repo --json tagName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Release '$Tag' must exist in $Repo before publishing." }
    & gh @uploadArgs
    if ($LASTEXITCODE -ne 0) { throw "Release upload failed with exit code $LASTEXITCODE." }
    & gh release upload $Tag --repo $Repo --clobber $feedPath
    if ($LASTEXITCODE -ne 0) { throw "Feed upload failed with exit code $LASTEXITCODE." }
}
[pscustomobject]@{ FeedPath = $feedPath; FeedUrl = "github-release://$Repo/$([uri]::EscapeDataString($Tag))/feed.json"; DryRun = [bool]$DryRun }
