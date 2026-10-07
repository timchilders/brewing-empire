# Unity integration (Phase 5 spike)

The deterministic core (`src/BreweryEmpire.Core`, netstandard2.1) is complete and
Unity-free. This folder is the front end that consumes it as a **compiled DLL** in
`Assets/Plugins/`, so the rules can never drift between editor and build.

## Prerequisites

- **Unity 2021.2 or newer** (netstandard2.1 support) via Unity Hub — **not yet
  installed on this machine**. Install it first.
- .NET 8 SDK (already present; used only to publish the DLL).

## Step 1 — publish the DLL

```bash
bash tools/publish-unity.sh
```

This writes `BreweryEmpire.Core.dll` plus its dependencies into
`Assets/Plugins/BreweryEmpire.Core/`. The dependencies are `System.Text.Json.dll`
and 7 transitives — Unity ships its own (older) `System.Text.Json`, so this is the
one thing the spike must validate (checkpoint 1 below).

## Step 2 — create the Unity project

Option A (recommended): create a fresh project in Unity Hub (any 3D template),
then copy `Assets/` from this repo into it (or symlink it).

Option B: open Unity Hub → "Add project from disk" → point at this `unity/` folder
(after Unity generates `ProjectSettings/` + `Packages/` on first open).

## Step 3 — first scene

Create an empty scene, add an empty GameObject, attach `BreweryEmpireBootstrap`
(`Assets/Scripts/BreweryEmpireBootstrap.cs`), press Play.

## Checkpoints

1. **Compile** — no `CS0433` (ambiguous `System.Text.Json`/`System.Memory`) or
   missing-assembly errors. If Unity reports a duplicate reference, the fallback is
   (a) source-link the core (`mklink /D Assets/BreweryEmpire.Core src/BreweryEmpire.Core`)
   and delete the published DLLs, or (b) pin `System.Text.Json` to a lower version
   matching Unity's. Pick one only if (1) fails.
2. **Run** — the dashboard shows `Burton Brewery`, `Balance`, and a market. Press
   "Advance 30 days": balance changes, batches appear, no exceptions. Same seed →
   identical dashboard (determinism holds through the DLL boundary).
3. **Save/load** (optional) — call `SaveSystem.Save(_state)` and `SaveSystem.Load`
   from a button; confirm the dashboard is byte-identical after reload + same ticks.

## Notes

- The core's public API is "call a static system, read `GameState`" — no event bus.
  The UI polls `_state` after each `TickSystem.AdvanceDay`.
- `unity/Assets/Plugins/BreweryEmpire.Core/*.dll` is build output and is gitignored;
  regenerate with `tools/publish-unity.sh`.
