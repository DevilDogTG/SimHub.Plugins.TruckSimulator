# DevDogs.TruckSimulator

A [SimHub](https://www.simhubdash.com/) plugin that adds extra properties, events and actions for
**Euro Truck Simulator 2** and **American Truck Simulator**.

<img src="docs/images/plugin-icon.svg" width="48" alt="Plugin icon"> In SimHub it appears in the left
menu as **Truck Simulator**.

## Install

1. Download `DevDogs.TruckSimulator-<version>.zip` from the releases page.
2. Close SimHub.
3. Extract the zip into the SimHub folder, next to `SimHubWPF.exe` (usually `C:\Program Files (x86)\SimHub`).
4. Start SimHub and enable **DevDogs Truck Simulator** when SimHub asks.

To uninstall, close SimHub and delete the `DevDogs.TruckSimulator.*` files from the SimHub folder.

Requires SimHub 9.x on Windows.

## The plugin page

Open **Truck Simulator** in SimHub's left menu:

- **Settings:** over-speed margin, wear warning level, metric or imperial dashboard units, and the
  speed-camera radar's alert distance, cone angle and "only cameras facing my direction" filter.
- **Live telemetry:** switch on to see what this plugin reads from the game (each value with the SimHub
  property it comes from), every value it publishes, and its recent events. Only this plugin's data,
  so its values can't be confused with SimHub's own. Off by default; refreshed only while the tab is open.
- **Recording:** records the game values this plugin reads to a file, to replay real drives in tests
  (see [Telemetry recording](#telemetry-recording)).

## Properties

Every name below is prefixed with `DDTruckPlugin.`, for example `DDTruckPlugin.Job.Status`.

### Dashboard
| Property | Type | Meaning |
|---|---|---|
| `Dashboard.DisplayUnitMetric` | bool | Dashboards should show metric units (setting, toggled by the `SwitchDisplayUnit` action). |

### Damage
| Property | Type | Meaning |
|---|---|---|
| `Damage.WearAverage` | number | Average wear of cabin, chassis, engine, transmission and wheels, in percent (0–100). |
| `Damage.WearWarning` | bool | Average wear is above the wear warning level setting. |

### Drivetrain
| Property | Type | Meaning |
|---|---|---|
| `Drivetrain.GearDashboard` | text | Gear label: `N`, `R1`…, the gear number, or `C1`/`C2` crawler gears on 12+2 gearboxes (road gears renumbered from 1). |
| `Drivetrain.PeakTorque` | bool | Engine speed is inside the truck's peak-torque band. |
| `Drivetrain.PeakTorque.Min` / `.Max` | number | The peak-torque band, in RPM (known per base truck; 1000–1300 otherwise). |
| `Drivetrain.FuelRangeStable` | number | Fuel range in km, holding the last value while the game briefly reports 0. |
| `Drivetrain.FuelValue.AverageConsumptionLitresPer100Mile` | number | Average consumption, litres per 100 miles. |
| `Drivetrain.FuelValue.AverageConsumptionMilesPerGallonUK` | number | Average fuel economy, miles per UK gallon. |
| `Drivetrain.FuelValue.AverageConsumptionMilesPerGallonUS` | number | Average fuel economy, miles per US gallon. |

### Engine and lights
| Property | Type | Meaning |
|---|---|---|
| `Engine.Starting` | bool | The engine is cranking (turning but not yet running). |
| `Lights.HazardWarningOn` | bool | Hazard lights are on (both blinkers), held steady between flashes. |

### Job and navigation
| Property | Type | Meaning |
|---|---|---|
| `Job.Status` | text | `None`, `Taken`, `Loading`, `Ongoing`, `Completed` or `Abandoned`. |
| `Job.InProgress` | bool | The game reports that a job is active. |
| `Job.OverSpeedLimit` | bool | Speed is above the road's limit plus the over-speed margin setting. |
| `Job.OverSpeedLimitPercentage` | number | 0–1: position of the speed between the limit and the limit plus the margin. |
| `Job.NextRestWarning` | bool | Less than one in-game hour until the driver must rest. |
| `Job.RemainingDeliveryTime.Time.Days` / `.Hours` / `.Minutes` | number | Delivery time left, split into days, hours (0–23) and minutes (0–59). |
| `Navigation.TotalDaysLeft` / `.TotalHoursLeft` / `.Minutes` | number | Route time left, split into days, hours (0–23) and minutes (0–59). |

### City and country names
| Property | Type | Meaning |
|---|---|---|
| `L.Job.CitySource` / `L.Job.CityDestination` | text | Source and destination city. |
| `L.Job.CountrySource` / `L.Job.CountryDestination` | text | Source and destination country. |
| `L.A.Job.…` (same four names) | text | The same names using ASCII characters only (`Kraków` → `Krakow`). |
| `L.A.Job.CitySourceFromSDK` / `L.A.Job.CityDestinationFromSDK` | text | The city names as the game reports them, in ASCII. |

Translated names are not included yet; these properties show the names the game reports.

### Speed-camera radar (ETS2)
Warns when a fining speed camera is ahead. Cameras come from the game's map (707 in ETS2 1.61); a camera
counts only when it faces your direction of travel, because that is the only traffic it fines. In real
drives the game fined about 25 m before the camera, so the default 350 m alert gives plenty of warning.

| Property | Type | Meaning |
|---|---|---|
| `Radar.CameraAhead` | bool | A camera facing your direction is ahead within the alert distance and cone. |
| `Radar.Distance` | number | Distance to it: metres (metric) or miles (imperial); 0 when none. |
| `Radar.DistanceMeters` | number | Distance to it in metres, whatever the unit setting. |
| `Radar.CameraModel` | text | The camera model, for example `speed_camera_ch`. |
| `Radar.OverSpeed` | bool | A camera is ahead and you are above the limit plus the over-speed margin. |
| `Radar.Debug.*` | | Cameras loaded, database game version, truck X/Z, nearest camera (any direction) with distance, model and facing angle, alert counters. |

The radar holds an alert until the camera is alongside, lets a nearer camera take over, and keeps the
alert through pauses. Example dashboard: `examples/dashboards/DDTruck Radar Test`.

### Recording
| Property | Type | Meaning |
|---|---|---|
| `Recorder.Recording` | bool | A telemetry recording is running or waiting for game data. |

## Events and actions

| Event | When |
|---|---|
| `DamageIncrease` | Average wear rose by more than one percentage point (not on start-up). |
| `JobTaken`, `JobLoading`, `JobOngoing`, `JobCompleted`, `JobAbandoned` | `Job.Status` changes. |
| `JobReset` | The `JobStatusReset` action ran. |
| `RadarCameraAhead` / `RadarCameraPassed` | A camera came into the alert zone / was passed or left behind. |

| Action | Does |
|---|---|
| `SwitchDisplayUnit` | Toggles metric and imperial dashboard units. |
| `JobStatusReset` | Forgets the tracked job so the status is worked out again. |
| `ToggleTelemetryRecording` | Starts or stops a telemetry recording. |

## Moving from TruckSimulatorPlugin

Both plugins can be installed side by side. Property names after the prefix are the same, so moving a
dashboard over is a find-and-replace of `TruckSimulatorPlugin.` with `DDTruckPlugin.`. Values that
differ because the original had a bug are listed in the [changelog](CHANGELOG.md).

## Telemetry recording

**Start recording** on the Recording tab (or the `ToggleTelemetryRecording` action) records every
update's game values while ETS2 or ATS is running, to
`<SimHub>\PluginsData\Common\DevDogs.TruckSimulator\Recordings\*.jsonl.gz` (about 15 MB per hour). The
file is created when the first game data arrives. Recordings replay through the plugin in tests.

## Build

```powershell
dotnet build -c Release                 # needs SimHub installed; override with -p:SimHubPath=<folder>
dotnet test -c Release
./build/Install-DevBuild.ps1            # copy the build into SimHub (SimHub must be closed)
./build/New-ReleasePackage.ps1          # build, test and write artifacts/DevDogs.TruckSimulator-<version>.zip
```

The speed-camera database is generated from an extracted game (the extraction must include the sign
models' `.pmg` files):

```powershell
dotnet run --project tools/DevDogs.TruckSimulator.DataTools -- cameras --base <extracted game root> --game ets2 --out src/DevDogs.TruckSimulator.Core/Radar/Data/ets2
```

SimHub runs on .NET Framework 4.8, so the plugin targets `net48`; it is built and tested with the
.NET 10 SDK ([docs/adr](docs/adr)).

## Credits

This project is a rebuild of **[TruckSimulatorPlugin](https://github.com/sjdawson/TruckSimulatorPlugin)**
by **[Steven John Dawson (sjdawson)](https://github.com/sjdawson)**. The original plugin designed every
property, event and action this plugin starts from: the job status tracking, damage and wear warnings,
peak-torque and crawler-gear indicators, fuel range and consumption conversions, hazard light detection
and city/country localisation. All credit for that design belongs to the original author.

- Original repository: https://github.com/sjdawson/TruckSimulatorPlugin
- Original documentation: https://sjdawson.gitbook.io/trucksimulatorplugin/

The original repository does not carry a license. This project is written as a new code base against
the documented behaviour of the original.

## License

[MIT](LICENSE) © DevDogs
