# ADR-0006: Read game options from the loaded profile's config.cfg

**Date:** 2026-11-25 (design approved 2026-11-20, released in 1.2.0)
**Status:** accepted
**Deciders:** DevilDogTG (maintainer)

## Context
A dashboard should tell "high beams on" from "high beams on, and the game may dim them". With the game's
"Automatic high beams" option on, the game dims the lamp for oncoming traffic, but the telemetry's only high
beam field, `LightsValues.BeamHigh`, follows the switch (checked in game by the owner). Neither the SCS
telemetry SDK nor SimHub's game reader carries gameplay options.

The option is stored only in the profile's plain-text `config.cfg` (`uset g_automatic_high_beams "1"`). Each
profile has its own file, the profile folder is the profile name's UTF-8 bytes in hex, and a Steam Cloud
profile keeps it under Steam `userdata`, not Documents. No file records which profile is loaded, but
`game.log.txt` logs it at load (`Set profile finished: '<name>'`, `Profile type: <type>`). Checked in game
(ETS2 1.62.3.3s): the game rewrites `config.cfg` as soon as Options is applied, from the menu or while
driving; it also rewrites it unchanged at profile load, game load and quit; it does not log toggles.

## Options Considered

### Option A: infer the option on the dashboard from telemetry
- **Pros:** no plugin change.
- **Cons:** impossible: `BeamHigh` follows the switch, and no channel shows the dimmed lamp.

### Option B: a dashboard variable the user sets
- **Pros:** no file access.
- **Cons:** a `.djson` has no persistent user setting; the value is lost on every dashboard update and
  can't be shared with other dashboards.

### Option C: read the loaded profile's `config.cfg` in the plugin, with a setting override
- **Pros:** follows the game's own option with no lag; any dashboard can bind the property; the setting
  covers an unknown or wrongly picked profile.
- **Cons:** depends on undocumented file layouts (profile folders, `game.log.txt` lines) that a game
  update could change; file watching in the host.

### Active profile: newest `config.cfg` vs `game.log.txt`
The game writes the loaded profile's files on save and exit, so the newest `config.cfg` is a usable guess,
but it is wrong right after switching profiles. `game.log.txt` names the profile and its storage directly.

## Decision
Option C. `Lights.AutoHighBeam` is `true` only when the loaded profile's `config.cfg` has
`g_automatic_high_beams "1"`; the game not running, an unknown profile or a missing file give `false`, so a
dashboard shows plain "on" as before.
- The profile comes from the last `Set profile finished` / `Profile type` lines of `game.log.txt`, polled
  once a second from the last offset (the log starts over on each game run). With no such line, the newest
  `config.cfg` of all the game's profiles is used.
- Cloud profiles: every Steam `userdata` id is scanned (the newest file wins when several have the profile);
  local profiles: `Documents\<game>\profiles`. `steam_profiles` and its `.bak` copies are never read.
- The profile folder is watched; a change is re-read after a 300 ms debounce (one apply writes three files
  within 0.25 s) with full sharing, and published only when the value differs.
- Setting "Automatic high beams": Auto-detect (the file), Always on, Always off.
- Parsing, the log tracker, hex naming and the path picker live in Core (pure, unit-tested); file I/O and
  watching live in the host (`GameConfig/ProfileConfigWatcher`).

## Consequences
**Easier:** other profile options (`g_automatic_headlights` sits next to it) can be published the same way.
**Harder:** a game update that moves profiles or changes the `game.log.txt` lines breaks detection
silently (the property stays `false`); the setting is the user's fallback until the plugin is fixed.
**Follow-up:** the Trucker Information dashboard binds its high beam `Auto` state to the property
(dashboard 2.3.0, needs plugin 1.2.0+).
