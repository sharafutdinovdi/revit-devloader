[CmdletBinding()]
param(
    [string]$Version = $(if ($env:RELEASE_VERSION) { $env:RELEASE_VERSION } else { '0.0.0-local' }),
    [string]$StagingDir = (Join-Path $PSScriptRoot '../release/package-staging/RevitDevLoader'),
    [string]$OutputDir = (Join-Path $PSScriptRoot '../release/packages'),
    [switch]$InstallIfMissing
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw 'Version must be MAJOR.MINOR.PATCH with optional prerelease or build metadata.'
}
$numericVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
$StagingDir = (Resolve-Path -LiteralPath $StagingDir).Path
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$years = @(Get-ChildItem -LiteralPath $StagingDir -Directory |
    Where-Object { $_.Name -match '^20\d{2}$' } | Sort-Object Name)
if ($years.Count -eq 0) { throw "No staged Revit years found in $StagingDir. Run package.ps1 first." }
if (-not (Test-Path -LiteralPath (Join-Path $StagingDir 'LICENSE'))) { throw 'Staged LICENSE is missing.' }
foreach ($year in $years) {
    $apiFiles = @(Get-ChildItem -LiteralPath $year.FullName -Recurse -File -Filter 'RevitAPI*.dll')
    if ($apiFiles.Count) { throw "Autodesk API assemblies must not be distributed: $($apiFiles.Name -join ', ')" }
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $year.FullName 'RevitDevLoader.addin') -Raw
    foreach ($addin in $manifest.RevitAddIns.AddIn) {
        $assembly = [string]$addin.Assembly
        if ($assembly -notmatch '^RevitDevLoader\\([^\\/]+\.dll)$') {
            throw "Expected a relative RevitDevLoader assembly path in the $($year.Name) manifest: $assembly"
        }
        if (-not (Test-Path -LiteralPath (Join-Path $year.FullName $Matches[1]))) {
            throw "Manifest assembly is missing for Revit $($year.Name): $assembly"
        }
    }
}

function Find-Iscc {
    $candidates = @($env:INNOSETUP_ISCC)
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    if (${env:ProgramFiles(x86)}) { $candidates += "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
    if ($env:LOCALAPPDATA) { $candidates += "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" }
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { return $candidate }
    }
}
$iscc = Find-Iscc
if (-not $iscc -and $InstallIfMissing) {
    & choco install innosetup --no-progress -y
    if ($LASTEXITCODE -ne 0) { throw 'Chocolatey could not install Inno Setup.' }
    $iscc = Find-Iscc
}
if (-not $iscc) { throw 'ISCC.exe not found. Install Inno Setup 6, set INNOSETUP_ISCC, or pass -InstallIfMissing.' }

# Generated input stays inside the ignored release tree.
$generatedDir = Join-Path $PSScriptRoot '../release/installer-generated'
New-Item -ItemType Directory -Force -Path $generatedDir, $OutputDir | Out-Null
$includePath = [IO.Path]::GetFullPath((Join-Path $generatedDir 'RevitDevLoader.years.iss'))
$lines = @('[Components]')
foreach ($year in $years.Name) {
    $lines += 'Name: "revit{0}"; Description: "Revit {0}"' -f $year
}
$lines += '[Files]'
foreach ($year in $years.Name) {
    $lines += 'Source: "{{#StagingDir}}\{0}\*"; DestDir: "{{#AddinsRoot}}\{0}\RevitDevLoader"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit{0}' -f $year
    $lines += 'Source: "{{#StagingDir}}\{0}\RevitDevLoader.addin"; DestDir: "{{#AddinsRoot}}\{0}"; Flags: ignoreversion; Components: revit{0}' -f $year
}
$lines += '[UninstallDelete]'
foreach ($year in $years.Name) {
    $lines += 'Type: filesandordirs; Name: "{{#AddinsRoot}}\{0}\RevitDevLoader"; Components: revit{0}' -f $year
    $lines += 'Type: files; Name: "{{#AddinsRoot}}\{0}\RevitDevLoader.addin"; Components: revit{0}' -f $year
}
$lines += @('[Code]', 'procedure InitializeWizard;', 'var', '  Selected: String;', '  I: Integer;', 'begin')
$lines += @('  { Inno Setup has already applied explicit component switches. }',
    '  for I := 1 to ParamCount do',
    '    if Pos(''/COMPONENTS='', Uppercase(ParamStr(I))) = 1 then Exit;',
    '  Selected := '''';')
foreach ($year in $years.Name) {
    $lines += "  if RevitInstalled('$year') then Selected := Selected + 'revit$year,';"
}
$lines += @('  if Selected <> '''' then Delete(Selected, Length(Selected), 1);',
    '  WizardSelectComponents(Selected);', 'end;')
Set-Content -LiteralPath $includePath -Value $lines -Encoding utf8
foreach ($scope in @('user', 'admin')) {
    & $iscc "/DMyAppVersion=$Version" "/DNumericVersion=$numericVersion" "/DStagingDir=$StagingDir" "/DOutputDir=$OutputDir" "/DScope=$scope" "/DYearsInclude=$includePath" (Join-Path $PSScriptRoot 'RevitDevLoader.iss')
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $scope with exit code $LASTEXITCODE." }
    $installer = Join-Path $OutputDir "revit-devloader-$Version-$scope-setup.exe"
    if (-not (Test-Path -LiteralPath $installer)) { throw "Installer output is missing: $installer" }
    Write-Host "Installer created: $installer"
}
