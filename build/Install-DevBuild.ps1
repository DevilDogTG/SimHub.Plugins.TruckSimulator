#Requires -Version 7.0
<#
.SYNOPSIS
    Copies the locally built plugin into a SimHub install for testing.

.DESCRIPTION
    Copies the Release build of the plugin assemblies and their .pdb files (so errors in SimHub's
    log carry line numbers) into the SimHub folder. Refuses to run while SimHub is open, because
    SimHub locks loaded plugin assemblies.

.PARAMETER SimHubPath
    SimHub install folder. Defaults to the folder SimHub records in the registry.

.EXAMPLE
    dotnet build -c Release; ./build/Install-DevBuild.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $SimHubPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repoRoot 'src/DevDogs.TruckSimulator/bin/Release/net48'

if (-not $SimHubPath) {
    $SimHubPath = (Get-ItemProperty -Path 'HKCU:\Software\SimHub' -Name 'InstallDirectory' -ErrorAction SilentlyContinue).InstallDirectory
}
if (-not $SimHubPath -or -not (Test-Path (Join-Path $SimHubPath 'SimHubWPF.exe'))) {
    throw "SimHub not found. Pass -SimHubPath <folder containing SimHubWPF.exe>."
}
if (Get-Process -Name 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'SimHub is running. Close it first; it locks loaded plugin assemblies.'
}
if (-not (Test-Path (Join-Path $output 'DevDogs.TruckSimulator.dll'))) {
    throw "No Release build at $output. Run 'dotnet build -c Release' first."
}

$files = @(
    'DevDogs.TruckSimulator.dll',
    'DevDogs.TruckSimulator.pdb',
    'DevDogs.TruckSimulator.Core.dll',
    'DevDogs.TruckSimulator.Core.pdb'
)

foreach ($file in $files) {
    $source = Join-Path $output $file
    if ($PSCmdlet.ShouldProcess((Join-Path $SimHubPath $file), 'Copy')) {
        Copy-Item -LiteralPath $source -Destination $SimHubPath -Force
        [PSCustomObject]@{
            File        = $file
            Destination = $SimHubPath
            Size        = (Get-Item -LiteralPath $source).Length
        }
    }
}
