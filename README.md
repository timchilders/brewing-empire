# Brewery Empire — Core Simulation

A deterministic, Unity-free C# engine for a brewing-empire strategy game set
from 1750 onward. Every brewing decision, spoilage risk and coin of profit is
resolved in integer arithmetic so that a given seed and sequence of actions
**always** reproduces the same world — on any platform, across a hundred-year
campaign, and after a save/load round trip.

The intent is to finish the logic foundation first, then drop a Unity front end
onto this library without touching the rules.

## Design pillars

- **Determinism is non-negotiable.** One tick = one day. The only randomness
  source is `DeterministicRandom` (xorshift128+), seeded from the game seed and
  persisted in the save. No `System.Random`, no `Guid.NewGuid`, no wall clock,
  no floating point in saved state. The build is verified against all of these.
- **Fun over strict realism**, but the fun is grounded in real brewing history:
  before germ theory you lose beer to infection you cannot explain; Burton's
  sulphated water makes pale ale sing; summer brewing is dangerous enough that
  Bavaria banned it outright in 1553.
- **Integers everywhere.** Money is `long` cents. Quality, flavour, hygiene and
  reputation are basis points (10000 = 100%). Volumes are whole litres. Saves
  are exact and never drift.

## Project layout

```
src/BreweryEmpire.Core/        netstandard2.1 — the whole game logic, no Unity
  Economy/                      Money, Ledger, PriceBook
  State/                        GameDate, DeterministicRandom, GameState, SaveSystem
  Model/                        Ingredients, Recipes, Vessels, Batches, Staff, ...
  Simulation/                  BrewingSystem, SpoilageSystem, SalesSystem, TickSystem
tests/BreweryEmpire.Core.Tests/ net8.0 — xunit + FluentAssertions
```

## Build & test

```bash
dotnet build -c Release
dotnet test
```

The Core project compiles with `TreatWarningsAsErrors` — a warning is a failed
build.

## How a turn works (`TickSystem.AdvanceDay`)

1. Calendar advances exactly one day.
2. Vessels tick; any whose occupancy expired free themselves.
3. `SpoilageSystem` rolls infection against the day's temperature × vessel
   hygiene × staff resistance (chemists like Hansen/Pasteur cut risk).
4. Batches that reached conditioning end become sellable.
5. `SalesSystem` sells what the local market absorbs; excise duty is charged.
6. Daily upkeep posts; wages post on the first of the month.
7. Bankruptcy is evaluated, ledger history is compacted, PRNG state is synced.

Nodes are always visited in sorted-id order so replay never depends on
insertion order.

## Save format

`SaveSystem` serializes to JSON through explicit DTOs (not by reflecting over
live entities). Saves carry a `SaveVersion`; a save newer than the build
supports is refused rather than silently mangled. Round-tripping a save and
resuming must continue identically — this is asserted by the integration tests.

## Known trade-offs

- Fixed 365-day year, no leap years (exact date arithmetic beats calendar
  fidelity here).
- Vessel occupancy is a simple day counter, not a per-stage brewing timeline.
- Rivals (`RivalBrewer`) are stubbed in Phase 1; the field exists so saves
  remain loadable once Phase 2 fills the type in.
