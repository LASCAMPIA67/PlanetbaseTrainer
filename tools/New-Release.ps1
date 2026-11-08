#Requires -Version 7.2
<#
.SYNOPSIS
    Builds the release packages of Planetbase Trainer into the artifacts folder.

.DESCRIPTION
    1. Builds the plugin in Release, with deterministic paths: artifacts\PlanetbaseTrainer.zip.
    2. Downloads BepInEx (version and SHA-256 hash pinned in bepinex.psd1, cached in artifacts\cache) and builds the
       all-in-one package: artifacts\PlanetbaseTrainer-with-BepInEx.zip.
    3. Writes artifacts\SHA256SUMS.txt.
    Planetbase must be installed: the plugin compiles against the game's own assemblies, which are never redistributed.

.EXAMPLE
    pwsh .\tools\New-Release.ps1
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive script with colored status messages.')]
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$project = Join-Path $root 'src/PlanetbaseTrainer/PlanetbaseTrainer.csproj'
$bepInEx = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'bepinex.psd1')

if (git -C $root status --porcelain) {
    Write-Warning 'The working tree has uncommitted changes: the packages will not match a commit.'
}

# 1. Plugin package, produced by the PackageRelease target of the project.
Write-Host 'Building the plugin (Release)...' -ForegroundColor Cyan
dotnet build $project --configuration Release --nologo -p:ContinuousIntegrationBuild=true -p:DeployToGame=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed (exit code $LASTEXITCODE)."
}
$version = (dotnet msbuild $project -getProperty:Version).Trim()
$pluginZip = Join-Path $artifacts 'PlanetbaseTrainer.zip'
if (-not (Test-Path -LiteralPath $pluginZip)) {
    throw "$pluginZip was not produced."
}

# 2. All-in-one package: BepInEx, then the plugin on top of it.
$cache = Join-Path $artifacts 'cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$bepInExZip = Join-Path $cache ([IO.Path]::GetFileName(([uri] $bepInEx.Url).AbsolutePath))
if (-not (Test-Path -LiteralPath $bepInExZip)) {
    Write-Host "Downloading $($bepInEx.Url)"
    Invoke-WebRequest -Uri $bepInEx.Url -OutFile $bepInExZip
}
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $bepInExZip).Hash
if ($hash -ne $bepInEx.Sha256) {
    Remove-Item -LiteralPath $bepInExZip -Force
    throw "Unexpected SHA-256 hash for $bepInExZip ($hash). The file was deleted: run the script again."
}

$staging = Join-Path $artifacts 'bundle'
Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -LiteralPath $bepInExZip -DestinationPath $staging
Expand-Archive -LiteralPath $pluginZip -DestinationPath $staging -Force
$bundleZip = Join-Path $artifacts 'PlanetbaseTrainer-with-BepInEx.zip'
Remove-Item -LiteralPath $bundleZip -Force -ErrorAction SilentlyContinue
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $bundleZip, [IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $staging -Recurse -Force

# 3. Checksums, in the GNU coreutils format ("sha256sum -c SHA256SUMS.txt" works).
$sums = foreach ($file in $pluginZip, $bundleZip) {
    '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant(), (Split-Path -Leaf $file)
}
$sumsFile = Join-Path $artifacts 'SHA256SUMS.txt'
[IO.File]::WriteAllText($sumsFile, ($sums -join "`n") + "`n")

Write-Host ''
Write-Host "Planetbase Trainer $version packages:" -ForegroundColor Green
foreach ($file in $pluginZip, $bundleZip, $sumsFile) {
    Write-Host ('  {0,-40} {1,10:N0} bytes' -f (Split-Path -Leaf $file), (Get-Item -LiteralPath $file).Length)
}
