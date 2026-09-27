<#
.SYNOPSIS
    Creates a Desktop and/or Start Menu shortcut for the published KeyboardSound.exe.

.DESCRIPTION
    Plain Windows .lnk shortcuts via the WScript.Shell COM object - no installer, no registry
    changes, no admin rights. Both shortcuts point directly at the exe and pick up its embedded
    icon (Resources/app.ico, set via ApplicationIcon in KeyboardSound.App.csproj), so Explorer,
    the Start Menu and the taskbar all show the real KeyboardSound icon.

    Run this after publishing (see README.md's "Production build" section). By default it looks
    for publish\win-x64\KeyboardSound.exe next to the repo root; pass -ExePath to point at a
    different build.

.PARAMETER ExePath
    Path to KeyboardSound.exe. Defaults to <repoRoot>\publish\win-x64\KeyboardSound.exe.

.PARAMETER Desktop
    Create a Desktop shortcut. Default: on.

.PARAMETER StartMenu
    Create a Start Menu shortcut (current user, no admin rights needed). Default: on.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\create-shortcuts.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\create-shortcuts.ps1 -ExePath C:\Apps\KeyboardSound\KeyboardSound.exe
#>

param(
    [string]$ExePath,
    [bool]$Desktop = $true,
    [bool]$StartMenu = $true
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) {
    $ExePath = Join-Path $repoRoot 'publish\win-x64\KeyboardSound.exe'
}

if (-not (Test-Path $ExePath)) {
    throw "KeyboardSound.exe not found at '$ExePath'. Publish it first, e.g.:`n  dotnet publish src\KeyboardSound.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64"
}
$ExePath = (Resolve-Path $ExePath).Path
$workingDir = Split-Path -Parent $ExePath

function New-Shortcut {
    param([string]$LinkPath)

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($LinkPath)
    $shortcut.TargetPath = $ExePath
    $shortcut.WorkingDirectory = $workingDir
    $shortcut.IconLocation = "$ExePath,0"
    $shortcut.Description = 'KeyboardSound'
    $shortcut.Save()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
}

if ($Desktop) {
    $desktopPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'KeyboardSound.lnk'
    New-Shortcut -LinkPath $desktopPath
    Write-Host "Desktop shortcut created: $desktopPath"
}

if ($StartMenu) {
    $startMenuDir = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
    $startMenuPath = Join-Path $startMenuDir 'KeyboardSound.lnk'
    New-Shortcut -LinkPath $startMenuPath
    Write-Host "Start Menu shortcut created: $startMenuPath"
}
