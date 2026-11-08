<#
.SYNOPSIS
    Uninstalls Planetbase Trainer and, optionally, BepInEx.

.DESCRIPTION
    By default, removes only the plugin and its configuration file. With -RemoveBepInEx, also removes BepInEx
    (winhttp.dll, doorstop_config.ini, the BepInEx folder...): the game is then back to its original state.
    Saved games are never touched. Add -WhatIf to see what would be removed.

.PARAMETER GameDir
    The Planetbase folder. Detected automatically when omitted (see Resolve-PlanetbaseDir.ps1).

.PARAMETER RemoveBepInEx
    Also removes BepInEx, including any other BepInEx plugin installed in the game.

.PARAMETER KeepConfig
    Keeps the trainer configuration file (hotkeys and settings).

.EXAMPLE
    .\tools\Uninstall.ps1

.EXAMPLE
    .\tools\Uninstall.ps1 -RemoveBepInEx -WhatIf
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive script with colored status messages.')]
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string] $GameDir,
    [switch] $RemoveBepInEx,
    [switch] $KeepConfig
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GameDir = & (Join-Path $PSScriptRoot 'Resolve-PlanetbaseDir.ps1') -GameDir $GameDir

function Remove-IfPresent {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive script with colored status messages.')]
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    if ((Test-Path -LiteralPath $Path) -and $PSCmdlet.ShouldProcess($Path, 'Remove')) {
        Remove-Item -LiteralPath $Path -Recurse -Force
        Write-Host "Removed: $Path"
    }
}

Remove-IfPresent -Path (Join-Path $GameDir 'BepInEx\plugins\PlanetbaseTrainer')
if (-not $KeepConfig) {
    Remove-IfPresent -Path (Join-Path $GameDir 'BepInEx\config\com.lascampia.planetbasetrainer.cfg')
}

if ($RemoveBepInEx) {
    foreach ($file in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt') {
        Remove-IfPresent -Path (Join-Path $GameDir $file)
    }
    Remove-IfPresent -Path (Join-Path $GameDir 'BepInEx')
}

Write-Host 'Done.' -ForegroundColor Green
