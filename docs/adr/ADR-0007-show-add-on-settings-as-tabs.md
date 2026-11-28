# ADR-0007: Show other DevDogs plugins' settings as tabs through an AppDomain slot

**Date:** 2026-11-27
**Status:** accepted
**Deciders:** DevilDogTG (maintainer)

## Context
DevDogs.TruckSimulator.NavigationDash needs this plugin (it reads `DDTruckPlugin.*` properties) and has its own
settings page. The owner wants it configurable from this plugin's page, as an add-on, while the two stay separate
plugins: own repositories, releases, properties and settings files (no merge).

SimHub gives each plugin with a settings page its own page: in the left menu when the plugin's "Show in left main
menu" switch (Add/remove features) is on, else under "Additional plugins". Nothing in SimHub puts one plugin's page
inside another's.

Neither plugin may depend on the other's assembly: SimHub loads every DLL in its folder, and a plugin whose types
refer to a missing assembly fails to load, so NavigationDash could no longer say "DevDogs.TruckSimulator is missing".
SimHub loads plugins in no fixed order, and on every game switch it ends both plugins and creates new instances.

A spike (2026-11-27, `1.3.0-spike.1` of both plugins in SimHub 9.13.5) showed the approach below working: the tab
shows NavigationDash's whole page and its Save; NavigationDash's own page keeps working; the tab follows game switches;
NavigationDash with this plugin's 1.2.0 (no tabs) works as before.

## Options Considered

### Option A: merge NavigationDash into this plugin
- **Pros:** one plugin, one page.
- **Cons:** rejected by the owner: separate releases, and most users of this plugin don't need navigation.

### Option B: a shared contract assembly both plugins reference
- **Pros:** typed interface.
- **Cons:** the add-on's plugin class would fail to load without this plugin; two copies of the contract DLL in the
  SimHub folder (one per plugin zip) would overwrite each other.

### Option C: an AppDomain data slot holding BCL and WPF types only
- **Pros:** no assembly reference either way; works in any load order; an add-on that finds no host keeps its own page.
- **Cons:** an untyped contract (string keys), so it is versioned by the slot's name and documented here.

## Decision
Option C.
- **Slot:** AppDomain data `DevDogs.TruckSimulator.AddOns.v1`, a `ConcurrentDictionary<string, object>` (ordinal keys)
  of add-on id to entry. The first add-on creates it, locking on the interned slot name; this plugin only reads it.
- **Entry:** an `IDictionary<string, object>` with `title` (string, the tab header; the id when missing),
  `createSettings` (`Func<System.Windows.Controls.Control>`, required: entries without it are left out) and,
  optionally, `version` (string). Keys this plugin does not know are ignored, so an add-on may send more.
- **Add-on lifecycle:** the add-on sets its entry in `Init` (a new entry object each time) and removes it in `End` only
  while the slot still holds that same object, so the old instance of a game switch cannot remove the new one's entry.
- **This page:** reads the slot when shown and every 2 s while shown; one tab per add-on after Settings / Live
  telemetry / Recording, ordered by id; a changed entry object rebuilds the tab's content; a missing entry removes the
  tab. An exception from `createSettings` shows as a message in that tab.
- Reading the slot is in Core (`AddOns/AddOnSlot`, unit-tested); the tabs are in the plugin (`UI/AddOnTabs`).
- A breaking change to the entry needs a new slot name (`...AddOns.v2`), read next to v1 for a while.
- **Version line** (added 2026-11-28, 1.4.0): the line under the page's title names each add-on after this plugin's
  version, for example `Version 1.4.0 · Navigation Dash 1.5.1`, refreshed with the tabs. Entries whose ids share the
  part before the first `.` are one add-on with several tabs (NavigationDash's `navigationdash` and
  `navigationdash.reports`): it is named once, by the first entry's title (ordinal id order) and the first `version`
  in the group. An add-on that sends no `version` gets a tab but is not named on the line. The entry is unchanged; an
  add-on with several tabs gives them ids under one root (Core `AddOns/AddOnVersions`, unit-tested).

## Consequences
**Easier:** any DevDogs plugin can show its page here with a few lines, in any release order: without a host it keeps
its own page, with an older host nothing changes.
**Harder:** the contract is not checked by the compiler; this ADR and `AddOnSlot` are its definition.
**Follow-up:** NavigationDash registers in its 1.3.0 (its own ADR-0007); its own page stays, and users who prefer the
tab can turn off its "Show in left main menu" switch.
