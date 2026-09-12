[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Project,
    [ValidatePattern('^\d+\.\d+\.\d+$')] [string]$Version,
    [string]$OutputDir = (Join-Path $PWD 'artifacts/packages')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath $Project).Path
if (Test-Path -LiteralPath $projectPath -PathType Container) {
    $projects = @(Get-ChildItem -LiteralPath $projectPath -Filter '*.csproj' -File)
    if ($projects.Count -ne 1) { throw 'Project folder must contain one csproj.' }
    $projectPath = $projects[0].FullName
}
$projectRoot = Split-Path -Parent $projectPath
$manifestPath = Join-Path $projectRoot 'plugin.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (-not $Version) { $Version = $manifest.version }
$repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDir).Path
$payloadRoot = Join-Path $outputRoot ('.build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $payloadRoot | Out-Null
try {
    Push-Location $repository
    try {
        foreach ($year in $manifest.revit) {
            if ($year -notmatch '^20\d{2}$') { throw "Invalid Revit year: $year" }
            $configurationName = 'Release.R' + $year.Substring(2)
            & dotnet build $projectPath -c $configurationName "-p:Version=$Version" '-p:DeployAddin=false' '-p:LaunchRevit=false'
            if ($LASTEXITCODE -ne 0) { throw "Build failed for Revit $year." }
            $yearRoot = Join-Path $payloadRoot $year
            New-Item -ItemType Directory -Path $yearRoot | Out-Null
            Copy-Item -Path (Join-Path $projectRoot "bin/$configurationName/*") -Destination $yearRoot -Recurse
        }
    }
    finally { Pop-Location }
    $packageType = if (@($manifest.commands).Count -eq 0) { 'application' } else { 'command' }
    $entryPoint = if ($packageType -eq 'application') { $manifest.entry.applicationClass } else { $manifest.commands[0].class }
    & (Join-Path $PSScriptRoot 'New-DevLoaderPackage.ps1') -PayloadFolder $payloadRoot `
        -PluginId $manifest.id -Version $Version -MainAssembly ([System.IO.Path]::GetFileName($manifest.entry.assembly)) `
        -EntryPoint $entryPoint -PluginType $packageType -ManifestPath $manifestPath -OutputDir $outputRoot
}
finally { Remove-Item -LiteralPath $payloadRoot -Recurse -Force }
