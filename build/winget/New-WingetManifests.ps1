[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [Parameter(Mandatory)]
    [string]$ReleaseTag,
    [Parameter(Mandatory)]
    [string]$OutputDir,
    [switch]$SkipDownload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($ReleaseTag -cne "v$Version") { throw 'ReleaseTag must equal v followed by Version.' }
$repository = 'sharafutdinovdi/revit-devloader'
$encodedTag = [Uri]::EscapeDataString($ReleaseTag)
$downloadRoot = "https://github.com/$repository/releases/download/$encodedTag"
$values = @{ VERSION = $Version }
$tempDir = Join-Path ([IO.Path]::GetTempPath()) ("devloader-winget-" + [Guid]::NewGuid())
try {
    if ($SkipDownload) {
        Write-Warning 'Template check only: SHA256 values are dummy hashes. Do not submit these manifests.'
        $values.RELEASE_DATE = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
    }
    else {
        New-Item -ItemType Directory -Path $tempDir | Out-Null
        $headers = @{ 'User-Agent' = 'RevitDevLoader-WinGet' }
        if ($env:GH_TOKEN) { $headers.Authorization = "Bearer $env:GH_TOKEN" }
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repository/releases/tags/$encodedTag" -Headers $headers
        $values.RELEASE_DATE = ([DateTime]$release.published_at).ToString('yyyy-MM-dd')
    }
    foreach ($scope in @('user', 'admin')) {
        $filename = "revit-devloader-$Version-$scope-setup.exe"
        $url = "$downloadRoot/$([Uri]::EscapeDataString($filename))"
        $values["$($scope.ToUpperInvariant())_URL"] = $url
        if ($SkipDownload) {
            $hash = '0' * 64
        }
        else {
            $downloadPath = Join-Path $tempDir $filename
            Invoke-WebRequest -Uri $url -OutFile $downloadPath
            $hash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
        }
        $values["$($scope.ToUpperInvariant())_SHA256"] = $hash
    }
    $manifestDir = Join-Path $OutputDir "manifests/s/Sharafutdinov/RevitDevLoader/$Version"
    New-Item -ItemType Directory -Force -Path $manifestDir | Out-Null
    foreach ($template in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.yaml.template') {
        $content = Get-Content -LiteralPath $template.FullName -Raw
        foreach ($key in $values.Keys) { $content = $content.Replace("{{$key}}", $values[$key]) }
        if ($content -match '\{\{[A-Z_]+\}\}') { throw "Unresolved placeholder in $($template.Name)." }
        $path = Join-Path $manifestDir $template.Name.Replace('.template', '')
        Set-Content -LiteralPath $path -Value $content -NoNewline -Encoding utf8
        Write-Host "Manifest created: $path"
    }
}
finally {
    if (Test-Path -LiteralPath $tempDir) { Remove-Item -LiteralPath $tempDir -Recurse -Force }
}
