#Requires -Version 7.0
<#
.SYNOPSIS
    Builds, tests and packages the plugin as a zip that extracts into the SimHub folder.

.DESCRIPTION
    Runs the tests, builds the plugin in Release, and writes
    artifacts/DevDogs.TruckSimulator-<version>.zip. The zip holds the two plugin assemblies at its
    root (users extract it into the SimHub folder) plus INSTALL.txt and the license.

.PARAMETER SimHubPath
    SimHub install folder used to resolve SimHub's assemblies. Defaults to the build's own lookup.

.EXAMPLE
    ./build/New-ReleasePackage.ps1
#>
[CmdletBinding()]
param(
    [string] $SimHubPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'DevDogs.TruckSimulator.slnx'
$pluginProject = Join-Path $repoRoot 'src/DevDogs.TruckSimulator/DevDogs.TruckSimulator.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'

$propertyArgs = @()
if ($SimHubPath) {
    $propertyArgs += "-p:SimHubPath=$SimHubPath"
}
$buildArgs = @('-c', 'Release') + $propertyArgs

try {
    dotnet build $solution @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

    dotnet test $solution -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }

    $version = (dotnet msbuild $pluginProject -getProperty:Version -p:Configuration=Release @propertyArgs).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $version) { throw "Could not read the version ($LASTEXITCODE)" }
    $output = Join-Path $repoRoot 'src/DevDogs.TruckSimulator/bin/Release/net48'

    $stage = Join-Path $artifacts "stage/$version"
    if (Test-Path $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null

    $payload = @(
        'DevDogs.TruckSimulator.dll',
        'DevDogs.TruckSimulator.Core.dll'
    )
    foreach ($file in $payload) {
        Copy-Item -LiteralPath (Join-Path $output $file) -Destination $stage
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stage 'DevDogs.TruckSimulator.LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'INSTALL.txt') -Destination (Join-Path $stage 'DevDogs.TruckSimulator.INSTALL.txt')

    $zip = Join-Path $artifacts "DevDogs.TruckSimulator-$version.zip"
    if (Test-Path $zip) {
        Remove-Item -LiteralPath $zip -Force
    }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

    [PSCustomObject]@{
        Version = $version
        Package = $zip
        Files   = (Get-ChildItem -LiteralPath $stage).Name
    }
}
catch {
    Write-Error "Packaging failed: $_"
    exit 1
}
