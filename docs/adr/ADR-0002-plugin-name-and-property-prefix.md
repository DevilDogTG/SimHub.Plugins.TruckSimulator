# ADR-0002: Plugin name and property prefix

**Date:** 2026-10-09
**Status:** accepted
**Deciders:** DevDogs (maintainer)

## Context
SimHub prefixes every property, event and action a plugin registers with the plugin's class name. The
original plugin's class is `TruckSimulatorPlugin`, so its properties read `TruckSimulatorPlugin.Job.Status`.
Users' dashboards bind to those full names. This project is a separate plugin, and users may have both
installed.

## Options Considered

### Option A: keep the original class name (drop-in replacement)
- **Pros:** existing dashboards work unchanged.
- **Cons:** both plugins register the same names when installed together; users can't tell which
  plugin a property comes from; settings stored under the same key.

### Option B: new class name, same names after the prefix
- **Pros:** both plugins can run side by side; migrating a dashboard is a prefix find-and-replace.
- **Cons:** existing dashboards must be updated once.

## Decision
Option B. The plugin class is `DDTruckPlugin`, so properties read `DDTruckPlugin.<name>`. Settings use
the key `DDTruckPluginSettings`. The assembly and root namespace are `DevDogs.TruckSimulator`. Property,
event and action names after the prefix stay the same as the original plugin.

## Consequences
**Easier:** side-by-side installation; clear ownership of each property; no settings collision.
**Harder:** dashboards built for the original plugin need the prefix replaced.
**Follow-up:** the README documents the prefix change. Renaming `DDTruckPlugin` later is a breaking
change for every dashboard.
