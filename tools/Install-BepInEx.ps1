<#
.SYNOPSIS
    Installs BepInEx 5 (Windows x86) into the Planetbase folder, after checking its SHA-256 hash.

.DESCRIPTION
    Planetbase 1.4.x is a 32-bit Unity 2022.3 (Mono) game: it needs the x86 build of BepInEx 5. The version and its
    SHA-256 hash are pinned in bepinex.psd1 (the version validated with Steam build 20301361 of the game).
    Nothing is overwritten when BepInEx is already installed, unless -Force is given.

.PARAMETER GameDir
    The Planetbase folder. Detected automatically when omitted (see Resolve-PlanetbaseDir.ps1).

.PARAMETER Force
    Reinstalls BepInEx even when it is already installed.

.EXAMPLE
    .\tools\Install-BepInEx.ps1

.EXAMPLE
    .\tools\Install-BepInEx.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Planetbase' -WhatIf
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive script with colored status messages.')]
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $GameDir,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GameDir = & (Join-Path $PSScriptRoot 'Resolve-PlanetbaseDir.ps1') -GameDir $GameDir
$bepInEx = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'bepinex.psd1')

if ((Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\core\BepInEx.dll')) -and -not $Force) {
    Write-Host "BepInEx is already installed in $GameDir (use -Force to reinstall)." -ForegroundColor Yellow
    return
}

$archive = Join-Path ([IO.Path]::GetTempPath()) ('BepInEx_{0}_{1}.zip' -f $bepInEx.Version, [guid]::NewGuid().ToString('N'))
try {
    Write-Host "Downloading $($bepInEx.Url)"
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $bepInEx.Url -OutFile $archive -UseBasicParsing

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash
    if ($hash -ne $bepInEx.Sha256) {
        throw "Unexpected SHA-256 hash ($hash): the download is corrupted or was tampered with. Nothing was installed."
    }
    Write-Host 'SHA-256 hash verified.' -ForegroundColor Green

    if ($PSCmdlet.ShouldProcess($GameDir, "Install BepInEx $($bepInEx.Version)")) {
        Expand-Archive -LiteralPath $archive -DestinationPath $GameDir -Force
        New-Item -ItemType Directory -Force -Path (Join-Path $GameDir 'BepInEx\plugins') | Out-Null
        Write-Host "BepInEx $($bepInEx.Version) installed in $GameDir" -ForegroundColor Green
    }
}
finally {
    # Always delete the temporary download, even with -WhatIf.
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue -WhatIf:$false
}
