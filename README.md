# DevDogs.TruckSimulator

A [SimHub](https://www.simhubdash.com/) plugin that adds extra properties, events and actions for
**Euro Truck Simulator 2** and **American Truck Simulator**.

> **Status:** early development. A rebuild of the original plugin on a modern .NET toolchain,
> extended with ATS localisation and new features.

## Credits

This project is a rebuild of **[TruckSimulatorPlugin](https://github.com/sjdawson/TruckSimulatorPlugin)**
by **[Steven John Dawson (sjdawson)](https://github.com/sjdawson)**. The original plugin designed every
property, event and action this plugin starts from: the job status tracking, damage and wear warnings,
peak-torque and crawler-gear indicators, fuel range and consumption conversions, hazard light detection
and city/country localisation. All credit for that design belongs to the original author.

- Original repository: https://github.com/sjdawson/TruckSimulatorPlugin
- Original documentation: https://sjdawson.gitbook.io/trucksimulatorplugin/

The original repository does not carry a license. This project is written as a new code base against
the documented behaviour of the original, and the author of the original will be contacted about it.

## Differences from the original

- Properties use the `DDTruckPlugin.` prefix (the original uses `TruckSimulatorPlugin.`), so both
  plugins can be installed side by side. Property names after the prefix are kept the same, so moving a
  dashboard over is a find-and-replace of the prefix.
- City and country localisation is generated from the games' own locale files, for both ETS2 and ATS.

## Requirements

- SimHub 9.x (Windows)
- .NET 10 SDK to build. SimHub runs on .NET Framework 4.8, so the plugin assembly targets `net48`;
  see [docs/adr](docs/adr) for why.

## Install

Download `DevDogs.TruckSimulator-<version>.zip`, close SimHub, extract the zip into the SimHub folder
(next to `SimHubWPF.exe`) and start SimHub. Enable "DevDogs Truck Simulator" when SimHub asks.
Properties appear under `DDTruckPlugin.*`.

## Build

```powershell
dotnet build -c Release                 # needs SimHub installed; override with -p:SimHubPath=<folder>
dotnet test -c Release
./build/Install-DevBuild.ps1            # copy the build into SimHub (SimHub must be closed)
./build/New-ReleasePackage.ps1          # build, test and write artifacts/DevDogs.TruckSimulator-<version>.zip
```

## License

[MIT](LICENSE) © DevDogs
