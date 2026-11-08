<#
.SYNOPSIS
    Returns the Planetbase install folder.

.DESCRIPTION
    Resolution order: the -GameDir argument, the PLANETBASE_DIR environment variable, the parent folder of this
    repository (when it is cloned inside the game folder), then Steam's install record for Planetbase (app 403190).
    Throws when Planetbase.exe cannot be found.

.PARAMETER GameDir
    An explicit game folder. When given, it is the only candidate.

.EXAMPLE
    .\tools\Resolve-PlanetbaseDir.ps1
#>
[CmdletBinding()]
[OutputType([string])]
param(
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-SteamInstallLocation {
    [OutputType([string])]
    param()

    if ($PSVersionTable.PSEdition -ne 'Desktop' -and -not $IsWindows) {
        return $null
    }
    foreach ($view in [Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32) {
        $hive = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
        try {
            $key = $hive.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 403190')
            if ($null -ne $key) {
                try {
                    $location = $key.GetValue('InstallLocation')
                    if ($location) {
                        return [string] $location
                    }
                }
                finally {
                    $key.Dispose()
                }
            }
        }
        finally {
            $hive.Dispose()
        }
    }
    return $null
}

if ($GameDir) {
    $candidates = @($GameDir)
}
else {
    $candidates = @(
        $env:PLANETBASE_DIR
        (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
        (Get-SteamInstallLocation)
    ) | Where-Object { $_ }
}

foreach ($candidate in $candidates) {
    if (Test-Path -LiteralPath (Join-Path $candidate 'Planetbase.exe')) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
}

if ($GameDir) {
    throw "Planetbase.exe was not found in '$GameDir'."
}
throw 'Planetbase was not found. Pass the game folder with -GameDir, or set the PLANETBASE_DIR environment variable.'
