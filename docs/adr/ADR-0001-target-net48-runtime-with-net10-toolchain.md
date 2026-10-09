# ADR-0001: Target the net48 runtime, build with the .NET 10 toolchain

**Date:** 2026-10-09
**Status:** accepted
**Deciders:** DevDogs (maintainer)

## Context
The goal is to rebuild sjdawson/TruckSimulatorPlugin on a modern .NET 10 toolchain. SimHub loads
plugins into its own process. On the installed SimHub 9.13.2:

- `SimHubWPF.exe.config` declares `<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />`.
- `SimHub.Plugins.dll` and `GameReaderCommon.dll` target `.NETFramework,Version=v4.8`.

A .NET Framework 4.8 process cannot load an assembly built for `net10.0`, so the plugin assembly itself
cannot target .NET 10.

## Options Considered

### Option A: net48 plugin, .NET 10 SDK, netstandard2.0 core, net10.0 tests
SDK-style projects built with `dotnet build`. The plugin targets `net48`, the logic sits in a
`netstandard2.0` library, and the tests run on `net10.0`. Modern C# through `LangVersion=latest` and
PolySharp.
- **Pros:** loads in SimHub today; one CLI for build and test; modern C# syntax; the logic is tested
  on .NET 10 without SimHub.
- **Cons:** the runtime is still .NET Framework, so .NET 10-only BCL APIs and packages can't be used in
  the plugin or core.

### Option B: .NET 10 sidecar process plus a net48 shim plugin
A .NET 10 process reads the SCS telemetry shared memory and relays it to a thin net48 plugin over IPC.
- **Pros:** real .NET 10 runtime.
- **Cons:** re-implements the telemetry reader SimHub already provides; two processes, IPC latency,
  lifecycle handling and a harder install; no user-visible gain.

### Option C: wait for SimHub to move to modern .NET
- **Pros:** no work.
- **Cons:** no sign that it will happen.

## Decision
Option A: the plugin targets `net48` and everything is built and tested with the .NET 10 SDK, because
it is the only option that loads in SimHub without re-implementing what SimHub already provides.

## Consequences
**Easier:** standard `dotnet build` / `dotnet test`; testing the logic in CI without SimHub installed.
**Harder:** the core library must stay `netstandard2.0`-compatible; C# features that need runtime
support (for example default interface members) are unavailable.
**Follow-up:** if SimHub moves to modern .NET, retarget the plugin and core and drop PolySharp.
