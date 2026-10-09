# ADR-0004: Speed-camera radar with an embedded camera database

**Date:** 2026-10-09
**Status:** accepted
**Deciders:** DevDogs (maintainer)

## Context
The DriveDogs Radar mod proved in game (ETS2 1.61) that speed cameras ahead of the truck can be
detected by matching the truck's telemetry position against a list of cameras extracted from the map.
It also proved the game can't show an in-cab indicator for it, so detection and display move into this
plugin, where SimHub dashboards and LEDs can show it. The camera list is generated in the Radar repo
from the game's map for one game version, and rarely changes.

## Options Considered

### Option A: separate SimHub plugin
- **Pros:** independent releases.
- **Cons:** a second install and a second settings page; duplicates the telemetry reading this plugin
  already does.

### Option B: section in this plugin, database shared with the Radar repo
- **Pros:** one copy of the data.
- **Cons:** couples two repos' release cycles; a cloner of this repo couldn't build it alone.

### Option C: section in this plugin, database copied into this repo and embedded
- **Pros:** self-contained build and release; nothing for users to install or configure.
- **Cons:** two copies of the data; a new game version or map DLC needs a new copy and a release.

## Decision
Option C: the radar is a section of this plugin (`DDTruckPlugin.Radar.*`), and each game's camera
database is copied into `src/DevDogs.TruckSimulator.Core/Radar/Data/<game>/` and embedded in the
assembly. A copy is taken only when its `camera_db.meta.json` game version matches the game it is for;
otherwise it is regenerated with the Radar repo's tools first. There is no user override file, because
the data changes rarely.

## Consequences
**Easier:** one install; the radar is tested with the real database in unit tests (Berlin drive-through).
**Harder:** game or map updates need a regenerated database and a plugin release. The release ships
camera positions derived from the game's map data, as the localisation data does (ADR-0003).
**Follow-up:** ATS needs its own extraction (`Data/ats/`); the code already selects the database by game.
The database has no camera facing, so cameras on parallel roads or the opposite carriageway inside the
cone also alert; extracting sign rotation would allow direction-aware alerts.
