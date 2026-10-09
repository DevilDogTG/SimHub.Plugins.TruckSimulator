# ADR-0003: Generate city and country localisation from the games' files

**Date:** 2026-10-09
**Status:** accepted
**Deciders:** DevDogs (maintainer)

## Context
The original plugin exposes translated city and country names for ETS2 only. It reads them from JSON
files that its installer ships, but those files are not in its repository, so they can't be rebuilt or
updated. This plugin should support both ETS2 and ATS and stay current with game and map DLC updates.

## Options Considered

### Option A: reuse the original plugin's JSON files
- **Pros:** no work.
- **Cons:** no source to regenerate them; ETS2 only; out of date with newer map DLCs; unlicensed.

### Option B: generate at build time from the games' locale and city definition files
A generator in this repo reads an extraction of the game files and writes per-language JSON that ships
in the release zip.
- **Pros:** repeatable; covers ETS2 and ATS; the plugin only loads JSON; no setup for users.
- **Cons:** a maintainer must regenerate after game updates; the release ships names taken from the
  games' data.

### Option C: generate at runtime from the user's game install
- **Pros:** always current, including the user's own DLC; ships nothing taken from the games.
- **Cons:** the plugin would need an SCS archive reader and decoder, and a game-path setting, which
  would also push packaging from a zip to an installer.

## Decision
Option B: generate the localisation data at build time with a generator kept in this repository,
because it supports both games while keeping the plugin simple and the release a plain zip.

## Consequences
**Easier:** ATS support; a documented way to refresh data after game updates.
**Harder:** releases depend on a maintainer having an up-to-date extraction of both games.
**Follow-up:** the generator's language and input format are decided when it is built.
