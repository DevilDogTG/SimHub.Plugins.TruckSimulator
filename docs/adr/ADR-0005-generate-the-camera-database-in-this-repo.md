# ADR-0005: Generate the camera database in this repo, with camera facing

**Date:** 2026-10-11
**Status:** accepted (supersedes the "copied from the Radar repo" part of ADR-0004)
**Deciders:** DevDogs (maintainer)

## Context
ADR-0004 embedded a camera database copied from the DriveDogs Radar repo's Python extraction. Real
drives then showed the radar must know which way each camera faces: cameras fine only traffic travelling
the way they face (two fines at 0° and 3° from the camera's facing; seven over-the-limit passes at
50–180° off went unfined). The map stores that facing on each sign's node. The Radar mod itself is
strobe-only and doesn't use the database, so this plugin is its only consumer.

## Options Considered

### Option A: add facing to the Radar repo's Python tool and keep copying
- **Pros:** smallest change.
- **Cons:** this repo keeps depending on another repo for its data; two repos to change for every
  game update.

### Option B: port the extraction into this repo as a C# tool
- **Pros:** self-contained; one language with the plugin; runs on any machine with an extracted game
  (no agent tooling needed); also the place for ATS and later data generators.
- **Cons:** a port to verify; the Radar repo keeps its own Python copy for its own needs.

## Decision
Option B: `tools/DevDogs.TruckSimulator.DataTools` (`cameras` command) scans an extracted game for
camera models (`.pmg` with a `speedcam.*` hookup other than `speedcam.toll`), finds the placed sign
items in the map sectors, and reads each sign node's yaw as the camera's `facing` (compass degrees,
0 = north/−Z, 90 = east/+X). Output is the same `camera_db.csv` + `camera_db.meta.json`, plus a `facing`
column. On ETS2 1.61.1.1 its CSV is byte-identical to the Python extraction with facing added.

## Consequences
**Easier:** regenerating after game or map updates is one command in this repo; direction-aware alerts.
**Harder:** the tool needs an extraction that includes the sign models' `.pmg` files; it refuses to
write a database when some are missing (override with `--allow-missing-models`).
**Follow-up:** run it for ATS (`--game ats`, `map/usa`) when ATS support is added.

## Update 2026-11-01: facing comes from the camera locator, not the sign node alone
The node's yaw is only the model's orientation. In Finland (ETS2 1.61.1.9) a camera fined the truck with
no alert while the radar showed the camera across the road: `speedcamera_01_fi` is built facing the
other way. The validation above used `speed_camera_ch`, whose node happens to point the enforced way.

The game decides the direction from the model's `speedcam.*` locator and its hookup
(`unit/hookup/speed_camera.sii`: `speedcam.r` has `reverse_direction: true`). The tool now reads the
locator's rotation and composes: **facing = node heading − locator yaw + 180°**, without the 180° for a
reverse hookup. Calibrated on `speed_camera_ch` (locator turned 180°, plain hookup = the node's heading,
as validated above). A model whose camera locators enforce opposite directions (`speedcamera_hw_dk`,
`speedcamera_hw_fr`, …) is two-way: its facing is empty, so the radar alerts for it from either side.

Checked against back-to-back camera pairs on the map, using each camera's trigger point: the rule fixes
`speedcamera_01_fi`, `_01_lt`, `_01_es_ibe` and `speed_camera_02_be`, and turns five side-on models
(`speedcamera_small_01_it`, `_small_02_orange_it`, `_01_est`, `_01_es_ibe`, `_01_pt_ibe`) that were 90°
off and so never alerted with the facing filter on. Cameras are not always on the right of their
carriageway: `speedcamera_hw_ch` stands back-to-back in the median (owner's in-game check at Erfurt), so
no side-of-road rule is enforced; the tests pin known facings instead.

## Update 2026-11-01 (1.0.1): per-model corrections where the hookup contradicts the placement
`speedcamera_small_01_it` came out 180° off: in Italy the radar alerted for the camera across the road
and not for the one on the truck's side (owner report, toll section near 6.7k / 28.5k). Its `speedcam.20`
locator (19.96, 0, 7.00, yaw 90°) puts every one of its 32 placed cameras' trigger points on the other
carriageway, while `speedcamera_small_02_orange_it` has the same locator with `speedcam.r` and comes out
right. No single rule makes both right (turning the locator yaw the other way breaks the orange, `_01_est`
and `_01_es_ibe` models), so the tool keeps a short table of per-model turns (`CameraModels`), applied
after the rule. Each entry needs in-game evidence; models the side-of-road check only suspects
(`speedcamera_small_03_hw_it`, `_3way_hw_01_it`, `_3way_hw_02_it`, `_01_pt_ibe`) stay on the rule until
checked in game.
