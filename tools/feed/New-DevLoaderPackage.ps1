[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PayloadFolder,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')] [string]$PluginId,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')] [string]$Version,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*\.dll$')] [string]$MainAssembly,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z_][A-Za-z0-9_.+]*$')] [string]$EntryPoint,
    [ValidateSet('command', 'application')] [string]$PluginType = 'command',
    [string]$DisplayName,
    [string]$AssemblyVersion,
    [string]$OutputDir = (Join-Path $PWD 'artifacts/packages')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$payloadRoot = (Resolve-Path -LiteralPath $PayloadFolder).Path
$versions = @(Get-ChildItem -LiteralPath $payloadRoot -Directory |
    Where-Object { $_.Name -match '^20\d{2}$' } | Sort-Object Name)
if ($versions.Count -eq 0) { throw 'PayloadFolder must contain Revit year folders.' }
foreach ($year in $versions) {
    if (-not (Test-Path -LiteralPath (Join-Path $year.FullName $MainAssembly) -PathType Leaf)) {
        throw "Missing $MainAssembly for Revit $($year.Name)."
    }
}
if (-not $DisplayName) { $DisplayName = $PluginId }
if (-not $AssemblyVersion) {
    $assemblyPath = Join-Path $versions[-1].FullName $MainAssembly
    $AssemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()
}
foreach ($value in @($DisplayName, $AssemblyVersion)) {
    if ($value -match '[\r\n]') { throw 'Metadata values must be single-line strings.' }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$archivePath = Join-Path (Resolve-Path -LiteralPath $OutputDir).Path "$PluginId-DevPayload-$Version.zip"
if (Test-Path -LiteralPath $archivePath) { throw "Package already exists: $archivePath" }
$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('revit-devloader-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingRoot | Out-Null
try {
    $lines = @(
        'schemaVersion=2', "pluginId=$PluginId", "displayName=$DisplayName",
        "releaseId=$Version", "assemblyVersion=$AssemblyVersion",
        "createdUtc=$([datetime]::UtcNow.ToString('o'))", "pluginType=$PluginType",
        "mainAssembly=$MainAssembly", "versions=$(($versions.Name) -join ',')"
    )
    $entryKey = if ($PluginType -eq 'application') { 'applicationClass' } else { 'commandType' }
    $lines += "$entryKey=$EntryPoint"
    $metadataPath = Join-Path $stagingRoot 'release-info.properties'
    [System.IO.File]::WriteAllLines($metadataPath, $lines, (New-Object System.Text.UTF8Encoding($false)))
    $temporaryArchive = Join-Path $stagingRoot 'package.zip'
    $archive = [System.IO.Compression.ZipFile]::Open($temporaryArchive, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $metadataPath, 'release-info.properties') | Out-Null
        foreach ($year in $versions) {
            $prefix = $year.FullName.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            foreach ($file in Get-ChildItem -LiteralPath $year.FullName -File -Recurse) {
                if ($file.Extension -eq '.pdb') { continue }
                $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, "payload/$($year.Name)/$relative") | Out-Null
            }
        }
    }
    finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporaryArchive -Destination $archivePath
}
finally { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }

[pscustomobject]@{
    PluginId = $PluginId
    ReleaseId = $Version
    Path = $archivePath
    Sha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    Size = (Get-Item -LiteralPath $archivePath).Length
}
