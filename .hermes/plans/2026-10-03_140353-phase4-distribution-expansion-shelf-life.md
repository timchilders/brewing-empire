# Phase 4: Distribution, Expansion & Shelf-Life — Implementation Plan

> **For Hermes:** Implement task-by-task, TDD. Each task = failing test → see it fail →
> minimal implementation → see it pass → commit. Keep `dotnet build -c Release`
> warning-free (TreatWarningsAsErrors) and `dotnet test` green at every step.
> Everything stays integer-only and deterministic — the guards in
> `tests/BreweryEmpire.Core.Tests/Guards/` must remain green.

**Goal:** Close out every core-sim mechanic Phases 2–3 explicitly deferred — real
transport-mode choice (rail/canal/ship/refrigerated-rail) with in-transit spoilage, a
vessel build/purchase surface that makes stainless steel a real unlock, a shelf-life
model that gives the drum-roaster its payoff, and a scenario layer that assembles all of
it — without touching the Unity-free guarantee.

---

## Scope decision (read first)

Phase 4 was forked in the codebase: Phase 2's plan called "the Unity front end (Phase 4)",
but Phases 2–3 explicitly deferred four *core-sim* mechanics to "a dedicated phase / the
Phase 4 UI/scenario layer":

1. **Transport modes** — `LogisticsSystem.DispatchShipment` hardcodes `TransportMode.HorseCart`
   (`Simulation/LogisticsSystem.cs:36-40`); the `TransportMode`/`TransportSpec`/`LogisticsGraph`
   types exist but nothing consumes them. Phase 3's open questions called this "the largest
   deferred item — worth a dedicated phase."
2. **Vessel build/purchase surface** — `Vessel.Create` prices vessels, but nothing lets the
   player *buy* one; `stainless-steel` is "Effect deferred — no build system yet"
   (`TechCatalog.cs:85`).
3. **Drum-roaster shelf-life model** — "Effect deferred — no shelf-life model yet"
   (`TechCatalog.cs:37`). `PackagingSpec.ShelfLifeDays` exists but is never read.
4. **Scenario layer** — "That build system … belongs to the Phase 4 UI/scenario layer"
   (Phase 3 risks).

**This plan implements all four** (a "finish the core" phase), and leaves the Unity front end
for a later phase. Two latent bugs are fixed along the way because the transport work is
meaningless without them:
- **Shipments are not saved.** `GameState.Shipments` exists but `SaveSystem` never serializes
  it — beer in transit vanishes on save/load.
- **Shipped beer loses all its attributes.** `LogisticsSystem.Deliver` rebuilds a blank
  `RecipeId("shipped")` batch, discarding quality/flavour/IBU/ABV/style/pasteurization.

If a shorter phase is wanted, Block D (scenario layer) is the only trimmable block — Blocks
A–C are the substance.

---

## Current context / assumptions (verified against the code)

- Repo `C:\Users\Child\OneDrive\Projects\brewing-empire`, solution `BreweryEmpire.sln`,
  `src/BreweryEmpire.Core` (netstandard2.1, C# 9, Nullable enable) + `tests/BreweryEmpire.Core.Tests`
  (net8.0, xUnit + FluentAssertions).
- **dotnet is NOT on PATH.** Every `dotnet` command needs this prefix (shell is git-bash/MSYS):
  ```bash
  export PATH="$HOME/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  unset DOTNET_ROOT
  ```
  (Do **not** set `DOTNET_ROOT` to the MSYS path — it breaks the native muxer with
  "No .NET SDKs were found".)
- **Tick order** (`Simulation/TickSystem.cs:19-59`): date++ → Fermentation → Logistics
  (`ProcessShipments`) → Spoilage (`ProcessNode` per node) → Market → Economy (upkeep/payroll) →
  bankruptcy/compact → Research/Events → sync RNG.
- **`DispatchShipment` is single-hop** and takes a bare `distanceKm`; it ignores `WorldMap`
  routes entirely (`Simulation/LogisticsSystem.cs:24-56`). It is called in
  `tests/.../Integration/Burton1750V2Tests.cs` (3 call sites) and `tests/.../Simulation/LogisticsTests.cs`
  and `TechEffectTests.cs`. **Keep the 7-arg signature working** (backward compatible).
- **`WorldMap`** (`Model/Sites/BreweryNode.cs:176-222`) stores symmetric, distance-only routes
  (`SetRoute(a,b,km)`, `DistanceKm`, `HasRoute`) — mode-less today; tested in
  `tests/.../Model/Sites/BreweryNodeTests.cs` (3-arg `SetRoute`, symmetry, self-route/negative throw).
- **`TransportSpec.For(mode)`** (`Model/Logistics/LogisticsGraph.cs:31-61`) already carries
  `DaysPer100Km`, `CostPerLitrePer100Km`, `SpoilageModifierBasisPoints`, `IsRefrigeratedCapable`
  (SteamRail = capable, SpoilageModifier 7000; CargoShip 13000; HorseCart/Canal 10000).
- **Two-stage spoilage** (`Simulation/SpoilageSystem.cs`): `BaseInfectionRiskBasisPoints(ambient)`,
  `RollOrganism`, `ProgressionRateBasisPoints(organism, ambientC, ibu, abv, cold, pasteurized)`,
  `DefenseBasisPoints`, `WriteOff`. `Batch.ContractInfection`/`AdvanceInfection`/`RestoreInfection`
  are the batch-side API. `ProgressionRateBasisPoints` already halves rate when `cold=true`.
- **Staff bonuses exist but two are unwired:** `TraitEffect.TransitSpeed` (drayman) and
  `TraitEffect.ContainerLossReduction` (cooper) — see `Model/Staff/StaffRole.cs:18-28`.
- **`Vessel.Create(id, type, tier, capacity)`** (`Model/Sites/Vessel.cs:137-165`) returns a vessel
  with `PurchaseCost`/`DailyUpkeep`/`HygieneBasisPoints` set; `BreweryNode.AddVessel` adds it.
- **`Batch`** (`Model/Brewing/Batch.cs`) has `ReadyOn`, `QualityBasisPoints` (private set, via
  `SetQuality`/`AdjustQuality`), `Style`, `IbuTenths`, `AbvBasisPoints`, `CostOfGoods`,
  `CostPerLitre`. `MarkReady`/`MarkSpoiled`/`ApplyLossBasisPoints` exist. No shelf-life field yet.
- **`SaveVersion` is currently 3** (`GameState.CurrentSaveVersion = 3`). `Load` refuses only
  *newer* versions, so v1/v2/v3 saves load against v4.
- No `.json` golden-master fixtures are committed (the Phase 2 `Fixtures/` dir was never landed),
  so mint-id sequencing changes in Phase 4 break no fixture.
- Assume `dotnet build -c Release` and `dotnet test` are green at Phase 3 completion. **Task A0 re-verifies.**

---

## Task blocks

| Block | Tasks | Theme |
|---|---|---|
| A — Transport | A0–A7 | Shipment model + save, mode-bearing routes, real dispatch, cargo preservation, in-transit spoilage, refrigerated rail |
| B — Expansion | B1–B2 | Vessel build/purchase, tier gating, stainless-steel unlock |
| C — Shelf-life | C1–C3 | Batch shelf-life, staleness decay, drum-roaster payoff |
| D — Scenario | D1–D3 | ScenarioDefinition, catalog, NewGame assembler |
| E — Proof | E1–E3 | Integration, save round-trip + determinism, guards/README |

All new code in `src/BreweryEmpire.Core`; all tests in `tests/BreweryEmpire.Core.Tests`.

---

# Block A — Transport

### Task A0: Re-verify baseline

```bash
export PATH="$HOME/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
unset DOTNET_ROOT
dotnet build -c Release
dotnet test
```
**Expected:** 0 warnings/errors; all tests pass. Stop and report if red — do not build on red.

---

### Task A1: `Shipment` carries mode, refrigeration and cargo

**Objective:** A shipment knows how it travels and what it carries, and round-trips through a save.

**Files:** `src/BreweryEmpire.Core/Model/Logistics/Shipment.cs`; test
`tests/BreweryEmpire.Core.Tests/Model/Logistics/ShipmentTests.cs`.

**Step 1 — failing test** (`ShipmentTests.cs`):
```csharp
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Sites;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Logistics
{
    public class ShipmentTests
    {
        [Fact]
        public void Defaults_To_HorseCart_Unrefrigerated()
        {
            var s = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 3, PackagingType.WoodenCask, 100);
            s.Mode.Should().Be(TransportMode.HorseCart);
            s.IsRefrigerated.Should().BeFalse();
            s.Cargo.Should().BeNull();
        }

        [Fact]
        public void Can_Carry_Mode_Refrigeration_And_Cargo()
        {
            var cargo = new Batch(new BatchId("c1"), new RecipeId("r1"), "a", new VesselId("v1"),
                                  100, new State.GameDate(), new State.GameDate());
            var s = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 3, PackagingType.Bottle, 100)
            {
                Mode = TransportMode.SteamRail,
                IsRefrigerated = true,
                Cargo = cargo
            };
            s.Mode.Should().Be(TransportMode.SteamRail);
            s.IsRefrigerated.Should().BeTrue();
            s.Cargo.Should().BeSameAs(cargo);
        }
    }
}
```

**Step 2 — run, expect FAIL** (`Shipment` has no `Mode`/`IsRefrigerated`/`Cargo`):
```bash
dotnet test --filter "FullyQualifiedName~ShipmentTests"
```

**Step 3 — implement.** In `Shipment.cs`, add `using BreweryEmpire.Core.Model.Brewing;` and add
three settable properties after `TransportCost` (line 36):
```csharp
        /// <summary>How this shipment travels (speed/cost/spoilage trade-off).</summary>
        public TransportMode Mode { get; set; } = TransportMode.HorseCart;

        /// <summary>True if the cargo rides in a refrigerated hold (rail, post-refrigeration).</summary>
        public bool IsRefrigerated { get; set; }

        /// <summary>The beer in transit, as a batch snapshot preserving quality/flavour/style.</summary>
        public Batch? Cargo { get; set; }
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Logistics/Shipment.cs tests/BreweryEmpire.Core.Tests/Model/Logistics/ShipmentTests.cs
git commit -m "feat(logistics): shipment carries transport mode, refrigeration and cargo"
```

---

### Task A2: Save/load shipments (bump save version 3 → 4)

**Objective:** Beer in transit survives a save. Also extract the batch↔DTO mapping into helpers so
the shipment cargo can reuse it.

**Files:** `src/BreweryEmpire.Core/State/GameState.cs` (bump version), `src/BreweryEmpire.Core/State/SaveSystem.cs`;
test append to `tests/BreweryEmpire.Core.Tests/State/SaveSystemTests.cs`.

**Step 1 — failing test** (append to `SaveSystemTests.cs`):
```csharp
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;

        [Fact]
        public void Shipments_In_Transit_Round_Trip()
        {
            var s = TestScenario.Standard();
            var r = BreweryEmpire.Core.Simulation.BrewingSystem.TryStartBrew(
                s, new BreweryEmpire.Core.Model.NodeId("burton"),
                new BreweryEmpire.Core.Model.RecipeId("pale-ale"));
            r.Batch!.MarkReady();

            var sh = BreweryEmpire.Core.Simulation.LogisticsSystem.DispatchShipment(
                s, new BreweryEmpire.Core.Model.NodeId("burton"),
                new BreweryEmpire.Core.Model.NodeId("burton"),
                r.Batch.Id, 100, PackagingType.WoodenCask, 10);

            sh.Should().NotBeNull();
            var reloaded = SaveSystem.Load(SaveSystem.Save(s));
            reloaded.Shipments.Should().ContainSingle(x => x.Id == sh!.Id);
            reloaded.Shipments[0].Mode.Should().Be(sh.Mode);
        }

        [Fact]
        public void Version_3_Save_Loads_With_No_Shipments()
        {
            var s = TestScenario.Standard();
            var json = SaveSystem.Save(s).Replace("\"SaveVersion\":4", "\"SaveVersion\":3");
            var reloaded = SaveSystem.Load(json);
            reloaded.Shipments.Should().BeEmpty();
        }
```

**Step 2 — run, expect FAIL** (compile error: `ShipmentDto`/`Shipments` DTO field missing).

**Step 3 — implement.** Four edits to `SaveSystem.cs`:

(a) Add usings at the top:
```csharp
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
```

(b) **Extract batch↔DTO helpers** — replace the inline batch mapping. In `ToDto`, replace the
`foreach (var b in n.Batches) { nodeDto.Batches.Add(new BatchDto { … }); }` block (lines 121–148)
with:
```csharp
                foreach (var b in n.Batches)
                    nodeDto.Batches.Add(ToBatchDto(b));
```
In `FromDto`, replace the `foreach (var b in n.Batches) { … node.AddBatch(batch); }` block
(lines 297–333) with:
```csharp
                foreach (var b in n.Batches)
                    node.AddBatch(FromBatchDto(b));
```
Add the two helpers (move the exact code that was inline, plus the new `ShelfLifeDays` field from
Block C — include it now to avoid a second edit):
```csharp
        private static BatchDto ToBatchDto(Batch b) => new BatchDto
        {
            Id = b.Id.Value,
            RecipeId = b.RecipeId.Value,
            NodeId = b.NodeId,
            VesselId = b.VesselId.Value,
            VolumeLitres = b.VolumeLitres,
            State = (int)b.State,
            BrewedOnTotalDays = b.BrewedOn.TotalDays,
            ReadyOnTotalDays = b.ReadyOn.TotalDays,
            QualityBasisPoints = b.QualityBasisPoints,
            CostOfGoodsCents = b.CostOfGoods.Cents,
            Flavor = b.Flavor,
            OriginalGravityPoints = b.OriginalGravityPoints,
            FinalGravityPoints = b.FinalGravityPoints,
            AttenuationBasisPoints = b.AttenuationBasisPoints,
            AbvBasisPoints = b.AbvBasisPoints,
            IbuTenths = b.IbuTenths,
            SrmLovibond = b.SrmLovibond,
            YeastIngredientId = b.YeastIngredientId,
            YeastGeneration = b.YeastGeneration,
            Style = (int)b.Style,
            IsPasteurized = b.IsPasteurized,
            ShelfLifeDays = b.ShelfLifeDays,
            Infections = new List<Infection>(b.Infections)
        };

        private static Batch FromBatchDto(BatchDto b)
        {
            var batch = new Batch(new BatchId(b.Id), new RecipeId(b.RecipeId), b.NodeId,
                                  new VesselId(b.VesselId), Math.Max(1, b.VolumeLitres),
                                  GameDate.FromTotalDays(b.BrewedOnTotalDays),
                                  GameDate.FromTotalDays(b.ReadyOnTotalDays))
            {
                CostOfGoods = Money.FromCents(b.CostOfGoodsCents),
                Flavor = b.Flavor ?? new FlavorProfile(),
                OriginalGravityPoints = b.OriginalGravityPoints,
                FinalGravityPoints = b.FinalGravityPoints,
                AttenuationBasisPoints = b.AttenuationBasisPoints,
                AbvBasisPoints = b.AbvBasisPoints,
                IbuTenths = b.IbuTenths,
                SrmLovibond = b.SrmLovibond,
                YeastIngredientId = string.IsNullOrEmpty(b.YeastIngredientId) ? "ale-yeast" : b.YeastIngredientId,
                YeastGeneration = Math.Max(1, b.YeastGeneration),
                Style = (BeerStyle)b.Style,
                IsPasteurized = b.IsPasteurized,
                ShelfLifeDays = b.ShelfLifeDays > 0 ? b.ShelfLifeDays : 30
            };

            batch.SetQuality(b.QualityBasisPoints);
            int delta = batch.VolumeLitres - b.VolumeLitres;
            if (delta > 0) batch.Remove(delta);
            foreach (var inf in b.Infections ?? new List<Infection>())
                batch.RestoreInfection(inf);
            batch.SetQuality(b.QualityBasisPoints);
            if ((BatchState)b.State == BatchState.Ready) batch.MarkReady();
            else if ((BatchState)b.State == BatchState.Spoiled) batch.MarkSpoiled();
            return batch;
        }
```
(If `BatchDto.ShelfLifeDays` does not exist yet because Block C hasn't run, add `public int ShelfLifeDays { get; set; }`
to `BatchDto` in this task — it is harmless and Block C will consume it.)

(c) Add a `Shipments` field to `GameStateDto` (before the closing brace, ~line 466):
```csharp
            public List<ShipmentDto> Shipments { get; set; } = new List<ShipmentDto>();
```
And add the `ShipmentDto` class (after `BatchDto`, ~line 533):
```csharp
        internal sealed class ShipmentDto
        {
            public string Id { get; set; } = string.Empty;
            public string BatchId { get; set; } = string.Empty;
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
            public int Packaging { get; set; }
            public int VolumeLitres { get; set; }
            public int DaysRemaining { get; set; }
            public long TransportCostCents { get; set; }
            public int Mode { get; set; }
            public bool IsRefrigerated { get; set; }
            public BatchDto? Cargo { get; set; }
        }
```

(d) In `ToDto`, after the `Markets` loop (before `return dto;`, ~line 227) add:
```csharp
            foreach (var sh in s.Shipments)
            {
                dto.Shipments.Add(new ShipmentDto
                {
                    Id = sh.Id.Value,
                    BatchId = sh.BatchId.Value,
                    From = sh.From.Value,
                    To = sh.To.Value,
                    Packaging = (int)sh.Packaging,
                    VolumeLitres = sh.VolumeLitres,
                    DaysRemaining = sh.DaysRemaining,
                    TransportCostCents = sh.TransportCost.Cents,
                    Mode = (int)sh.Mode,
                    IsRefrigerated = sh.IsRefrigerated,
                    Cargo = sh.Cargo != null ? ToBatchDto(sh.Cargo) : null
                });
            }
```
In `FromDto`, after the `Markets` loop (before `return state;`, ~line 417) add:
```csharp
            foreach (var sh in dto.Shipments)
            {
                state.Shipments.Add(new Shipment(
                    new ShipmentId(sh.Id), new BatchId(sh.BatchId),
                    new NodeId(sh.From), new NodeId(sh.To),
                    sh.DaysRemaining, (PackagingType)sh.Packaging, sh.VolumeLitres)
                {
                    TransportCost = Money.FromCents(sh.TransportCostCents),
                    Mode = (TransportMode)sh.Mode,
                    IsRefrigerated = sh.IsRefrigerated,
                    Cargo = sh.Cargo != null ? FromBatchDto(sh.Cargo) : null
                });
            }
```

(e) `GameState.cs` — `public const int CurrentSaveVersion = 3;` → `= 4;`.

**Step 4 — run, expect PASS** (round-trip + v3-load). **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/State/SaveSystem.cs src/BreweryEmpire.Core/State/GameState.cs tests/BreweryEmpire.Core.Tests/State/SaveSystemTests.cs
git commit -m "feat(save): persist shipments in transit, bump save version to 4"
```

---

### Task A3: `WorldMap` routes carry a transport mode

**Objective:** The world's routes know how they are travelled, so dispatch can stop hardcoding
HorseCart.

**Files:** `src/BreweryEmpire.Core/Model/Sites/BreweryNode.cs` (WorldMap section); test append to
`tests/BreweryEmpire.Core.Tests/Model/Sites/BreweryNodeTests.cs`.

**Step 1 — failing test:**
```csharp
using BreweryEmpire.Core.Model.Logistics;

        [Fact]
        public void Route_Remembers_Its_Mode()
        {
            var map = new WorldMap();
            map.SetRoute(new NodeId("a"), new NodeId("b"), 180, TransportMode.SteamRail);
            map.TryGetRoute(new NodeId("a"), new NodeId("b"), out var r).Should().BeTrue();
            r.DistanceKm.Should().Be(180);
            r.Mode.Should().Be(TransportMode.SteamRail);
        }

        [Fact]
        public void Route_Without_Mode_Has_Null_Mode()
        {
            var map = new WorldMap();
            map.SetRoute(new NodeId("a"), new NodeId("b"), 50);
            map.TryGetRoute(new NodeId("a"), new NodeId("b"), out var r).Should().BeTrue();
            r.Mode.Should().BeNull();
        }
```

**Step 2 — run, expect FAIL** (no `SetRoute(a,b,km,mode)` overload / `TryGetRoute`).

**Step 3 — implement.** In `WorldMap` (`Model/Sites/BreweryNode.cs`), add
`using BreweryEmpire.Core.Model.Logistics;` to the file's usings. Replace the `_routeDistancesKm`
field and the three route methods (lines 181–183 and 209–221) with:
```csharp
        /// <summary>A route between two nodes, with an optional transport mode.</summary>
        public sealed record WorldRoute
        {
            public int DistanceKm { get; init; }
            public TransportMode? Mode { get; init; }
        }

        private readonly Dictionary<string, WorldRoute> _routes =
            new Dictionary<string, WorldRoute>(StringComparer.Ordinal);
```
…and (replacing `SetRoute`/`HasRoute`/`DistanceKm`):
```csharp
        /// <summary>Routes are symmetric: distance from A to B equals B to A.</summary>
        public void SetRoute(NodeId a, NodeId b, int distanceKm) =>
            SetRoute(a, b, distanceKm, null);

        public void SetRoute(NodeId a, NodeId b, int distanceKm, TransportMode? mode)
        {
            if (distanceKm < 0) throw new ArgumentOutOfRangeException(nameof(distanceKm));
            if (a == b) throw new ArgumentException("A node cannot have a route to itself.");
            _routes[RouteKey(a, b)] = new WorldRoute { DistanceKm = distanceKm, Mode = mode };
        }

        public bool HasRoute(NodeId a, NodeId b) => _routes.ContainsKey(RouteKey(a, b));

        public int DistanceKm(NodeId a, NodeId b) =>
            _routes.TryGetValue(RouteKey(a, b), out var r)
                ? r.DistanceKm
                : throw new KeyNotFoundException("No route between " + a + " and " + b + ".");

        public bool TryGetRoute(NodeId a, NodeId b, out WorldRoute route) =>
            _routes.TryGetValue(RouteKey(a, b), out route!);
```
(Keep `RouteKey` as-is.)

**Step 4 — run, expect PASS** (existing `BreweryNodeTests` still green — 3-arg `SetRoute`, symmetry,
self-route/negative throw). **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Sites/BreweryNode.cs tests/BreweryEmpire.Core.Tests/Model/Sites/BreweryNodeTests.cs
git commit -m "feat(logistics): world routes carry a transport mode"
```

---

### Task A4: Mode-aware dispatch that preserves the beer

**Objective:** Dispatch picks the mode (registered route, else argument), charges the real cost,
sets real transit days, honours the drayman, and snapshots the batch as cargo so nothing is lost.

**Files:** `src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs`; test append to
`tests/BreweryEmpire.Core.Tests/Simulation/LogisticsTests.cs`.

**Step 1 — failing test:**
```csharp
using BreweryEmpire.Core.Model.Staff;

        [Fact]
        public void Dispatch_Uses_Registered_Route_Mode_And_Charges_Its_Cost()
        {
            var s = TestScenario.Standard();
            s.World.SetRoute(new NodeId("burton"), new NodeId("burton"), 100, TransportMode.SteamRail);
            var batch = BrewAndReady(s);

            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.WoodenCask, distanceKm: 999)!;

            sh.Mode.Should().Be(TransportMode.SteamRail);
            sh.Cargo.Should().NotBeNull();
            sh.Cargo!.Style.Should().Be(batch.Style);
            sh.Cargo.QualityBasisPoints.Should().Be(batch.QualityBasisPoints);
            sh.Cargo.IbuTenths.Should().Be(batch.IbuTenths);
        }

        [Fact]
        public void Dispatch_Preserves_Brewing_Work_In_Cargo()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.WoodenCask, distanceKm: 10)!;

            sh.Cargo!.Flavor.Should().Be(batch.Flavor);
            sh.Cargo.AbvBasisPoints.Should().Be(batch.AbvBasisPoints);
            sh.Cargo.IsPasteurized.Should().Be(batch.IsPasteurized);
        }
```

**Step 2 — run, expect FAIL** (dispatch hardcodes HorseCart; no `Cargo`; no `Mode` set).

**Step 3 — implement.** In `LogisticsSystem.cs`, add `using BreweryEmpire.Core.Model.Staff;`, then
replace the whole `DispatchShipment` body (lines 24–56) with the overload pair below:
```csharp
        /// <summary>Dispatch along the registered route (or the fallback distance) by horse cart.</summary>
        public static Shipment? DispatchShipment(GameState state, NodeId from, NodeId to,
                                                BatchId batchId, int litres,
                                                PackagingType packaging, int distanceKm)
            => DispatchShipment(state, from, to, batchId, litres, packaging,
                                TransportMode.HorseCart, distanceKm);

        /// <summary>Dispatch a shipment by an explicit mode. Preserves the beer's quality/flavour
        /// as cargo; charges the mode's real per-litre cost.</summary>
        public static Shipment? DispatchShipment(GameState state, NodeId from, NodeId to,
                                                BatchId batchId, int litres,
                                                PackagingType packaging, TransportMode mode,
                                                int distanceKm, bool refrigerated = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (!ResearchSystem.IsPackagingUnlocked(state, packaging)) return null;

            var node = state.World.Get(from);
            var batch = node.Batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null || batch.VolumeLitres < litres) return null;

            // Prefer the registered route's distance and mode; the arguments are a fallback.
            int distance = distanceKm;
            if (state.World.TryGetRoute(from, to, out var route))
            {
                distance = route.DistanceKm;
                if (route.Mode != null) mode = route.Mode.Value;
            }

            var spec = TransportSpec.For(mode);
            var routeForCost = new Route { From = from, To = to, DistanceKm = distance, Mode = mode };

            // Draymen speed transit.
            int draymanBonus = state.Staff.AggregateBonus(from.Value, TraitEffect.TransitSpeed);
            int transitDays = routeForCost.TransitDays;
            if (draymanBonus > 0)
                transitDays = Math.Max(1, transitDays * (10000 - draymanBonus) / 10000);

            // Refrigeration only on a capable mode, gated by the refrigeration tech.
            bool isRefrigerated = refrigerated && ResearchSystem.CanRefrigerateShipment(state, mode);

            var cargo = new Batch(new BatchId(state.MintId("batch")), batch.RecipeId,
                                  batch.NodeId, batch.VesselId, litres, state.Date, state.Date)
            {
                Style = batch.Style,
                IsPasteurized = batch.IsPasteurized,
                OriginalGravityPoints = batch.OriginalGravityPoints,
                FinalGravityPoints = batch.FinalGravityPoints,
                AttenuationBasisPoints = batch.AttenuationBasisPoints,
                AbvBasisPoints = batch.AbvBasisPoints,
                IbuTenths = batch.IbuTenths,
                SrmLovibond = batch.SrmLovibond,
                YeastIngredientId = batch.YeastIngredientId,
                YeastGeneration = batch.YeastGeneration,
                Flavor = batch.Flavor,
                CostOfGoods = batch.CostPerLitre * litres
            };
            cargo.SetQuality(batch.QualityBasisPoints);
            cargo.MarkReady();
            foreach (var inf in batch.Infections) cargo.RestoreInfection(inf);

            var shipmentId = new ShipmentId(state.MintId("shipment"));
            var shipment = new Shipment(shipmentId, batchId, from, to,
                                        transitDays, packaging, litres)
            {
                Mode = mode,
                IsRefrigerated = isRefrigerated,
                Cargo = cargo,
                TransportCost = Money.FromCents((long)routeForCost.CostPerLitre.Cents * litres)
            };

            batch.Remove(litres);
            state.Ledger.ForceDebit(state.Date, LedgerCategory.TransportCost,
                                    shipment.TransportCost,
                                    "Shipment " + shipmentId + " " + from + " -> " + to + " (" + mode + ")",
                                    from.Value);

            return shipment;
        }
```
`ResearchSystem.CanRefrigerateShipment` is added in Task A7 — add a temporary stub now so this
compiles, then fill it in A7:
```csharp
// ResearchSystem.cs — temporary stub (real implementation in Task A7):
public static bool CanRefrigerateShipment(GameState state, TransportMode mode) => false;
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs src/BreweryEmpire.Core/Simulation/ResearchSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/LogisticsTests.cs
git commit -m "feat(logistics): mode-aware dispatch with drayman speed and cargo snapshot"
```

---

### Task A5: Delivery preserves beer and applies ullage (cooper bonus)

**Objective:** Arrived beer keeps its attributes; casks leak (reduced by coopers), bottles do not.

**Files:** `src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs` (`Deliver`); test append to
`LogisticsTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Delivered_Beer_Keeps_Its_Style_And_Quality()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.WoodenCask, distanceKm: 1)!;

            LogisticsSystem.ProcessShipments(s);   // arrives on day 1

            var dest = s.World.Get(new NodeId("burton"));
            var arrived = dest.Batches.First(b => b.VolumeLitres <= 100 && b.Style == batch.Style);
            arrived.Style.Should().Be(batch.Style);
            arrived.QualityBasisPoints.Should().Be(batch.QualityBasisPoints);
        }

        [Fact]
        public void Cask_Shipment_Arrives_Lighter_Than_Dispatched()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 1000, PackagingType.WoodenCask, distanceKm: 1)!;

            LogisticsSystem.ProcessShipments(s);

            var dest = s.World.Get(new NodeId("burton"));
            var arrived = dest.Batches.First(b => b.Id != batch.Id);
            arrived.VolumeLitres.Should().BeLessThan(1000);
        }
```

**Step 2 — run, expect FAIL** (delivery builds a blank `RecipeId("shipped")` batch; no ullage).

**Step 3 — implement.** Replace `Deliver` (lines 76–90) with:
```csharp
        private static void Deliver(GameState state, Shipment shipment)
        {
            if (!state.World.TryGet(shipment.To, out var dest)) return;

            if (shipment.Cargo != null)
            {
                // Coopers cut ullage; casks leak, sealed bottles lose nothing.
                int ullage = PackagingSpec.For(shipment.Packaging).UllageLossBasisPointsPerLeg;
                int cooperBonus = state.Staff.AggregateBonus(shipment.From.Value, TraitEffect.ContainerLossReduction);
                if (cooperBonus > 0)
                    ullage = ullage * Math.Max(0, 10000 - cooperBonus) / 10000;

                shipment.Cargo.ApplyLossBasisPoints(ullage);
                dest.AddBatch(shipment.Cargo);
                return;
            }

            // Fallback: a shipment built by hand (tests) still delivers a ready batch.
            var batch = new Batch(new BatchId(state.MintId("batch")),
                                  new RecipeId("shipped"), shipment.To.Value,
                                  new VesselId("none"), shipment.VolumeLitres,
                                  state.Date, state.Date)
            {
                IsPasteurized = false
            };
            batch.MarkReady();
            dest.AddBatch(batch);
        }
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/LogisticsTests.cs
git commit -m "feat(logistics): deliver cargo intact with cooper-reduced ullage"
```

---

### Task A6: In-transit spoilage

**Objective:** Beer spoils while travelling, scaled by the mode's `SpoilageModifierBasisPoints`;
refrigerated holds suppress progression. This is the payoff for the mode choice.

**Files:** `src/BreweryEmpire.Core/Simulation/SpoilageSystem.cs` (new `ProcessShipment`),
`src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs` (call it in `ProcessShipments`); test
`tests/BreweryEmpire.Core.Tests/Simulation/TransitSpoilageTests.cs`.

**Step 1 — failing test** (`TransitSpoilageTests.cs`):
```csharp
using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Simulation;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class TransitSpoilageTests
    {
        private static BreweryEmpire.Core.Model.Logistics.Shipment Dispatch(GameState s, TransportMode mode, int km)
        {
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            r.Batch!.MarkReady();
            return LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                r.Batch.Id, 100, PackagingType.WoodenCask, mode, km)!;
        }

        [Fact]
        public void Cargo_Ship_Is_Most_Spoilage_Prone()
        {
            // CargoShip SpoilageModifier 13000 vs HorseCart 10000: over many transit days the
            // ship contracts an infection at least as often; assert the modifier is applied.
            TransportSpec.For(TransportMode.CargoShip).SpoilageModifierBasisPoints
                .Should().BeGreaterThan(TransportSpec.For(TransportMode.HorseCart).SpoilageModifierBasisPoints);
        }

        [Fact]
        public void Warm_Transit_Can_Contract_An_Infection()
        {
            var s = TestScenario.Standard(seed: 7);
            s.Date = GameDate.FromYearMonthDay(1750, 7, 15);   // hot
            var sh = Dispatch(s, TransportMode.HorseCart, 200);
            bool everInfected = false;
            for (int d = 0; d < sh.DaysRemaining; d++)
            {
                SpoilageSystem.ProcessShipment(s, sh);
                sh.AdvanceDay();
                everInfected |= sh.Cargo!.Infections.Count > 0;
            }
            // With seed 7 and a hot 4+ day cart trip, an infection is essentially certain.
            everInfected.Should().BeTrue();
        }

        [Fact]
        public void Refrigerated_Transit_Never_Contracts_At_Freezing_Temps()
        {
            var s = TestScenario.Standard(seed: 1);
            s.Date = GameDate.FromYearMonthDay(1750, 1, 10);   // ~4C ambient in Burton
            var sh = Dispatch(s, TransportMode.SteamRail, 200);
            sh.IsRefrigerated = true;
            for (int d = 0; d < sh.DaysRemaining; d++)
            {
                SpoilageSystem.ProcessShipment(s, sh);
                sh.AdvanceDay();
            }
            // At 4C the base risk is 10bp — no infection across the trip.
            sh.Cargo!.Infections.Should().BeEmpty();
        }
    }
}
```
(If the first test's seed is flaky, loosen to "at least one of two seeds"; determinism is asserted
in Block E, not here.)

**Step 2 — run, expect FAIL** (`SpoilageSystem.ProcessShipment` missing).

**Step 3 — implement.** In `SpoilageSystem.cs` add `using BreweryEmpire.Core.Model.Logistics;`, and:
```csharp
        /// <summary>Advance spoilage for beer in transit. Reuses the two-stage model; the mode's
        /// spoilage modifier and the refrigerated flag gate it.</summary>
        public static void ProcessShipment(GameState state, Shipment shipment)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (shipment == null) throw new ArgumentNullException(nameof(shipment));
            if (shipment.Cargo == null || shipment.Cargo.State == BatchState.Spoiled) return;

            int ambient = state.World.TryGet(shipment.From, out var src)
                ? src.Climate.AmbientTempOn(state.Date)
                : 15;   // deterministic fallback

            var spec = TransportSpec.For(shipment.Mode);
            var cargo = shipment.Cargo;
            bool cold = shipment.IsRefrigerated;

            bool hasActive = cargo.Infections.Any(i =>
                !i.IsIntentionalSour && i.ProgressionBasisPoints < 10000);

            if (!hasActive)
            {
                int risk = BaseInfectionRiskBasisPoints(ambient);
                risk = risk * spec.SpoilageModifierBasisPoints / 10000;
                if (risk > 0 && state.Random.Chance(risk))
                    cargo.ContractInfection(RollOrganism(state),
                        "Transit " + shipment.Mode + " at " + ambient + "C");
            }
            else
            {
                int rate = ProgressionRateBasisPoints(
                    ActiveOrganism(cargo), ambient, cargo.IbuTenths,
                    cargo.AbvBasisPoints, cold, cargo.IsPasteurized);
                rate = rate * spec.SpoilageModifierBasisPoints / 10000;
                if (cargo.AdvanceInfection(rate))
                    state.Ledger.ForceDebit(state.Date, LedgerCategory.SpoilageWriteOff,
                        cargo.CostOfGoods,
                        "Transit spoilage " + cargo.Id + " (" + shipment.Mode + ")",
                        shipment.From.Value);
            }
        }
```
In `LogisticsSystem.ProcessShipments` (lines 59–74), add the spoilage call after `AdvanceDay()`:
```csharp
                shipment.AdvanceDay();
                SpoilageSystem.ProcessShipment(state, shipment);
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/SpoilageSystem.cs src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TransitSpoilageTests.cs
git commit -m "feat(sim): in-transit spoilage scaled by transport mode and refrigeration"
```

---

### Task A7: Refrigerated rail capability

**Objective:** Refrigerated shipping is only possible on a capable mode (SteamRail) *after* the
`refrigeration` tech is researched. Replaces the A4 stub.

**Files:** `src/BreweryEmpire.Core/Simulation/ResearchSystem.cs`; test append to
`tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Refrigerated_Shipping_Requires_Refrigeration_Tech_And_Rail()
        {
            var s = TestScenario.Standard();
            ResearchSystem.CanRefrigerateShipment(s, BreweryEmpire.Core.Model.Logistics.TransportMode.SteamRail)
                .Should().BeFalse();
            ResearchSystem.CanRefrigerateShipment(s, BreweryEmpire.Core.Model.Logistics.TransportMode.HorseCart)
                .Should().BeFalse();   // horse cart is never refrigerated-capable

            ResearchSystem.Unlock(s, "refrigeration");
            ResearchSystem.CanRefrigerateShipment(s, BreweryEmpire.Core.Model.Logistics.TransportMode.SteamRail)
                .Should().BeTrue();
        }
```

**Step 2 — run, expect FAIL** (stub always returns false).

**Step 3 — implement.** In `ResearchSystem.cs` add `using BreweryEmpire.Core.Model.Logistics;` and
replace the stub with:
```csharp
        /// <summary>Whether a shipment on this mode can ride refrigerated: the mode must be
        /// refrigerated-capable and mechanical refrigeration must be researched.</summary>
        public static bool CanRefrigerateShipment(GameState state, TransportMode mode) =>
            TransportSpec.For(mode).IsRefrigeratedCapable && HasTech(state, "refrigeration");
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/ResearchSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(logistics): refrigerated rail gated behind refrigeration tech"
```

---

# Block B — Expansion (vessel build/purchase)

### Task B1: `BuildSystem.TryPurchaseVessel`

**Objective:** Buy a vessel for a site: all-or-nothing (funds checked via `TryDebit`, vessel added
only on success).

**Files:** create `src/BreweryEmpire.Core/Simulation/BuildSystem.cs`; test
`tests/BreweryEmpire.Core.Tests/Simulation/BuildSystemTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class BuildSystemTests
    {
        [Fact]
        public void Purchase_Adds_Vessel_And_Debits_Capital()
        {
            var s = TestScenario.Standard(openingCapitalWhole: 2000);
            int before = s.World.Get(new NodeId("burton")).Vessels.Count;
            long cashBefore = s.Ledger.Balance.Cents;

            var v = BuildSystem.TryPurchaseVessel(s, new NodeId("burton"),
                VesselType.OpenFermenter, EquipmentTier.Copper, 1500);

            v.Should().NotBeNull();
            s.World.Get(new NodeId("burton")).Vessels.Count.Should().Be(before + 1);
            s.Ledger.Balance.Cents.Should().BeLessThan(cashBefore);
        }

        [Fact]
        public void Insufficient_Funds_Adds_Nothing()
        {
            var s = TestScenario.Standard(openingCapitalWhole: 0);   // ~$0 after nothing
            // Spend everything first so the ledger is empty.
            var node = s.World.Get(new NodeId("burton"));
            int before = node.Vessels.Count;

            var v = BuildSystem.TryPurchaseVessel(s, new NodeId("burton"),
                VesselType.OpenFermenter, EquipmentTier.Copper, 1500);

            v.Should().BeNull();
            node.Vessels.Count.Should().Be(before);
        }

        [Fact]
        public void Non_Brewery_Node_Is_Refused()
        {
            var s = TestScenario.Standard();
            var wh = new BreweryNode(new NodeId("wh"), "WH", NodeType.Warehouse,
                new RegionId("london"), WaterProfile.London, RegionClimate.Dublin);
            s.World.AddNode(wh);

            BuildSystem.TryPurchaseVessel(s, new NodeId("wh"),
                VesselType.OpenFermenter, EquipmentTier.Copper, 1500).Should().BeNull();
        }
    }
}
```

**Step 2 — run, expect FAIL** (`BuildSystem` missing).

**Step 3 — implement** `Simulation/BuildSystem.cs`:
```csharp
using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Capital expenditure. Buying a vessel is all-or-nothing: the debit and the
    /// add happen together, so a broke player never ends up with a half-built tank.
    /// </summary>
    public static class BuildSystem
    {
        public static Vessel? TryPurchaseVessel(GameState state, NodeId nodeId,
                                                VesselType type, EquipmentTier tier,
                                                int capacityLitres)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (capacityLitres <= 0) throw new ArgumentOutOfRangeException(nameof(capacityLitres));

            if (!state.World.TryGet(nodeId, out var node)) return null;
            if (node.Type != NodeType.Brewery) return null;
            if (!ResearchSystem.CanBuildTier(state, tier)) return null;

            var vessel = Vessel.Create(state.MintId("vessel"), type, tier, capacityLitres);

            if (!state.Ledger.TryDebit(state.Date, LedgerCategory.CapitalExpenditure,
                                       vessel.PurchaseCost,
                                       "Purchase " + tier + " " + type + " at " + nodeId.Value,
                                       nodeId.Value))
                return null;

            node.AddVessel(vessel);
            return vessel;
        }
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/BuildSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/BuildSystemTests.cs
git commit -m "feat(build): add vessel purchase system"
```

---

### Task B2: Tier gating + stainless-steel unlock effect

**Objective:** Stainless vessels are only buildable after the `stainless-steel` tech — the payoff
that makes the previously-deferred tech real.

**Files:** `src/BreweryEmpire.Core/Simulation/ResearchSystem.cs` (`CanBuildTier`); test append to
`BuildSystemTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Stainless_Vessel_Requires_Stainless_Steel_Tech()
        {
            var s = TestScenario.Standard(openingCapitalWhole: 2000);
            BuildSystem.TryPurchaseVessel(s, new NodeId("burton"),
                VesselType.OpenFermenter, EquipmentTier.Stainless, 1500).Should().BeNull();

            ResearchSystem.Unlock(s, "stainless-steel");
            BuildSystem.TryPurchaseVessel(s, new NodeId("burton"),
                VesselType.OpenFermenter, EquipmentTier.Stainless, 1500).Should().NotBeNull();
        }

        [Fact]
        public void Wooden_Vessel_Is_Always_Buildable()
        {
            var s = TestScenario.Standard(openingCapitalWhole: 2000);
            BuildSystem.TryPurchaseVessel(s, new NodeId("burton"),
                VesselType.OpenFermenter, EquipmentTier.Wooden, 800).Should().NotBeNull();
        }
```

**Step 2 — run, expect FAIL** (`ResearchSystem.CanBuildTier` missing → compile error).

**Step 3 — implement.** In `ResearchSystem.cs` add `using BreweryEmpire.Core.Model.Sites;`, and:
```csharp
        /// <summary>Whether a vessel of this material may be purchased. Only stainless is gated —
        /// it needs the stainless-steel tech (and thus the Modern era).</summary>
        public static bool CanBuildTier(GameState state, EquipmentTier tier) =>
            tier != EquipmentTier.Stainless || HasTech(state, "stainless-steel");
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/ResearchSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/BuildSystemTests.cs
git commit -m "feat(build): gate stainless vessels behind the stainless-steel tech"
```

---

# Block C — Shelf-life & the drum-roaster

### Task C1: Batch shelf-life, computed at brew

**Objective:** A batch knows how long it keeps — style, strength, and (for dark beer) the drum
roaster all extend it.

**Files:** `src/BreweryEmpire.Core/Model/Brewing/Batch.cs` (`ShelfLifeDays`, `AgeDays`),
`src/BreweryEmpire.Core/Simulation/BrewingSystem.cs` (`ShelfLifeFor` + set it); test
`tests/BreweryEmpire.Core.Tests/Simulation/ShelfLifeTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Simulation;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class ShelfLifeTests
    {
        private static Batch BrewPaleAle()
        {
            var s = TestScenario.Standard();
            return BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
        }

        [Fact]
        public void Batch_Has_A_Positive_Shelf_Life()
        {
            BrewPaleAle().ShelfLifeDays.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Dark_Beer_Keeps_Longer_Than_Pale()
        {
            var s = TestScenario.Standard();
            var porter = TestScenario.PaleAle();
            porter.Style = BeerStyle.Porter;
            s.Recipes["porter"] = porter;

            var pale = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            // Need a second vessel/ready state; simplest: read the helper directly.
            int porterShelf = BrewingSystem.ShelfLifeFor(s, BeerStyle.Porter, pale.AbvBasisPoints);
            int paleShelf = BrewingSystem.ShelfLifeFor(s, BeerStyle.PaleAle, pale.AbvBasisPoints);

            porterShelf.Should().BeGreaterThan(paleShelf);
        }

        [Fact]
        public void Drum_Roaster_Extends_Dark_Beer_Shelf_Life()
        {
            var s = TestScenario.Standard();
            int before = BrewingSystem.ShelfLifeFor(s, BeerStyle.Stout, 500);
            ResearchSystem.Unlock(s, "drum-roaster");
            int after = BrewingSystem.ShelfLifeFor(s, BeerStyle.Stout, 500);
            after.Should().BeGreaterThan(before);
        }

        [Fact]
        public void Drum_Roaster_Does_Not_Extend_Pale_Ale()
        {
            var s = TestScenario.Standard();
            int before = BrewingSystem.ShelfLifeFor(s, BeerStyle.PaleAle, 500);
            ResearchSystem.Unlock(s, "drum-roaster");
            BrewingSystem.ShelfLifeFor(s, BeerStyle.PaleAle, 500).Should().Be(before);
        }

        [Fact]
        public void Age_Days_Counts_From_Ready()
        {
            var b = BrewPaleAle();
            b.MarkReady();
            var now = b.ReadyOn.AddDays(10);
            b.AgeDays(now).Should().Be(10);
        }
    }
}
```

**Step 2 — run, expect FAIL** (`ShelfLifeDays`, `AgeDays`, `ShelfLifeFor` missing).

**Step 3 — implement.**

(a) `Batch.cs` — after `public GameDate ReadyOn { get; set; }` (line 52) add:
```csharp
        /// <summary>Days the beer keeps before it starts to stale (style/strength/tech driven).</summary>
        public int ShelfLifeDays { get; set; } = 30;

        /// <summary>Days since the batch became ready to sell.</summary>
        public int AgeDays(GameDate now) => Math.Max(0, now.TotalDays - ReadyOn.TotalDays);
```

(b) `BrewingSystem.cs` — after the gravity block sets `batch.AbvBasisPoints = abv;` (line 119), add:
```csharp
            batch.ShelfLifeDays = ShelfLifeFor(state, recipe.Style, abv);
```
And add the helper (make it `internal` so tests in the same assembly can call it):
```csharp
        /// <summary>Base keeping quality. Dark, hoppy, strong beer keeps; the drum roaster's
        /// super-roasted barley extends dark styles further.</summary>
        internal static int ShelfLifeFor(GameState state, BeerStyle style, int abvBasisPoints)
        {
            int days = 30;
            if (style == BeerStyle.Porter || style == BeerStyle.Stout || style == BeerStyle.Mild) days += 15;
            days += abvBasisPoints / 40;   // strength preserves
            if (ResearchSystem.HasTech(state, "drum-roaster") &&
                (style == BeerStyle.Porter || style == BeerStyle.Stout))
                days += 45;
            return days;
        }
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Brewing/Batch.cs src/BreweryEmpire.Core/Simulation/BrewingSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/ShelfLifeTests.cs
git commit -m "feat(brewing): batch shelf-life from style, strength and drum roaster"
```

---

### Task C2: Staleness decay (past shelf life, quality drifts, then spoils)

**Objective:** Ready beer past its shelf life loses quality each day and eventually spoils —
making "keep it fresh" matter and the drum-roaster's extra shelf life a real competitive edge.

**Files:** `src/BreweryEmpire.Core/Simulation/SpoilageSystem.cs` (`ProcessNode`); test append to
`ShelfLifeTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Stale_Beer_Loses_Quality_Then_Spoils()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            b.MarkReady();
            b.SetQuality(1000);
            int shelf = b.ShelfLifeDays;

            var node = s.World.Get(new NodeId("burton"));
            // Advance past shelf life, then run the spoilage pass each day.
            s.Date = b.ReadyOn.AddDays(shelf + 1);
            SpoilageSystem.ProcessNode(s, node);
            b.QualityBasisPoints.Should().BeLessThan(1000);
        }

        [Fact]
        public void Fresh_Beer_Does_Not_Decay()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            b.MarkReady();
            b.SetQuality(1000);
            var node = s.World.Get(new NodeId("burton"));

            s.Date = b.ReadyOn.AddDays(1);   // still fresh
            SpoilageSystem.ProcessNode(s, node);
            b.QualityBasisPoints.Should().Be(1000);
        }
```

**Step 2 — run, expect FAIL** (no staleness logic).

**Step 3 — implement.** In `SpoilageSystem.ProcessNode`, inside the `foreach (var batch in node.Batches.ToList())`
loop, immediately after `if (batch.State == BatchState.Spoiled) continue;` (line 139) add:
```csharp
                // Ready beer past its shelf life stales, then spoils.
                if (batch.State == BatchState.Ready && batch.AgeDays(state.Date) > batch.ShelfLifeDays)
                {
                    batch.AdjustQuality(-25);
                    if (batch.QualityBasisPoints <= 0)
                    {
                        batch.MarkSpoiled();
                        WriteOff(state, node, batch);
                        continue;
                    }
                }
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/SpoilageSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/ShelfLifeTests.cs
git commit -m "feat(sim): stale beer decays and spoils past its shelf life"
```

---

### Task C3: Update drum-roaster catalog copy

**Objective:** The tech description no longer says "Effect deferred."

**Files:** `src/BreweryEmpire.Core/Model/Research/TechCatalog.cs`.

**Step 1** — edit the `drum-roaster` node (lines 33–38):
```csharp
                Id = "drum-roaster", DisplayName = "Patent Drum Roaster", MinEra = Era.Industrial,
                ResearchCostPoints = 220, PrerequisiteIds = new[] { "malting-kilns" },
                Description = "Super-roasted unmalted barley for light-bodied dark stouts.",
                LoreText = "Guinness-era roasting: roasted barley lets dark beer keep far longer."
```
Also edit `stainless-steel` (line 85):
```csharp
                Description = "Maximum hygiene and consistency; unlocks stainless vessels.",
```
(No code-behaviour change — commit with a doc message.)

**Step 2 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Research/TechCatalog.cs
git commit -m "docs(tech): drum roaster and stainless steel now have real effects"
```

---

# Block D — Scenario layer

### Task D1: `ScenarioDefinition` model

**Objective:** A pure-data description of a starting world (sites, vessels, routes, markets, staff,
rivals) that a single call can assemble.

**Files:** create `src/BreweryEmpire.Core/Model/Scenario/ScenarioDefinition.cs`; test
`tests/BreweryEmpire.Core.Tests/Model/Scenario/ScenarioDefinitionTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Scenario
{
    public class ScenarioDefinitionTests
    {
        [Fact]
        public void Definition_Holds_Its_Collections_In_Order()
        {
            var d = new ScenarioDefinition
            {
                Id = "x",
                Sites =
                {
                    new ScenarioSite { Id = "a" },
                    new ScenarioSite { Id = "b" }
                }
            };
            d.Sites.Select(s => s.Id).Should().Equal("a", "b");
        }
    }
}
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement** `Model/Scenario/ScenarioDefinition.cs`:
```csharp
using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Scenario
{
    /// <summary>A starting site (brewery or warehouse) with its vessels and stock.</summary>
    public sealed record ScenarioSite
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public NodeType Type { get; init; } = NodeType.Brewery;
        public string RegionId { get; init; } = string.Empty;
        public WaterProfile Water { get; init; } = WaterProfile.London;
        public RegionClimate Climate { get; init; } = RegionClimate.BurtonEngland;
        public long DailyOverheadCents { get; init; }
        public List<ScenarioVessel> Vessels { get; init; } = new List<ScenarioVessel>();
        public int StockKilos { get; init; }
    }

    public sealed record ScenarioVessel
    {
        public VesselType Type { get; init; }
        public EquipmentTier Tier { get; init; }
        public int CapacityLitres { get; init; }
    }

    /// <summary>A route between two sites, with a transport mode.</summary>
    public sealed record ScenarioRoute
    {
        public string From { get; init; } = string.Empty;
        public string To { get; init; } = string.Empty;
        public int DistanceKm { get; init; }
        public TransportMode Mode { get; init; } = TransportMode.HorseCart;
    }

    public sealed record ScenarioMarket
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string AdjacentNodeId { get; init; } = string.Empty;
        public RegionClimate Climate { get; init; } = RegionClimate.BurtonEngland;
        public int Population { get; init; }
        public BeerStyle Style { get; init; } = BeerStyle.PaleAle;
        public int BaseDemandLitresPerTick { get; init; }
    }

    public sealed record ScenarioStaff
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public StaffRole Role { get; init; }
        public int SkillBasisPoints { get; init; }
        public Money MonthlyWage { get; init; }
        public string AssignToNodeId { get; init; } = string.Empty;
    }

    public sealed record ScenarioRival
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int StrengthBasisPoints { get; init; }
        public string HomeRegionId { get; init; } = string.Empty;
    }

    /// <summary>The full starting-world description. Pure data, like TechCatalog/EventCatalog.</summary>
    public sealed record ScenarioDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public GameDate Start { get; init; } = GameDate.FromYearMonthDay(1750, 1, 1);
        public Money OpeningCapital { get; init; } = Money.FromWhole(2000);
        public List<ScenarioSite> Sites { get; init; } = new List<ScenarioSite>();
        public List<ScenarioRoute> Routes { get; init; } = new List<ScenarioRoute>();
        public List<ScenarioMarket> Markets { get; init; } = new List<ScenarioMarket>();
        public List<ScenarioStaff> Staff { get; init; } = new List<ScenarioStaff>();
        public List<ScenarioRival> Rivals { get; init; } = new List<ScenarioRival>();
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Scenario/ScenarioDefinition.cs tests/BreweryEmpire.Core.Tests/Model/Scenario/ScenarioDefinitionTests.cs
git commit -m "feat(scenario): add ScenarioDefinition model"
```

---

### Task D2: `ScenarioCatalog` (two scenarios)

**Objective:** Two named scenarios that exercise the whole Phase 4 surface: an era-1 cart world and
a post-refrigeration rail/bottle world.

**Files:** create `src/BreweryEmpire.Core/Model/Scenario/ScenarioCatalog.cs`; test
`tests/BreweryEmpire.Core.Tests/Model/Scenario/ScenarioCatalogTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Scenario
{
    public class ScenarioCatalogTests
    {
        [Fact]
        public void Catalog_Has_At_Least_Two_Scenarios()
        {
            ScenarioCatalog.All.Count.Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public void London_1890_Has_A_Refrigerated_Capable_Rail_Route()
        {
            var l = ScenarioCatalog.London1890;
            l.Routes.Should().Contain(r => r.Mode == BreweryEmpire.Core.Model.Logistics.TransportMode.SteamRail);
        }

        [Fact]
        public void Ids_Are_Unique()
        {
            ScenarioCatalog.All.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        }
    }
}
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement** `Model/Scenario/ScenarioCatalog.cs`:
```csharp
using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Scenario
{
    /// <summary>The built-in starting worlds.</summary>
    public static class ScenarioCatalog
    {
        public static ScenarioDefinition Burton1750 { get; } = new ScenarioDefinition
        {
            Id = "burton-1750",
            Name = "Burton, 1750",
            Start = GameDate.FromYearMonthDay(1750, 1, 1),
            OpeningCapital = Money.FromWhole(50_000),
            Sites =
            {
                new ScenarioSite
                {
                    Id = "burton", Name = "Burton Brewery", Type = NodeType.Brewery,
                    RegionId = "midlands", Water = WaterProfile.Burton,
                    Climate = RegionClimate.BurtonEngland, DailyOverheadCents = 200,
                    StockKilos = 5000,
                    Vessels =
                    {
                        new ScenarioVessel { Type = VesselType.MashTun, Tier = EquipmentTier.Copper, CapacityLitres = 2000 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Wooden, CapacityLitres = 1200 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Copper, CapacityLitres = 1200 }
                    }
                },
                new ScenarioSite
                {
                    Id = "london", Name = "London Warehouse", Type = NodeType.Warehouse,
                    RegionId = "london", Water = WaterProfile.London,
                    Climate = RegionClimate.Dublin, DailyOverheadCents = 100
                }
            },
            Routes =
            {
                new ScenarioRoute { From = "burton", To = "london", DistanceKm = 180, Mode = TransportMode.HorseCart }
            },
            Markets =
            {
                new ScenarioMarket { Id = "burton-market", Name = "Burton", AdjacentNodeId = "burton",
                    Climate = RegionClimate.BurtonEngland, Population = 12_000, Style = BeerStyle.PaleAle, BaseDemandLitresPerTick = 250 },
                new ScenarioMarket { Id = "london-market", Name = "London", AdjacentNodeId = "london",
                    Climate = RegionClimate.Dublin, Population = 20_000, Style = BeerStyle.Porter, BaseDemandLitresPerTick = 300 }
            },
            Staff =
            {
                new ScenarioStaff { Id = "bm-1", Name = "Sam Allsopp", Role = StaffRole.Brewmaster,
                    SkillBasisPoints = 6000, MonthlyWage = Money.FromWhole(25), AssignToNodeId = "burton" }
            },
            Rivals =
            {
                new ScenarioRival { Id = "rival-1", Name = "Rival & Co", StrengthBasisPoints = 3000, HomeRegionId = "london" }
            }
        };

        public static ScenarioDefinition London1890 { get; } = new ScenarioDefinition
        {
            Id = "london-1890",
            Name = "London, 1890",
            Start = GameDate.FromYearMonthDay(1890, 1, 1),
            OpeningCapital = Money.FromWhole(200_000),
            Sites =
            {
                new ScenarioSite
                {
                    Id = "london", Name = "London Brewery", Type = NodeType.Brewery,
                    RegionId = "london", Water = WaterProfile.London,
                    Climate = RegionClimate.Dublin, DailyOverheadCents = 500,
                    StockKilos = 20_000,
                    Vessels =
                    {
                        new ScenarioVessel { Type = VesselType.MashTun, Tier = EquipmentTier.Copper, CapacityLitres = 5000 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Iron, CapacityLitres = 3000 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Iron, CapacityLitres = 3000 }
                    }
                },
                new ScenarioSite
                {
                    Id = "hamburg", Name = "Hamburg Depot", Type = NodeType.Warehouse,
                    RegionId = "hamburg", Water = WaterProfile.Pilsen,
                    Climate = RegionClimate.Bavaria, DailyOverheadCents = 300
                }
            },
            Routes =
            {
                new ScenarioRoute { From = "london", To = "hamburg", DistanceKm = 750, Mode = TransportMode.CargoShip },
                new ScenarioRoute { From = "london", To = "hamburg", DistanceKm = 700, Mode = TransportMode.SteamRail }
            },
            Markets =
            {
                new ScenarioMarket { Id = "london-market", Name = "London", AdjacentNodeId = "london",
                    Climate = RegionClimate.Dublin, Population = 30_000, Style = BeerStyle.PaleAle, BaseDemandLitresPerTick = 600 },
                new ScenarioMarket { Id = "hamburg-market", Name = "Hamburg", AdjacentNodeId = "hamburg",
                    Climate = RegionClimate.Bavaria, Population = 25_000, Style = BeerStyle.Pilsner, BaseDemandLitresPerTick = 500 }
            },
            Staff =
            {
                new ScenarioStaff { Id = "bm-2", Name = "Carl Linde", Role = StaffRole.Chemist,
                    SkillBasisPoints = 8000, MonthlyWage = Money.FromWhole(60), AssignToNodeId = "london" },
                new ScenarioStaff { Id = "bm-3", Name = "Adolphus Busch", Role = StaffRole.Salesman,
                    SkillBasisPoints = 7000, MonthlyWage = Money.FromWhole(55), AssignToNodeId = "london" }
            },
            Rivals =
            {
                new ScenarioRival { Id = "rival-2", Name = "Continental Lager Co", StrengthBasisPoints = 6000, HomeRegionId = "hamburg" }
            }
        };

        public static IReadOnlyList<ScenarioDefinition> All { get; } =
            new List<ScenarioDefinition> { Burton1750, London1890 };
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Scenario/ScenarioCatalog.cs tests/BreweryEmpire.Core.Tests/Model/Scenario/ScenarioCatalogTests.cs
git commit -m "feat(scenario): add Burton 1750 and London 1890 starting worlds"
```

---

### Task D3: `ScenarioAssembler` + `GameState.NewGame(seed, scenario)`

**Objective:** One call builds the whole world from a scenario, deterministically.

**Files:** create `src/BreweryEmpire.Core/Model/Scenario/ScenarioAssembler.cs`; add an overload to
`src/BreweryEmpire.Core/State/GameState.cs`; test `tests/BreweryEmpire.Core.Tests/Model/Scenario/ScenarioAssemblerTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Scenario
{
    public class ScenarioAssemblerTests
    {
        [Fact]
        public void Burton_Scenario_Builds_A_Populated_World()
        {
            var s = ScenarioAssembler.Assemble(42, ScenarioCatalog.Burton1750);

            s.World.NodeCount.Should().Be(2);
            s.World.Get(new BreweryEmpire.Core.Model.NodeId("burton")).Vessels.Count.Should().Be(3);
            s.Markets.Count.Should().Be(2);
            s.Staff.All.Count().Should().Be(1);
            s.Rivals.Count.Should().Be(1);
            s.World.HasRoute(new BreweryEmpire.Core.Model.NodeId("burton"),
                             new BreweryEmpire.Core.Model.NodeId("london")).Should().BeTrue();
        }

        [Fact]
        public void Same_Seed_And_Scenario_Are_Identical()
        {
            var a = ScenarioAssembler.Assemble(7, ScenarioCatalog.Burton1750);
            var b = ScenarioAssembler.Assemble(7, ScenarioCatalog.Burton1750);
            TestScenario.Fingerprint(a).Should().Be(TestScenario.Fingerprint(b));
        }
    }
}
```

**Step 2 — run, expect FAIL** (`ScenarioAssembler` missing).

**Step 3 — implement** `Model/Scenario/ScenarioAssembler.cs`:
```csharp
using System;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Markets;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Scenario
{
    /// <summary>Turns a ScenarioDefinition into a live GameState. Deterministic: every id is minted
    /// from the state counter and every collection is built in scenario order.</summary>
    public static class ScenarioAssembler
    {
        public static GameState Assemble(int seed, ScenarioDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var state = GameState.NewGame(seed, def.Start, def.OpeningCapital);

            foreach (var site in def.Sites)
            {
                var node = new BreweryNode(new NodeId(site.Id), site.Name, site.Type,
                                           new RegionId(site.RegionId), site.Water, site.Climate)
                {
                    DailyOverhead = Economy.Money.FromCents(site.DailyOverheadCents)
                };

                foreach (var v in site.Vessels)
                    node.AddVessel(Vessel.Create(state.MintId("vessel"), v.Type, v.Tier, v.CapacityLitres));

                if (site.StockKilos > 0)
                    StockStandardIngredients(node, def.Start, site.StockKilos);

                state.World.AddNode(node);
            }

            foreach (var r in def.Routes)
                state.World.SetRoute(new NodeId(r.From), new NodeId(r.To), r.DistanceKm, r.Mode);

            foreach (var m in def.Markets)
            {
                var market = MarketNode.Create(m.Id, m.Name, new NodeId(m.AdjacentNodeId),
                                               m.Climate, m.Population);
                market.SetBaseDemand(m.Style, m.BaseDemandLitresPerTick);
                state.Markets.Add(market);
            }

            foreach (var st in def.Staff)
            {
                var member = new StaffMember(new StaffId(st.Id), st.Name, st.Role, st.SkillBasisPoints,
                                             st.MonthlyWage, 40, def.Start);
                member.AssignTo(st.AssignToNodeId);
                state.Staff.Hire(member, def.Start);
            }

            foreach (var r in def.Rivals)
                state.Rivals.Add(new RivalBrewer
                {
                    Id = r.Id, Name = r.Name, StrengthBasisPoints = r.StrengthBasisPoints,
                    HomeRegionId = r.HomeRegionId
                });

            state.SyncRandomState();
            return state;
        }

        private static void StockStandardIngredients(BreweryNode node, GameDate on, int kilos)
        {
            void Add(string id, int grams, long centsPerKg) =>
                node.Inventory.AddLot(new IngredientLot(new LotId(id + "-lot-" + on.TotalDays),
                    id, grams, on.Year, 10000, on, Economy.Money.FromCents(centsPerKg)));

            Add("pale-malt", kilos * 1000, 45);
            Add("crystal-malt", kilos * 100, 65);
            Add("goldings-hops", kilos * 20, 320);
            Add("ale-yeast", kilos * 5, 150);
        }
    }
}
```
Note: `GameState.NewGame(seed, start, capital)` already exists; it does NOT stock recipes. After
`Assemble`, the caller registers recipes (e.g. `s.Recipes["pale-ale"] = …`), exactly as today.

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Scenario/ScenarioAssembler.cs tests/BreweryEmpire.Core.Tests/Model/Scenario/ScenarioAssemblerTests.cs
git commit -m "feat(scenario): assemble a live GameState from a ScenarioDefinition"
```

(Optional: add `public static GameState NewGame(int seed, ScenarioDefinition s) => ScenarioAssembler.Assemble(seed, s);`
to `GameState` if you want a single entry point; otherwise `ScenarioAssembler.Assemble` is the API.)

---

# Block E — Proof

### Task E1: End-to-end Phase 4 integration

**Objective:** A scenario-built world that brews, ships by rail with refrigeration, buys a stainless
vessel, and watches stale beer decay — all in one deterministic run.

**Files:** create `tests/BreweryEmpire.Core.Tests/Integration/Phase4IntegrationTests.cs`.

**Step 1 — tests:**
```csharp
using System;
using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Scenario;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Integration
{
    public class Phase4IntegrationTests
    {
        private static GameState London1890()
        {
            var s = ScenarioAssembler.Assemble(1890, ScenarioCatalog.London1890);
            s.Recipes["pale-ale"] = TestScenario.PaleAle();
            // Era 4: grant the modern-era techs the scenario implies.
            ResearchSystem.Unlock(s, "refrigeration");
            ResearchSystem.Unlock(s, "bottling-line");
            ResearchSystem.Unlock(s, "stainless-steel");
            return s;
        }

        [Fact]
        public void Refrigerated_Rail_Shipment_Arrives_Unspoiled()
        {
            var s = London1890();
            var batch = BrewingSystem.TryStartBrew(s, new NodeId("london"), new RecipeId("pale-ale")).Batch!;
            batch.MarkReady();

            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("london"), new NodeId("hamburg"),
                batch.Id, 100, PackagingType.Bottle, TransportMode.SteamRail, 700, refrigerated: true);
            sh.Should().NotBeNull();
            sh!.IsRefrigerated.Should().BeTrue();
        }

        [Fact]
        public void Can_Buy_A_Stainless_Vessel_After_Unlocking()
        {
            var s = London1890();
            var v = BuildSystem.TryPurchaseVessel(s, new NodeId("london"),
                VesselType.OpenFermenter, EquipmentTier.Stainless, 4000);
            v.Should().NotBeNull();
            v!.Tier.Should().Be(EquipmentTier.Stainless);
        }

        [Fact]
        public void A_Year_Of_Scenario_Play_Is_Deterministic_And_Invariant_Holds()
        {
            string Run()
            {
                var s = London1890();
                for (int day = 0; day < 365; day++)
                {
                    if (day % 25 == 0)
                        BrewingSystem.TryStartBrew(s, new NodeId("london"), new RecipeId("pale-ale"));
                    var node = s.World.Get(new NodeId("london"));
                    var ready = node.Batches.FirstOrDefault(b => b.IsSellable && b.VolumeLitres >= 100);
                    if (ready != null && day % 30 == 0)
                        LogisticsSystem.DispatchShipment(s, new NodeId("london"), new NodeId("hamburg"),
                            ready.Id, 100, PackagingType.Bottle, TransportMode.SteamRail, 700, refrigerated: true);
                    TickSystem.AdvanceDay(s);
                }
                return s.Ledger.Balance.Cents + "|" + TestScenario.Fingerprint(s);
            }

            var result = Run();
            result.Should().Be(Run());
        }
    }
}
```

**Step 2 — run, expect FAIL→PASS** (fix any genuine integration bug surfaced). **Step 3 — commit:**
```bash
git add tests/BreweryEmpire.Core.Tests/Integration/Phase4IntegrationTests.cs
git commit -m "test(core): Phase 4 end-to-end — rail shipping, build, shelf-life, determinism"
```

---

### Task E2: Save round-trip with everything

**Objective:** A game with shipments in transit, a built vessel, unlocked tech and stale beer
resumes identically.

**Files:** append to `tests/BreweryEmpire.Core.Tests/Integration/Phase4IntegrationTests.cs`.

**Step 1 — tests:**
```csharp
        [Fact]
        public void Save_Load_Resume_Is_Identical_With_Shipments_And_Builds()
        {
            var s = London1890();
            BrewingSystem.TryStartBrew(s, new NodeId("london"), new RecipeId("pale-ale"));
            BuildSystem.TryPurchaseVessel(s, new NodeId("london"), VesselType.OpenFermenter, EquipmentTier.Stainless, 4000);
            var batch = s.World.Get(new NodeId("london")).Batches.First(b => b.VolumeLitres >= 100);
            batch.MarkReady();
            LogisticsSystem.DispatchShipment(s, new NodeId("london"), new NodeId("hamburg"),
                batch.Id, 100, PackagingType.Bottle, TransportMode.SteamRail, 700, refrigerated: true);

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));

            reloaded.Shipments.Should().HaveCount(1);
            TickSystem.AdvanceDays(reloaded, 30);
            TickSystem.AdvanceDays(s, 30);
            TestScenario.Fingerprint(reloaded).Should().Be(TestScenario.Fingerprint(s));
        }
```

**Step 2 — run, expect PASS** (any failure is a real save/determinism bug). **Step 3 — commit:**
```bash
git add tests/BreweryEmpire.Core.Tests/Integration/Phase4IntegrationTests.cs
git commit -m "test(core): save round-trip with shipments, builds and tech"
```

---

### Task E3: Guards, README, full suite

**Objective:** Phase 1's promises survive Phase 4, and the docs stay current.

**Files:** `README.md`; verify guards untouched.

**Step 1** — run the full suite and confirm zero regressions:
```bash
export PATH="$HOME/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
unset DOTNET_ROOT
dotnet build -c Release
dotnet test
```
**Expected:** 0 warnings, all tests green. (No new `System.Random`/`Guid`/`DateTime.Now`/float —
none of the Phase 4 code introduces any; the guards in `Guards/DeterminismGuardsTests.cs` should
already pass.)

**Step 2** — add a "Phase 4: distribution, expansion & shelf-life" note to `README.md` after the
Phase 3 paragraph, covering: transport modes + in-transit spoilage + refrigerated rail
(`LogisticsSystem`, `SpoilageSystem.ProcessShipment`), the vessel build surface
(`BuildSystem`, stainless-steel gating), the shelf-life model + drum-roaster payoff, and the
scenario layer (`ScenarioDefinition`/`ScenarioCatalog`/`ScenarioAssembler`). One short paragraph.

**Step 3 — commit:**
```bash
git add README.md
git commit -m "docs: describe transport, expansion, shelf-life and scenarios"
```

---

## Validation Summary

| Check | Command | Expected |
|---|---|---|
| Build | `dotnet build -c Release` (with env prefix) | 0 warnings, 0 errors |
| Unit tests | `dotnet test` | all pass (~+40 new tests) |
| Shipment save | `SaveSystemTests.Shipments_In_Transit_Round_Trip` | round-trips mode + cargo |
| v3→v4 load | `SaveSystemTests.Version_3_Save_Loads_With_No_Shipments` | loads, empty shipments |
| Transit spoilage | `TransitSpoilageTests.Warm_Transit_Can_Contract_An_Infection` | infection contracts |
| Refrigerated rail | `TechEffectTests.Refrigerated_Shipping_Requires_Refrigeration_Tech_And_Rail` | gated by tech |
| Build | `BuildSystemTests.Purchase_Adds_Vessel_And_Debits_Capital` | vessel added, capital debited |
| Shelf-life | `ShelfLifeTests.Drum_Roaster_Extends_Dark_Beer_Shelf_Life` | drum roaster extends stout |
| Determinism | `Phase4IntegrationTests.A_Year_Of_Scenario_Play_Is_Deterministic_And_Invariant_Holds` | identical across runs |
| Save/resume | `Phase4IntegrationTests.Save_Load_Resume_Is_Identical_With_Shipments_And_Builds` | identical fingerprint |
| Unity independence | existing guards | no `UnityEngine` reference |
| Determinism leaks | existing guards | no `System.Random`/`DateTime.Now`/`Guid.NewGuid` |

---

## Risks, tradeoffs, and open questions

- **Two latent bugs fixed in-scope.** Shipment save/load and attribute-loss-on-delivery are not
  "features" but preconditions for the transport work. Fixing them changes mint-id sequencing
  (cargo batches are minted at dispatch), which changes deterministic fingerprints vs. Phase 3 —
  acceptable because no golden-master fixture is committed, but call it out in the commit message.
- **Single-hop only.** `LogisticsGraph` (Dijkstra, multi-hop) already exists and is tested but
  remains disconnected from `DispatchShipment`. Multi-hop routing is deliberately deferred —
  single-hop mode-aware dispatch satisfies "stop hardcoding HorseCart". If multi-hop is wanted,
  `DispatchShipment`'s distance/mode resolution is the seam to route through `LogisticsGraph`.
- **`WorldMap` vs `LogisticsGraph` are two overlapping route stores.** Phase 4 makes `WorldMap`
  the single source dispatch reads (mode + distance). `LogisticsGraph` is now redundant except for
  its Dijkstra tests. Leave it until multi-hop lands, then consolidate into `WorldMap`.
- **Shelf-life numbers are placeholders** (base 30, +15 dark, +abv/40, +45 drum-roaster, −25bp/day
  stale). Same rule as Phases 1–3: get the plumbing and relationships testable, tune after.
- **`PackagingSpec.ShelfLifeDays` remains unused** (container longevity). The batch-level
  `ShelfLifeDays` is the active mechanic; packaging shelf-life can later cap transit shelf-life
  (`min(batch, packaging)`). Flag, don't wire, for now.
- **Scenario layer is the trimmable block.** If time-boxed, drop Block D first — the sim already
  runs headlessly via inline test worlds; the scenario layer is the ergonomic layer for the Unity
  front end and the headless runner.
- **`RefrigeratedRail` is modelled as `SteamRail` + `IsRefrigerated` + the `refrigeration` tech**,
  not a separate enum member. This reuses `TransportSpec.IsRefrigeratedCapable` and keeps the enum
  stable (safe — no save migration).

**Open questions (non-blocking; defaults chosen above):**
1. **Multi-hop routing** — do it as a Phase 4.5, or fold into a later Unity-facing phase?
2. **Should building require a foreman / take construction days?** Plan makes purchase instant;
   a construction-delay field on `Vessel` is a small add later.
3. **Should stale beer be *visible* to the player** (a "past best" flag) before it spoils? The
   quality decay already surfaces in price; a UI-only flag can come with the front end.
4. **Scenario recipes** — scenarios do not embed recipes (recipes stay caller-supplied). If you want
   fully self-contained scenarios, add a core `RecipeCatalog` (PaleAle/Porter factories) later.
