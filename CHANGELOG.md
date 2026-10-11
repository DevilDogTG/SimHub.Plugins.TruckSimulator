# Changelog

All notable changes to this project. Versions follow [semantic versioning](https://semver.org/).

## [0.1.0] - 2026-10-11

First release: a rebuild of sjdawson's TruckSimulatorPlugin for SimHub 9.x, built with the .NET 10 SDK.

### Added
- All of the original plugin's properties, events and actions, under the `DDTruckPlugin.` prefix
  (job status, speeding, rest and delivery time, damage and wear, peak torque, crawler gear label,
  fuel range and consumption, hazard lights, navigation time, city and country names).
- ATS support for city and country name properties (the original was ETS2 only).
- Plugin page with Settings, Live telemetry and Recording tabs; listed in SimHub's left menu as
  **Truck Simulator** with its own icon.
- Live telemetry tab showing only this plugin's input values (with their SimHub sources), published
  values and recent events.
- Opt-in telemetry recording (`ToggleTelemetryRecording`), including the truck's world position and
  heading and any fines, for replaying real drives in tests.

### Changed
- Properties use the `DDTruckPlugin.` prefix instead of `TruckSimulatorPlugin.`, so both plugins can be
  installed together; settings are stored separately (`DDTruckPluginSettings`).
- `Job.Status` is published as text (`Ongoing`) rather than an enum value.
- City and country names are looked up by city id and fall back to the name the game reports; they
  clear when there is no job instead of keeping the last job's cities.

### Fixed (compared with the original plugin)
- Miles per gallon divided by consumption instead of multiplying it: 0.35 L/km now shows about 8 mpg
  (UK) instead of about 1.
- `Job.NextRestWarning` uses total hours: 1 day 0 h 30 min until rest no longer counts as under an hour.
- `DamageIncrease` no longer fires on start-up when the truck is already worn.
- ATS section updates no longer run while the game isn't running (operator-precedence bug).
- `Job.OverSpeedLimitPercentage` ramps smoothly with a negative over-speed margin instead of jumping
  from 0 to 1.
- A missing or unexpected game value no longer stops every property from updating: values are read
  safely, and a failing section is logged once while the others keep running.
