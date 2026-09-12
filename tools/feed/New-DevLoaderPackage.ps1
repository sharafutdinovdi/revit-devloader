[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PayloadFolder,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')] [string]$PluginId,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')] [string]$Version,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*\.dll$')] [string]$MainAssembly,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z_][A-Za-z0-9_.+]*$')] [string]$EntryPoint,
    [ValidateSet('command', 'application')] [string]$PluginType = 'command',
    [string]$DisplayName,
    [string]$Icon,
    [string]$ManifestPath,
    [string]$Description = "",
    [string]$Author = "Dinar Sharafutdinov",
    [string]$AssemblyVersion,
    [string]$OutputDir = (Join-Path $PWD 'artifacts/packages')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ($Icon) {
    $Icon = (Resolve-Path -LiteralPath $Icon).Path
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($Icon)
    try {
        if ($image.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid -or
            $image.Width -ne 32 -or $image.Width -ne $image.Height) {
            throw 'Icon must be a 32x32 PNG.'
        }
    }
    finally { $image.Dispose() }
}

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
$archivePath = Join-Path (Resolve-Path -LiteralPath $OutputDir).Path "$PluginId-$Version.zip"
if (Test-Path -LiteralPath $archivePath) { throw "Package already exists: $archivePath" }
$stagingRoot = Join-Path (Resolve-Path -LiteralPath $OutputDir).Path ('.staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingRoot | Out-Null
try {
    if ($ManifestPath) {
        $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
        if ($manifest.schemaVersion -ne 2 -or $manifest.id -ne $PluginId) { throw 'Manifest schema or id mismatch.' }
        $manifest.version = $Version
        $manifest.revit = @($versions.Name)
    }
    else {
        $entry = [ordered]@{ assembly = "$($versions[-1].Name)/$MainAssembly" }
        $commands = @()
        if ($PluginType -eq 'application') { $entry['applicationClass'] = $EntryPoint }
        else { $commands = @([ordered]@{ id = 'default'; class = $EntryPoint; text = $DisplayName; tooltip = $Description; icon = 'icons/icon.png' }) }
        $manifest = [ordered]@{
            schemaVersion = 2; id = $PluginId; displayName = $DisplayName; version = $Version
            description = $Description; author = $Author; revit = @($versions.Name)
            icon = 'icons/icon.png'; entry = $entry; commands = $commands
        }
        if (-not $Icon) { throw 'A v2 package requires -Icon or -ManifestPath with an icons folder.' }
    }
    $metadataPath = Join-Path $stagingRoot 'plugin.json'
    [System.IO.File]::WriteAllText($metadataPath, ($manifest | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
    $temporaryArchive = Join-Path $stagingRoot 'package.zip'
    $archive = [System.IO.Compression.ZipFile]::Open($temporaryArchive, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $metadataPath, 'plugin.json') | Out-Null
        if ($Icon) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $Icon, 'icons/icon.png') | Out-Null
        }
        if ($ManifestPath) {
            $iconRoot = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $ManifestPath).Path) 'icons'
            foreach ($file in Get-ChildItem -LiteralPath $iconRoot -File -Filter '*.png') {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, "icons/$($file.Name)") | Out-Null
            }
        }
        foreach ($year in $versions) {
            $prefix = $year.FullName.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            foreach ($file in Get-ChildItem -LiteralPath $year.FullName -File -Recurse) {
                if ($file.Extension -eq '.pdb') { continue }
                $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, "$($year.Name)/$relative") | Out-Null
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
