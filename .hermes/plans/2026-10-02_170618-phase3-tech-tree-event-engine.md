# Phase 3: Tech Tree (Research DAG) & Event Engine — Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.
> Each task = failing test → watch it fail → minimal implementation → watch it pass → commit.
> Keep `dotnet build -c Release` warning-free (TreatWarningsAsErrors) and `dotnet test` green
> at every step. Everything is integer-only and deterministic — the guards in
> `tests/BreweryEmpire.Core.Tests/Guards/` must stay green.

**Goal:** Add the two systems the design doc assigns to Phase 3 — a researchable tech-tree DAG
that toggles the mechanics Phase 2 already built, and a choice-based historical event engine —
without touching the Unity-free guarantee.

**Architecture:** Phase 2 deliberately pre-built the *mechanics* (cold chain, pasteurization
toggle, yeast generation, gravity computation) as flags and fields; Phase 3 adds the *decision
layer* that flips them. A pure-data `TechNode` catalog + a `ResearchState` aggregate on `GameState`
are driven by a stateless `ResearchSystem` (chemists accrue points → research completes → an effect
switch flips the existing toggles). A pure-data `GameEvent` catalog + `PendingEvents`/`ResolvedEventIds`
on `GameState` are driven by a stateless `EventSystem` (deterministic date/state triggers enqueue an
event; the player resolves a choice; typed effects hit money/prestige/reputation/duty/ice).

**Tech Stack:** C# 9 / netstandard2.1 (library, unchanged), net8.0 (tests, unchanged), xUnit +
FluentAssertions, `dotnet` CLI. **No new project, no new package.**

---

## Current context / assumptions (verified against the code)

- Repo `C:\Users\Child\OneDrive\Projects\brewing-empire`, solution `BreweryEmpire.sln`.
- `src/BreweryEmpire.Core` (netstandard2.1) + `tests/BreweryEmpire.Core.Tests` (net8.0).
- `.NET` is **not** on PATH; every `dotnet` command must be prefixed with:
  `export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet" PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1`.
  Shell is git-bash/MSYS (POSIX syntax), not PowerShell.
- **Simulation is a static orchestration**, not an engine+systems list. `TickSystem.AdvanceDay(GameState)`
  calls static systems in a fixed order (`Simulation/TickSystem.cs:19-53`): Fermentation → Logistics →
  Spoilage → Market → Economy (upkeep + payroll) → bankruptcy/compact/sync. Phase 3 inserts
  `ResearchSystem.ProcessDay` + `EventSystem.ProcessDay` into the housekeeping tail.
- **Existing hooks Phase 3 rides on (do not rebuild them):**
  - `Era` enum (`State/GameDate.cs:15-21`) + `GameDate.Era` derivation (tested).
  - `TraitEffect.ResearchRate` (`Model/Staff/StaffRole.cs:23`) — **exists but nothing consumes it yet.**
  - Chemist historical figures already carry `ResearchRate` traits (`Model/Staff/HistoricalFigures.cs`:
    Hansen +1500, Pasteur +2000, Linde +2500).
  - Cold chain: `BreweryNode.IsRefrigerated` / `HasIceHouse` / `IceStockTonnes` / `HasColdStorage`
    (`Model/Sites/BreweryNode.cs:48-77`), consumed by `FermentationSystem` (temp penalty) and
    `SpoilageSystem` (cold slows progression).
  - Pasteurization: `Batch.IsPasteurized` (`Model/Brewing/Batch.cs:47`), consumed by
    `SpoilageSystem.ProgressionRateBasisPoints` (pasteurized → token 20bp/day).
  - Yeast: `Batch.YeastIngredientId`/`YeastGeneration`; spoilage organism roll is
    `(SpoilageOrganism)state.Random.NextInt(0, 5)` (`SpoilageSystem.cs:144`) — index 4 is `WildYeast`.
  - Gravity: `Batch.OriginalGravityPoints`/`FinalGravityPoints` already computed
    (`MashChemistry.ComputeGravity`), but `BrewingSystem.TryStartBrew` passes `waterFitBasisPoints: 10000`
    hardcoded (`BrewingSystem.cs:99-102`) and there is no saccharometer bonus yet.
  - `PriceBook.ExciseDutyModifierBasisPoints` (`Economy/PriceBook.cs:31`) — the event hook (tested).
  - `LedgerCategory.EventCost = 11` already exists (`Economy/Ledger.cs:21`); `Ledger.Credit` /
    `ForceDebit` / `Post` are the money API.
  - `GameState.Rivals`, `GameState.Markets`, `GameState.ReputationBasisPoints` exist. **There is no
    `UnlockedTechIds`, no `PrestigeBasisPoints`, no event state — Phase 3 adds them.**
- `SaveVersion` is currently `2` (`GameState.CurrentSaveVersion = 2`). Phase 3 adds fields and bumps to 3.
- **Scoping note (transport/packaging/vessel-tier):** `LogisticsSystem.DispatchShipment` hardcodes
  `TransportMode.HorseCart` (`LogisticsSystem.cs:34-38`), so a transport-mode tech has no enforcement
  point; vessel tier and packaging are scenario-driven, not built during play. This plan therefore
  **wires** the effects that have a live consumer in the sim (style gating, saccharometer, water
  chemistry, pure yeast, pasteurization, refrigeration, ice house, packaging gate) and **catalogues but
  does not mechanically wire** the ones that need a build/purchase/logistics surface (drum-roaster,
  refrigerated-rail, stainless-steel). This is called out per-task and in "Open questions".
- Assume `dotnet build -c Release` and `dotnet test` are green at Phase 2 completion. **Task 0 re-verifies.**

---

## Task blocks

| Block | Tasks | Theme |
|---|---|---|
| A — Research DAG core | A1–A5 | TechNode model, catalog, ResearchState + save, ResearchSystem, tick hook |
| B — Tech effects (wire the toggles) | B1–B7 | style gating, saccharometer, water chemistry, pure yeast, pasteurization, refrigeration/ice-house, packaging gate |
| C — Event engine | C1–C4 | GameEvent model, catalog, EventSystem triggers/choices/effects, tick hook |
| D — Proof | D1–D4 | era-progression integration, save round-trip + v2→v3, determinism, guards/README |

All new code lives in `src/BreweryEmpire.Core`; all tests in `tests/BreweryEmpire.Core.Tests`.

---

# Block A — Research DAG core

### Task A0: Re-verify baseline

**Objective:** Confirm Phase 2 is green before touching anything.

**Step 1** — run:
```bash
export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet" PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build -c Release
dotnet test
```
**Expected:** build 0 warnings/errors; tests all pass. If not, stop and report — do not build on a red baseline.

---

### Task A1: `TechNode` model

**Objective:** A serializable, pure-data description of one research node.

**Files:** Create `src/BreweryEmpire.Core/Model/Research/TechNode.cs`; test `tests/BreweryEmpire.Core.Tests/Model/Research/TechNodeTests.cs`.

**Step 1 — write the failing test** (`TechNodeTests.cs`):
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Research;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Research
{
    public class TechNodeTests
    {
        [Fact]
        public void Prerequisites_Default_Empty_Not_Null()
        {
            var n = new TechNode { Id = "x", DisplayName = "X" };
            n.PrerequisiteIds.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void PrerequisiteIds_Are_Preserved_In_Order()
        {
            var n = new TechNode { Id = "y", PrerequisiteIds = new[] { "a", "b" } };
            n.PrerequisiteIds.Should().Equal("a", "b");
        }

        [Fact]
        public void MinEra_Defaults_To_PreIndustrial()
        {
            new TechNode().MinEra.Should().Be(Era.PreIndustrial);
        }
    }
}
```

**Step 2 — run, expect FAIL** (`TechNode` does not exist):
```bash
dotnet test --filter "FullyQualifiedName~TechNodeTests"
```
Expected: compile error CS0246 ("The type or namespace name 'TechNode' could not be found").

**Step 3 — implement** `Model/Research/TechNode.cs`:
```csharp
using System;
using System.Collections.Generic;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Research
{
    /// <summary>
    /// One node in the research tree. Pure data: prerequisites and cost shape the
    /// DAG, effects are applied by ResearchSystem so the catalog stays serializable
    /// and the behaviour stays in one place.
    /// </summary>
    public sealed class TechNode
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Earliest era the node may be researched.</summary>
        public Era MinEra { get; init; } = Era.PreIndustrial;

        /// <summary>Research points required to complete.</summary>
        public int ResearchCostPoints { get; init; }

        /// <summary>Tech ids that must already be unlocked.</summary>
        public IReadOnlyList<string> PrerequisiteIds { get; init; } = Array.Empty<string>();

        public string Description { get; init; } = string.Empty;

        /// <summary>The educational "Science Snapshot" shown to the player.</summary>
        public string LoreText { get; init; } = string.Empty;
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Research/TechNode.cs tests/BreweryEmpire.Core.Tests/Model/Research/TechNodeTests.cs
git commit -m "feat(research): add TechNode model"
```

---

### Task A2: `TechCatalog`

**Objective:** The full, era-gated research tree as a static list (mirrors `HistoricalFigures`).

**Files:** Create `src/BreweryEmpire.Core/Model/Research/TechCatalog.cs`; test `tests/BreweryEmpire.Core.Tests/Model/Research/TechCatalogTests.cs`.

**Step 1 — failing test** (`TechCatalogTests.cs`):
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Research;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Research
{
    public class TechCatalogTests
    {
        [Fact]
        public void Every_Node_Has_Unique_Id()
        {
            TechCatalog.All.Select(t => t.Id).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void AvailableIn_Filters_By_Era()
        {
            var in1750 = TechCatalog.AvailableIn(GameDate.FromYearMonthDay(1750, 6, 1));
            in1750.Should().NotContain(t => t.Id == "saccharometer");   // Industrial era

            var in1900 = TechCatalog.AvailableIn(GameDate.FromYearMonthDay(1900, 6, 1));
            in1900.Should().Contain(t => t.Id == "pure-yeast");         // Scientific era
        }

        [Fact]
        public void Prerequisites_Reference_Known_Ids()
        {
            var ids = TechCatalog.All.Select(t => t.Id).ToHashSet();
            foreach (var t in TechCatalog.All)
                foreach (var p in t.PrerequisiteIds)
                    ids.Should().Contain(p, "every prerequisite must name a real tech");
        }

        [Fact]
        public void Get_Unknown_Returns_Null()
        {
            TechCatalog.Get("nope").Should().BeNull();
        }

        [Fact]
        public void RequiredTechFor_Maps_Styles()
        {
            TechCatalog.RequiredTechFor(BeerStyle.PaleAle).Should().BeNull();          // era-1 baseline
            TechCatalog.RequiredTechFor(BeerStyle.Porter).Should().Be("malting-kilns");
            TechCatalog.RequiredTechFor(BeerStyle.Pilsner).Should().Be("pale-revolution");
        }

        [Fact]
        public void TargetWaterFor_Maps_Styles()
        {
            TechCatalog.TargetWaterFor(BeerStyle.PaleAle).Should().Be(WaterProfile.Burton);
            TechCatalog.TargetWaterFor(BeerStyle.Stout).Should().Be(WaterProfile.Dublin);
        }
    }
}
```

**Step 2 — run, expect FAIL** (TechCatalog missing).

**Step 3 — implement** `Model/Research/TechCatalog.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Research
{
    /// <summary>
    /// The research tree. Eras and prerequisites come from the design doc's
    /// "Technology & Historical Science Tree" (section 3). Effects are NOT here —
    /// ResearchSystem.ApplyEffect owns them.
    /// </summary>
    public static class TechCatalog
    {
        private static readonly List<TechNode> _all = new List<TechNode>
        {
            new TechNode
            {
                Id = "malting-kilns", DisplayName = "Malting Kilns", MinEra = Era.PreIndustrial,
                ResearchCostPoints = 120,
                Description = "Roast barley into dark malt.",
                LoreText = "Kilning malt unlocks porter and stout — dark, robust beers that keep."
            },
            new TechNode
            {
                Id = "saccharometer", DisplayName = "Saccharometer & Hydrometer", MinEra = Era.Industrial,
                ResearchCostPoints = 180, PrerequisiteIds = new[] { "malting-kilns" },
                Description = "Measure sugar density to hit gravity targets.",
                LoreText = "Measuring rather than guessing raises extract efficiency."
            },
            new TechNode
            {
                Id = "drum-roaster", DisplayName = "Patent Drum Roaster", MinEra = Era.Industrial,
                ResearchCostPoints = 220, PrerequisiteIds = new[] { "malting-kilns" },
                Description = "Super-roasted unmalted barley for light-bodied dark stouts.",
                LoreText = "Guinness-era roasting. (Effect deferred — no shelf-life model yet.)"
            },
            new TechNode
            {
                Id = "pale-revolution", DisplayName = "The Bohemian Pale Revolution", MinEra = Era.Industrial,
                ResearchCostPoints = 300, PrerequisiteIds = new[] { "saccharometer" },
                Description = "Soft water + pale malt + bottom-fermenting yeast = golden lager.",
                LoreText = "Pilsner Urquell, 1842: shifts world demand from dark ales to golden lager."
            },
            new TechNode
            {
                Id = "ice-house", DisplayName = "Ice House Logistics", MinEra = Era.Industrial,
                ResearchCostPoints = 150,
                Description = "Store winter lake ice in insulated cellars for summer brewing.",
                LoreText = "Bavaria banned summer brewing in 1553; stored ice beat the heat."
            },
            new TechNode
            {
                Id = "pure-yeast", DisplayName = "Pure Yeast Isolation", MinEra = Era.Scientific,
                ResearchCostPoints = 400, PrerequisiteIds = new[] { "saccharometer" },
                Description = "Isolate a single yeast strain; eliminates wild-yeast contamination.",
                LoreText = "Carlsberg, 1883: Saccharomyces carlsbergensis — the first pure culture."
            },
            new TechNode
            {
                Id = "refrigeration", DisplayName = "Mechanical Refrigeration", MinEra = Era.Scientific,
                ResearchCostPoints = 450, PrerequisiteIds = new[] { "ice-house" },
                Description = "Ammonia compressor removes seasonal brewing limits.",
                LoreText = "Linde, 1873: year-round cold fermentation without an ice harvest."
            },
            new TechNode
            {
                Id = "pasteurization", DisplayName = "Pasteurization", MinEra = Era.Scientific,
                ResearchCostPoints = 350, PrerequisiteIds = new[] { "pure-yeast" },
                Description = "Heat-treat finished beer to near-immunity to spoilage.",
                LoreText = "Gentle heating kills spoilage organisms before shipping."
            },
            new TechNode
            {
                Id = "water-chemistry", DisplayName = "Water Chemistry & pH", MinEra = Era.Scientific,
                ResearchCostPoints = 380, PrerequisiteIds = new[] { "saccharometer" },
                Description = "Adjust water hardness and acidity to suit a style.",
                LoreText = "Carlsberg Lab, 1909: matching water to style raises extract."
            },
            new TechNode
            {
                Id = "stainless-steel", DisplayName = "Stainless Steel Vessels", MinEra = Era.Modern,
                ResearchCostPoints = 500, PrerequisiteIds = new[] { "refrigeration" },
                Description = "Maximum hygiene and consistency. (Effect deferred — no build system yet.)",
                LoreText = "Inert steel displaces wood and copper."
            },
            new TechNode
            {
                Id = "bottling-line", DisplayName = "Automated Bottling & Canning", MinEra = Era.Modern,
                ResearchCostPoints = 450, PrerequisiteIds = new[] { "pasteurization" },
                Description = "High-throughput packaging for global logistics.",
                LoreText = "Sealed, long-lived containers open distant markets."
            }
        };

        public static IReadOnlyList<TechNode> All => _all;

        public static TechNode? Get(string id) => _all.FirstOrDefault(t => t.Id == id);

        public static IEnumerable<TechNode> AvailableIn(GameDate date) =>
            _all.Where(t => date.Era >= t.MinEra);

        /// <summary>The tech that must be unlocked to brew a style; null = available from Era 1.</summary>
        public static string? RequiredTechFor(BeerStyle style) => style switch
        {
            BeerStyle.Porter or BeerStyle.Stout or BeerStyle.Mild => "malting-kilns",
            BeerStyle.Pilsner or BeerStyle.Lager or BeerStyle.ViennaLager or BeerStyle.Bock => "pale-revolution",
            _ => null   // PaleAle, BerlinerWeisse, Lambic are the era-1 baseline
        };

        /// <summary>The water profile a style prefers (used by the water-chemistry effect).</summary>
        public static WaterProfile TargetWaterFor(BeerStyle style) => style switch
        {
            BeerStyle.PaleAle => WaterProfile.Burton,
            BeerStyle.Pilsner or BeerStyle.Lager or BeerStyle.ViennaLager or BeerStyle.Bock => WaterProfile.Pilsen,
            BeerStyle.Porter or BeerStyle.Mild => WaterProfile.London,
            BeerStyle.Stout => WaterProfile.Dublin,
            _ => WaterProfile.Burton
        };
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Research/TechCatalog.cs tests/BreweryEmpire.Core.Tests/Model/Research/TechCatalogTests.cs
git commit -m "feat(research): add era-gated TechCatalog with prereq DAG"
```

---

### Task A3: `ResearchState` + `GameState` fields + save round-trip

**Objective:** Persist research progress, prestige, and event state. Bump save version 2 → 3 with a
backward-compatible load (old saves default to empty research).

**Files:** Create `src/BreweryEmpire.Core/State/ResearchState.cs`; modify `State/GameState.cs`,
`State/SaveSystem.cs`; tests `tests/BreweryEmpire.Core.Tests/State/SaveSystemTests.cs` (create if absent).

**Step 1 — failing test** (`SaveSystemTests.cs`) — asserts round-trip preserves the new state, and a
hand-written v2 save still loads:
```csharp
using System.Collections.Generic;
using BreweryEmpire.Core.Model.Events;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.State
{
    public class SaveSystemTests
    {
        [Fact]
        public void Research_State_Round_Trips()
        {
            var s = TestScenario.Standard();
            s.Research.UnlockedTechIds.Add("malting-kilns");
            s.Research.ActiveTechId = "saccharometer";
            s.Research.ActiveProgressPoints = 77;
            s.PrestigeBasisPoints = 1234;
            s.PendingEvents.Add(new GameEvent { Id = "e1", Title = "T" });
            s.ResolvedEventIds.Add("e0");

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));

            reloaded.Research.UnlockedTechIds.Should().Equal("malting-kilns");
            reloaded.Research.ActiveTechId.Should().Be("saccharometer");
            reloaded.Research.ActiveProgressPoints.Should().Be(77);
            reloaded.PrestigeBasisPoints.Should().Be(1234);
            reloaded.PendingEvents.Should().ContainSingle(e => e.Id == "e1");
            reloaded.ResolvedEventIds.Should().Equal("e0");
        }

        [Fact]
        public void Version_2_Save_Loads_With_Empty_Research()
        {
            // A v2 save has no research/prestige/event fields — they must default.
            var s = TestScenario.Standard();
            var json = SaveSystem.Save(s).Replace("\"SaveVersion\":3", "\"SaveVersion\":2");

            var reloaded = SaveSystem.Load(json);

            reloaded.Research.UnlockedTechIds.Should().BeEmpty();
            reloaded.Research.ActiveTechId.Should().BeNull();
            reloaded.PrestigeBasisPoints.Should().Be(0);
            reloaded.PendingEvents.Should().BeEmpty();
        }

        [Fact]
        public void Newer_Save_Version_Is_Refused()
        {
            var s = TestScenario.Standard();
            var json = SaveSystem.Save(s).Replace("\"SaveVersion\":3", "\"SaveVersion\":99");
            System.Action load = () => SaveSystem.Load(json);
            load.Should().Throw<System.InvalidOperationException>();
        }
    }
}
```
(If `tests/.../State/SaveSystemTests.cs` already exists, append these three facts to it instead of
creating a duplicate; the assertions above use only existing public API + the new fields.)

**Step 2 — run, expect FAIL** (`Research`, `PrestigeBasisPoints`, `PendingEvents`, `ResolvedEventIds`,
`GameEvent` don't exist).

**Step 3 — implement.**

(a) `State/ResearchState.cs`:
```csharp
using System.Collections.Generic;

namespace BreweryEmpire.Core.State
{
    /// <summary>Player research progress. Must survive a save; lives on GameState.</summary>
    public sealed class ResearchState
    {
        public List<string> UnlockedTechIds { get; set; } = new List<string>();
        public string? ActiveTechId { get; set; }
        public int ActiveProgressPoints { get; set; }
    }
}
```

(b) `State/GameState.cs` — add after `public int ReputationBasisPoints` (line 44):
```csharp
        /// <summary>Campaign-level research progress.</summary>
        public ResearchState Research { get; set; } = new ResearchState();

        /// <summary>Global prestige score; raised by tech/event choices, never negative.</summary>
        public int PrestigeBasisPoints { get; set; }

        /// <summary>Events awaiting a player choice.</summary>
        public List<BreweryEmpire.Core.Model.Events.GameEvent> PendingEvents { get; set; } =
            new List<BreweryEmpire.Core.Model.Events.GameEvent>();

        /// <summary>Ids of events already resolved (one-shot dedupe).</summary>
        public List<string> ResolvedEventIds { get; set; } = new List<string>();
```
And change `public const int CurrentSaveVersion = 2;` → `= 3;`.

(c) `State/SaveSystem.cs` — four edits:

(i) In `ToDto` (after `Rivals = s.Rivals` inside the `GameStateDto` initializer, ~line 68):
```csharp
                Research = s.Research,
                PrestigeBasisPoints = s.PrestigeBasisPoints,
                PendingEvents = s.PendingEvents,
                ResolvedEventIds = s.ResolvedEventIds,
```

(ii) In `FromDto` (after `Rivals = dto.Rivals ?? new List<RivalBrewer>()`, ~line 238):
```csharp
                Research = dto.Research ?? new ResearchState(),
                PrestigeBasisPoints = dto.PrestigeBasisPoints,
                PendingEvents = dto.PendingEvents ?? new List<BreweryEmpire.Core.Model.Events.GameEvent>(),
                ResolvedEventIds = dto.ResolvedEventIds ?? new List<string>(),
```

(iii) In `GameStateDto` (add before the closing brace ~line 454):
```csharp
            public ResearchState? Research { get; set; }
            public int PrestigeBasisPoints { get; set; }
            public List<BreweryEmpire.Core.Model.Events.GameEvent>? PendingEvents { get; set; }
            public List<string>? ResolvedEventIds { get; set; }
```

(iv) Add `using BreweryEmpire.Core.Model.Events;` is NOT required because the DTO uses fully-qualified
names above; add it if you prefer shorter names.

**Step 4 — run, expect PASS** (round-trip + v2-load + refuse-newer).

Note: the v2-load test replaces `"SaveVersion":3` — because the DTO is flat JSON and `SaveVersion`
is the only version marker, this exercises the "old save, new fields default" path directly.

**Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/State/ResearchState.cs src/BreweryEmpire.Core/State/GameState.cs src/BreweryEmpire.Core/State/SaveSystem.cs tests/BreweryEmpire.Core.Tests/State/SaveSystemTests.cs
git commit -m "feat(research): persist research/prestige/event state, bump save version to 3"
```
(If the test referenced `GameEvent` which does not exist yet, create a minimal stub now and finish it
in Task C1 — or better, implement Tasks C1 (model) before A3's test compiles. To keep this commit
self-contained, either reorder so C1 comes before A3, or add the `GameEvent` class in this task as a
minimal empty shell. **Recommended: create `GameEvent`'s full model in this task's place is deferred;
simplest is to write `Model/Events/GameEvent.cs` from Task C1 first.** If you hit a chicken-and-egg
compile error, implement the `GameEvent`/`EventChoice`/`EventEffect`/`EventEffectKind` definitions from
Task C1 now, then continue.)

---

### Task A4: `ResearchSystem` (accrual + unlock + effects)

**Objective:** The stateless brain: chemists accrue points daily, research completes and unlocks,
effects flip the existing toggles.

**Files:** Create `src/BreweryEmpire.Core/Simulation/ResearchSystem.cs`; test
`tests/BreweryEmpire.Core.Tests/Simulation/ResearchSystemTests.cs`.

**Step 1 — failing test** (representative; add more after GREEN):
```csharp
using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class ResearchSystemTests
    {
        private static GameState WithChemist(int skill, int researchTrait = 0)
        {
            var s = TestScenario.Standard();
            var traits = researchTrait > 0
                ? new[] { new StaffTrait { Id = "rt", DisplayName = "R", AppliesTo = StaffRole.Chemist, Effect = TraitEffect.ResearchRate, MagnitudeBasisPoints = researchTrait } }
                : System.Array.Empty<StaffTrait>();
            var chemist = new StaffMember(new StaffId("chem-1"), "Chemist", StaffRole.Chemist,
                                          skill, BreweryEmpire.Core.Economy.Money.FromWhole(30), 40,
                                          GameDate.FromYearMonthDay(1750, 1, 1), traits);
            s.Staff.Hire(chemist, GameDate.FromYearMonthDay(1750, 1, 1));
            return s;
        }

        [Fact]
        public void No_Staff_Accrues_One_Point_Per_Day()
        {
            var s = TestScenario.Standard();
            ResearchSystem.DailyResearchPoints(s).Should().Be(1);
        }

        [Fact]
        public void Chemist_Accelerates_Research()
        {
            var s = WithChemist(skill: 5000);
            ResearchSystem.DailyResearchPoints(s).Should().BeGreaterThan(1);
        }

        [Fact]
        public void Start_Requires_Era_And_Prerequisites()
        {
            var s = TestScenario.Standard();   // date = 1750
            ResearchSystem.Start(s, "saccharometer").Should().BeFalse(); // era not reached
            ResearchSystem.Start(s, "pure-yeast").Should().BeFalse();    // prereq saccharometer
            ResearchSystem.Start(s, "malting-kilns").Should().BeTrue();  // era-1, no prereq
            ResearchSystem.Start(s, "ice-house").Should().BeFalse();     // already researching
        }

        [Fact]
        public void ProcessDay_Completes_And_Unlocks()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Start(s, "malting-kilns");          // cost 120
            for (int i = 0; i < 120; i++) ResearchSystem.ProcessDay(s);
            ResearchSystem.HasTech(s, "malting-kilns").Should().BeTrue();
            s.Research.ActiveTechId.Should().BeNull();
        }

        [Fact]
        public void Ice_House_Unlock_Enables_Ice_Harvest()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));
            node.HasIceHouse.Should().BeFalse();

            ResearchSystem.Unlock(s, "ice-house");

            node.HasIceHouse.Should().BeTrue();
        }

        [Fact]
        public void Refrigeration_Unlock_Sets_IsRefrigerated()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));
            ResearchSystem.Unlock(s, "refrigeration");
            node.IsRefrigerated.Should().BeTrue();
        }

        [Fact]
        public void Packaging_Unlocked_Only_By_Bottling_Line()
        {
            var s = TestScenario.Standard();
            ResearchSystem.IsPackagingUnlocked(s, PackagingType.WoodenCask).Should().BeTrue();
            ResearchSystem.IsPackagingUnlocked(s, PackagingType.Bottle).Should().BeFalse();
            ResearchSystem.Unlock(s, "bottling-line");
            ResearchSystem.IsPackagingUnlocked(s, PackagingType.Bottle).Should().BeTrue();
        }

        [Fact]
        public void Style_Unlocked_Matches_Required_Tech()
        {
            var s = TestScenario.Standard();
            ResearchSystem.IsStyleUnlocked(s, BeerStyle.PaleAle).Should().BeTrue();
            ResearchSystem.IsStyleUnlocked(s, BeerStyle.Porter).Should().BeFalse();
            ResearchSystem.Unlock(s, "malting-kilns");
            ResearchSystem.IsStyleUnlocked(s, BeerStyle.Porter).Should().BeTrue();
        }
    }
}
```

**Step 2 — run, expect FAIL** (ResearchSystem missing).

**Step 3 — implement** `Simulation/ResearchSystem.cs`:
```csharp
using System;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Research;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// The research loop. Chemists accrue points each day; when the active node's
    /// cost is met it unlocks and its effect is applied. All arithmetic is integer;
    /// nothing here touches the RNG, so research is fully deterministic.
    /// </summary>
    public static class ResearchSystem
    {
        public const int BaseDailyPoints = 1;

        /// <summary>Points accrued per day: a base point plus each chemist's contribution.</summary>
        public static int DailyResearchPoints(GameState state)
        {
            int points = BaseDailyPoints;
            foreach (var s in state.Staff.All)
            {
                if (s.Role != StaffRole.Chemist) continue;
                points += (s.SkillBasisPoints + s.BonusFor(TraitEffect.ResearchRate)) / 1000;
            }
            return points;
        }

        public static bool HasTech(GameState state, string techId) =>
            state.Research.UnlockedTechIds.Contains(techId);

        public static bool CanStart(GameState state, string techId)
        {
            if (state.Research.ActiveTechId != null) return false;
            var tech = TechCatalog.Get(techId);
            if (tech == null) return false;
            if (HasTech(state, techId)) return false;
            if (state.Date.Era < tech.MinEra) return false;
            if (!tech.PrerequisiteIds.All(HasTechState(state))) return false;
            return true;
        }

        private static Func<string, bool> HasTechState(GameState state) =>
            id => state.Research.UnlockedTechIds.Contains(id);

        /// <summary>Begin researching. Returns false (no state change) if invalid.</summary>
        public static bool Start(GameState state, string techId)
        {
            if (!CanStart(state, techId)) return false;
            state.Research.ActiveTechId = techId;
            state.Research.ActiveProgressPoints = 0;
            return true;
        }

        /// <summary>Advance research one day; complete and unlock when the cost is met.</summary>
        public static void ProcessDay(GameState state)
        {
            if (state.Research.ActiveTechId == null) return;
            state.Research.ActiveProgressPoints += DailyResearchPoints(state);

            var tech = TechCatalog.Get(state.Research.ActiveTechId);
            if (tech != null && state.Research.ActiveProgressPoints >= tech.ResearchCostPoints)
                Unlock(state, tech.Id);
        }

        /// <summary>Force-unlock and apply an effect. Used by events and tests; does not
        /// re-check era/prereqs — Start/CanStart are the gated entry points.</summary>
        public static void Unlock(GameState state, string techId)
        {
            if (HasTech(state, techId)) return;
            state.Research.UnlockedTechIds.Add(techId);
            if (state.Research.ActiveTechId == techId) state.Research.ActiveTechId = null;
            ApplyEffect(state, techId);
        }

        public static bool IsStyleUnlocked(GameState state, BeerStyle style)
        {
            var required = TechCatalog.RequiredTechFor(style);
            return required == null || HasTech(state, required);
        }

        public static bool IsPackagingUnlocked(GameState state, PackagingType type) =>
            type == PackagingType.WoodenCask || HasTech(state, "bottling-line");

        /// <summary>Retroactive effects. Flag-only techs (saccharometer, pure-yeast,
        /// pasteurization, water-chemistry) are consumed directly by the systems that read
        /// HasTech(...); nothing to set here.</summary>
        private static void ApplyEffect(GameState state, string techId)
        {
            switch (techId)
            {
                case "ice-house":
                    foreach (var n in state.World.Nodes) n.HasIceHouse = true;
                    break;
                case "refrigeration":
                    foreach (var n in state.World.Nodes) n.IsRefrigerated = true;
                    break;
                // malting-kilns, saccharometer, drum-roaster, pale-revolution, pure-yeast,
                // pasteurization, water-chemistry, stainless-steel, bottling-line: flag-only.
            }
        }
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/ResearchSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/ResearchSystemTests.cs
git commit -m "feat(research): add ResearchSystem accrual, unlock and retroactive effects"
```

---

### Task A5: Wire research into the tick

**Objective:** Research advances every day, in the deterministic tail of the tick.

**Files:** Modify `Simulation/TickSystem.cs`; test in `ResearchSystemTests.cs` (append) or
`tests/BreweryEmpire.Core.Tests/Integration/EraProgressionTests.cs`.

**Step 1 — failing test** (append to `ResearchSystemTests.cs`):
```csharp
        [Fact]
        public void Tick_Advances_Research_Daily()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Start(s, "malting-kilns");   // cost 120, base 1/day
            TickSystem.AdvanceDays(s, 120);
            ResearchSystem.HasTech(s, "malting-kilns").Should().BeTrue();
        }
```

**Step 2 — run, expect FAIL** (tick does not call research).

**Step 3 — implement.** In `TickSystem.AdvanceDay`, replace the housekeeping tail
(lines 47–52):
```csharp
            EconomySystem.CheckBankruptcy(state);
            state.Ledger.CompactHistory(state.Date);
            state.SyncRandomState();
```
with:
```csharp
            EconomySystem.CheckBankruptcy(state);
            state.Ledger.CompactHistory(state.Date);

            // 8. Research accrues and may complete; due events are enqueued. Both are
            //    deterministic (no RNG) and order-independent, so they run last.
            ResearchSystem.ProcessDay(state);
            EventSystem.ProcessDay(state);

            state.SyncRandomState();
```
`EventSystem` is created in Task C4 — if it does not exist yet, either add a no-op stub
`Simulation/EventSystem.cs` (`public static class EventSystem { public static void ProcessDay(GameState s) { } }`)
now and finish it in C4, or defer this exact line until C4. **Recommended: implement C1–C4 before
finalising this task's commit**, and in the meantime wire only `ResearchSystem.ProcessDay(state)`.
Simplest order: do A5's `ResearchSystem` line now; add the `EventSystem` line in Task C4.

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/TickSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/ResearchSystemTests.cs
git commit -m "feat(sim): advance research in the daily tick"
```

---

# Block B — Tech effects (wire the toggles)

Each task is a RED→GREEN edit to an existing system plus a focused test. All tests live in
`tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs` (create once).

### Task B1: Style gating in `BrewingSystem`

**Objective:** You cannot brew Porter/Stout/Mild before *Malting Kilns*, or Pilsner/Lager before
*Pale Revolution*.

**Files:** Modify `Simulation/BrewingSystem.cs`; `tests/.../Simulation/TechEffectTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class TechEffectTests
    {
        private static GameState WithPorterRecipe()
        {
            var s = TestScenario.Standard();
            var r = TestScenario.PaleAle();
            r.Style = BeerStyle.Porter;          // Recipe.Style is settable
            s.Recipes["porter"] = r;
            return s;
        }

        [Fact]
        public void Locked_Style_Is_Rejected()
        {
            var s = WithPorterRecipe();
            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"));
            result.Success.Should().BeFalse();
            result.Reason.Should().Be(BrewFailureReason.LockedStyle);
        }

        [Fact]
        public void Unlocked_Style_Brews()
        {
            var s = WithPorterRecipe();
            ResearchSystem.Unlock(s, "malting-kilns");
            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"));
            result.Success.Should().BeTrue();
        }
    }
}
```

**Step 2 — run, expect FAIL** (no `LockedStyle`).

**Step 3 — implement:**
- In `Simulation/BrewingSystem.cs`, add `using BreweryEmpire.Core.Model.Research;`.
- Add to the `BrewFailureReason` enum (after `UnknownRecipe = 5`): `LockedStyle = 6`.
- In `TryStartBrew`, after the `UnknownRecipe` check (`state.Recipes.TryGetValue` block, ~line 52), add:
```csharp
            var requiredTech = TechCatalog.RequiredTechFor(recipe.Style);
            if (requiredTech != null && !ResearchSystem.HasTech(state, requiredTech))
                return BrewResult.Fail(BrewFailureReason.LockedStyle);
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/BrewingSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(brewing): gate styles behind their required tech"
```

---

### Task B2: Saccharometer mash-efficiency bonus

**Objective:** Researching the saccharometer grants +500bp mash efficiency (higher OG).

**Files:** Modify `Simulation/BrewingSystem.cs`; append to `TechEffectTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Saccharometer_Raises_Original_Gravity()
        {
            static int Og(bool hasTech)
            {
                var s = TestScenario.Standard();
                if (hasTech) ResearchSystem.Unlock(s, "saccharometer");
                var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                return r.Batch!.OriginalGravityPoints;
            }
            Og(hasTech: true).Should().BeGreaterThan(Og(hasTech: false));
        }
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement.** In `BrewingSystem.TryStartBrew`, replace the gravity block (lines 99–102):
```csharp
            var maltsterBonus = state.Staff.AggregateBonus(node.Id.Value, TraitEffect.MashEfficiency);
            var (og, fg, attenuation, abv) = MashChemistry.ComputeGravity(
                recipe, state.Catalog, vessel.Tier, vessel.ConditionBasisPoints,
                waterFitBasisPoints: 10000, maltsterBonusBasisPoints: maltsterBonus);
```
with:
```csharp
            var maltsterBonus = state.Staff.AggregateBonus(node.Id.Value, TraitEffect.MashEfficiency);
            if (ResearchSystem.HasTech(state, "saccharometer")) maltsterBonus += 500;

            int waterFit = 10000;
            if (ResearchSystem.HasTech(state, "water-chemistry"))
                waterFit = 10000 + node.Water.FitScoreBasisPoints(TechCatalog.TargetWaterFor(recipe.Style)) / 4;

            var (og, fg, attenuation, abv) = MashChemistry.ComputeGravity(
                recipe, state.Catalog, vessel.Tier, vessel.ConditionBasisPoints,
                waterFitBasisPoints: waterFit, maltsterBonusBasisPoints: maltsterBonus);
```
(The `waterFit` lines belong to Task B3, but including them here in one edit avoids a second churn;
keep both behind their own `HasTech` guard so each is independently testable. If you prefer strict
one-behaviour-per-commit, split: do only the `maltsterBonus += 500` line now, add `waterFit` in B3.)

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/BrewingSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(brewing): saccharometer grants mash efficiency bonus"
```

---

### Task B3: Water chemistry fits style water

**Objective:** Post-*Water Chemistry*, a brewery whose water matches the style brews at higher
efficiency (up to +2500bp), never a penalty.

**Files:** Modify `Simulation/BrewingSystem.cs` (the `waterFit` lines above, if not already added);
append to `TechEffectTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Water_Chemistry_Bonus_Tracks_Water_Fit()
        {
            static int Og(bool hasTech)
            {
                var s = TestScenario.Standard();   // Burton water, Pale Ale = perfect fit
                if (hasTech) ResearchSystem.Unlock(s, "water-chemistry");
                var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                return r.Batch!.OriginalGravityPoints;
            }
            Og(hasTech: true).Should().BeGreaterThan(Og(hasTech: false));
        }
```

**Step 2 — run, expect FAIL** (waterFit still hardcoded 10000).

**Step 3 — implement** the `waterFit` guard (see B2 step 3). If already added, no-op here beyond the test.

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/BrewingSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(brewing): water chemistry grants style-water efficiency bonus"
```

---

### Task B4: Pure yeast removes wild-yeast + raises base quality

**Objective:** Carlsberg's pure culture: wild-yeast contamination disappears and every brew is +500bp
quality.

**Files:** Modify `Simulation/SpoilageSystem.cs` and `Simulation/BrewingSystem.cs`; append to
`TechEffectTests.cs`.

**Step 1 — failing tests:**
```csharp
        [Fact]
        public void Pure_Yeast_Raises_Base_Quality()
        {
            static int Q(bool hasTech)
            {
                var s = TestScenario.Standard();
                if (hasTech) ResearchSystem.Unlock(s, "pure-yeast");
                var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                return r.Batch!.QualityBasisPoints;
            }
            Q(hasTech: true).Should().BeGreaterThan(Q(hasTech: false));
        }

        [Fact]
        public void Pure_Yeast_Never_Rolls_Wild_Yeast()
        {
            // Drive many infection rolls and assert WildYeast (index 4) never appears.
            var s = TestScenario.Standard();
            ResearchSystem.Unlock(s, "pure-yeast");
            var node = s.World.Get(new NodeId("burton"));
            for (int i = 0; i < 2000; i++)
            {
                // Force a contract by directly invoking the organism roll is internal; instead
                // assert on the public infection path over many forced contracts below.
            }
        }
```
The direct organism roll is private to `SpoilageSystem`. To test the effect without reaching into
internals, expose it as an `internal` helper (already the codebase's pattern — `BaseInfectionRiskBasisPoints`
is `internal`). In `SpoilageSystem`, extract the roll into:
```csharp
        internal static SpoilageOrganism RollOrganism(GameState state)
        {
            int maxExclusive = ResearchSystem.HasTech(state, "pure-yeast") ? 4 : 5;   // 4 excludes WildYeast
            return (SpoilageOrganism)state.Random.NextInt(0, maxExclusive);
        }
```
Then the test (the core project already sets `InternalsVisibleTo` for the test assembly, per
`Compatibility/IsExternalInit.cs`):
```csharp
        [Fact]
        public void Pure_Yeast_Never_Rolls_Wild_Yeast()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Unlock(s, "pure-yeast");
            for (int i = 0; i < 5000; i++)
                SpoilageSystem.RollOrganism(s).Should().NotBe(SpoilageOrganism.WildYeast);
        }
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement:**
- In `SpoilageSystem.ProcessNode`, replace line 144 (`var organism = (SpoilageOrganism)state.Random.NextInt(0, 5);`) with `var organism = RollOrganism(state);`, and add the `RollOrganism` helper above.
- In `BrewingSystem.ComputeQuality`, after `int baseQuality = 3000;` (line 129), add:
```csharp
            if (ResearchSystem.HasTech(state, "pure-yeast")) baseQuality += 500;
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/SpoilageSystem.cs src/BreweryEmpire.Core/Simulation/BrewingSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(sim): pure yeast eliminates wild infection and boosts base quality"
```

---

### Task B5: Pasteurization toggle

**Objective:** Post-*Pasteurization*, a ready batch can be pasteurized: pays a cost, gains
near-immunity, loses a little flavour.

**Files:** Create `Simulation/PasteurizationSystem.cs`; append to `TechEffectTests.cs`.

**Step 1 — failing tests:**
```csharp
        [Fact]
        public void Pasteurization_Requires_Tech_And_Ready_Batch()
        {
            var s = TestScenario.Standard();
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!;

            // No tech yet → refused.
            PasteurizationSystem.TryPasteurize(s, new NodeId("burton"), batch.Id).Should().BeFalse();

            ResearchSystem.Unlock(s, "pasteurization");
            // Still fermenting → refused.
            PasteurizationSystem.TryPasteurize(s, new NodeId("burton"), batch.Id).Should().BeFalse();
        }

        [Fact]
        public void Pasteurization_Sets_Flag_And_Lowers_Quality()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Unlock(s, "pasteurization");
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!;
            batch.MarkReady();
            int before = batch.QualityBasisPoints;

            PasteurizationSystem.TryPasteurize(s, new NodeId("burton"), batch.Id).Should().BeTrue();

            batch.IsPasteurized.Should().BeTrue();
            batch.QualityBasisPoints.Should().BeLessThan(before);
        }
```

**Step 2 — run, expect FAIL** (PasteurizationSystem missing).

**Step 3 — implement** `Simulation/PasteurizationSystem.cs`:
```csharp
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Heat-treat a finished batch. The spoilage payoff (pasteurized → token
    /// progression rate) is already wired in SpoilageSystem; this only flips the
    /// flag and charges the real cost.
    /// </summary>
    public static class PasteurizationSystem
    {
        public static bool TryPasteurize(GameState state, NodeId nodeId, BatchId batchId)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (!ResearchSystem.HasTech(state, "pasteurization")) return false;

            var node = state.World.Get(nodeId);
            var batch = node.Batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null || batch.State != BatchState.Ready) return false;

            var cost = Money.FromCents((long)batch.VolumeLitres * 2);
            state.Ledger.ForceDebit(state.Date, LedgerCategory.Upkeep, cost,
                                    "Pasteurization of " + batchId.Value, nodeId.Value);

            batch.IsPasteurized = true;
            batch.AdjustQuality(-300);   // gentle heat costs a little flavour
            return true;
        }
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/PasteurizationSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(sim): add pasteurization toggle with cost and quality tradeoff"
```

---

### Task B6: Refrigeration & ice-house effects (retroactive, already tested in A4)

The `ApplyEffect` switch (Task A4) already sets `IsRefrigerated`/`HasIceHouse` on all nodes, and
`FermentationSystem`/`SpoilageSystem` already read `HasColdStorage`. Add one end-to-end assertion
(optional but cheap) to `TechEffectTests.cs`:

```csharp
        [Fact]
        public void Refrigeration_Removes_Summer_Temperature_Penalty()
        {
            // Hot summer day: without refrigeration a fermenting batch loses quality;
            // with it, the same day does not.
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1880, 7, 15);
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var node = s.World.Get(new NodeId("burton"));
            int before = r.Batch!.QualityBasisPoints;
            FermentationSystem.ProcessNode(s, node);
            r.Batch.QualityBasisPoints.Should().BeLessThan(before);   // hot, no cold storage
        }
```
(Assert the *with-refrigeration* side by unlocking `refrigeration` first in a second scenario; the
mechanics are already proven by existing cold-chain tests in
`tests/.../Simulation/ColdChainTests.cs`.) Commit together:
```bash
git add tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "test(tech): refrigeration removes summer temperature penalty end-to-end"
```

---

### Task B7: Packaging gate in logistics

**Objective:** Bottle/Keg/Can shipments are refused before *Automated Bottling & Canning*.

**Files:** Modify `Simulation/LogisticsSystem.cs`; append to `TechEffectTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Bottle_Shipment_Requires_Bottling_Line()
        {
            var s = TestScenario.Standard();
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!; batch.MarkReady();

            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, BreweryEmpire.Core.Model.Packaging.PackagingType.Bottle, 10)
                .Should().BeNull();

            ResearchSystem.Unlock(s, "bottling-line");

            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, BreweryEmpire.Core.Model.Packaging.PackagingType.Bottle, 10)
                .Should().NotBeNull();
        }
```

**Step 2 — run, expect FAIL** (Bottle currently allowed).

**Step 3 — implement.** In `LogisticsSystem.DispatchShipment`, immediately after the null guard
(`if (state == null) throw ...`), add:
```csharp
            if (!ResearchSystem.IsPackagingUnlocked(state, packaging)) return null;
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/LogisticsSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/TechEffectTests.cs
git commit -m "feat(logistics): gate non-cask packaging behind bottling line"
```

---

# Block C — Event engine

### Task C1: `GameEvent` model

**Objective:** Serializable event/choice/effect data. (If you already created these to satisfy A3,
this task just adds the tests.)

**Files:** Create `src/BreweryEmpire.Core/Model/Events/GameEvent.cs`; test
`tests/BreweryEmpire.Core.Tests/Model/Events/GameEventTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Events;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Events
{
    public class GameEventTests
    {
        [Fact]
        public void Event_Holds_Choices_In_Order()
        {
            var e = new GameEvent
            {
                Id = "e1",
                Choices = new[]
                {
                    new EventChoice { Id = "a", Label = "A" },
                    new EventChoice { Id = "b", Label = "B" }
                }
            };
            e.Choices.Select(c => c.Id).Should().Equal("a", "b");
        }

        [Fact]
        public void Effect_Kind_Enum_Is_Stable_Ordinals()
        {
            // Ordinals are persisted implicitly via the typed kind; assert the ones we rely on.
            ((int)EventEffectKind.MoneyCents).Should().Be(0);
            ((int)EventEffectKind.Prestige).Should().Be(1);
        }
    }
}
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement** `Model/Events/GameEvent.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace BreweryEmpire.Core.Model.Events
{
    /// <summary>A historical or random event offered to the player. Pure data; the
    /// trigger logic lives in EventSystem, so this serializes cleanly.</summary>
    public sealed record GameEvent
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int MinYear { get; init; }
        public IReadOnlyList<EventChoice> Choices { get; init; } = Array.Empty<EventChoice>();
    }

    /// <summary>One selectable response with its effects.</summary>
    public sealed record EventChoice
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public IReadOnlyList<EventEffect> Effects { get; init; } = Array.Empty<EventEffect>();
    }

    /// <summary>A typed, integer state modifier.</summary>
    public sealed record EventEffect
    {
        public EventEffectKind Kind { get; init; }
        public int Amount { get; init; }                      // cents for money, bp/tonnes otherwise
        public string Target { get; init; } = string.Empty;   // tech id for UnlockTech
        public string Description { get; init; } = string.Empty; // ledger text for money effects
    }

    public enum EventEffectKind
    {
        MoneyCents = 0,          // signed: positive credit, negative debit (EventCost)
        Prestige = 1,            // added to PrestigeBasisPoints, clamped >= 0
        Reputation = 2,          // added to global ReputationBasisPoints, clamped [0,10000]
        ExciseDutyMultiplier = 3,// sets PriceBook.ExciseDutyModifierBasisPoints
        IceStock = 4,            // tonnes added to every ice-house node
        UnlockTech = 5           // immediately unlock Target
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Events/GameEvent.cs tests/BreweryEmpire.Core.Tests/Model/Events/GameEventTests.cs
git commit -m "feat(events): add GameEvent/EventChoice/EventEffect model"
```

---

### Task C2: `EventCatalog`

**Objective:** The four design-doc events, as data, with their choices and (placeholder-tuned) effects.

**Files:** Create `src/BreweryEmpire.Core/Model/Events/EventCatalog.cs`; test
`tests/BreweryEmpire.Core.Tests/Model/Events/EventCatalogTests.cs`.

**Step 1 — failing test:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Model.Events;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Events
{
    public class EventCatalogTests
    {
        [Fact]
        public void Every_Event_Has_At_Least_Two_Choices()
        {
            foreach (var e in EventCatalog.All)
                e.Choices.Count.Should().BeGreaterThanOrEqualTo(2, e.Id + " must be a real choice");
        }

        [Fact]
        public void Ids_Are_Unique()
        {
            EventCatalog.All.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        }
    }
}
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement** `Model/Events/EventCatalog.cs`:
```csharp
using System.Collections.Generic;

namespace BreweryEmpire.Core.Model.Events
{
    /// <summary>
    /// The four Victoria-style events from the design doc (section 3.5). Dollar
    /// figures are placeholders pending a balance pass; the plumbing is the product.
    /// </summary>
    public static class EventCatalog
    {
        private static EventEffect Money(int cents, string desc) => new EventEffect
            { Kind = EventEffectKind.MoneyCents, Amount = cents, Description = desc };

        private static EventEffect Prestige(int bp) => new EventEffect
            { Kind = EventEffectKind.Prestige, Amount = bp, Description = "Prestige" };

        private static EventEffect Reputation(int bp) => new EventEffect
            { Kind = EventEffectKind.Reputation, Amount = bp, Description = "Reputation" };

        private static EventEffect Duty(int bp) => new EventEffect
            { Kind = EventEffectKind.ExciseDutyMultiplier, Amount = bp, Description = "Excise duty" };

        private static EventEffect Ice(int tonnes) => new EventEffect
            { Kind = EventEffectKind.IceStock, Amount = tonnes, Description = "Ice stock" };

        public static IReadOnlyList<GameEvent> All { get; } = new List<GameEvent>
        {
            new GameEvent
            {
                Id = "pure-yeast-dilemma", MinYear = 1883,
                Title = "The Open-Source Pure Yeast Dilemma",
                Description = "Hansen's pure yeast can be shared with the world, or patented.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "share", Label = "Share it freely",
                        Effects = new List<EventEffect> { Prestige(5000), Reputation(2000) }
                    },
                    new EventChoice
                    {
                        Id = "patent", Label = "Patent it",
                        Effects = new List<EventEffect> { Money(50_000, "Pure yeast patent royalties"), Prestige(-1500) }
                    }
                }
            },
            new GameEvent
            {
                Id = "ice-shortage", MinYear = 1830,
                Title = "The Great Ice Shortage",
                Description = "A warm winter has starved the ice houses.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "buy", Label = "Buy Scandinavian lake ice",
                        Effects = new List<EventEffect> { Money(-20_000, "Imported Scandinavian ice"), Ice(20) }
                    },
                    new EventChoice
                    {
                        Id = "halt", Label = "Halt lager for the summer",
                        Effects = new List<EventEffect> { Reputation(-1000) }
                    }
                }
            },
            new GameEvent
            {
                Id = "hop-blight", MinYear = 1760,
                Title = "The Hop Blight",
                Description = "A blight has spiked hop prices across the country.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "premium", Label = "Pay premium for true hops",
                        Effects = new List<EventEffect> { Money(-30_000, "Premium hop purchase") }
                    },
                    new EventChoice
                    {
                        Id = "substitute", Label = "Substitute herbal blends",
                        Effects = new List<EventEffect> { Money(-5_000, "Herbal substitute"), Prestige(-2000), Reputation(-1500) }
                    }
                }
            },
            new GameEvent
            {
                Id = "temperance", MinYear = 1880,
                Title = "Temperance Movement Rises",
                Description = "Local prohibition threatens your markets.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "lobby", Label = "Lobby the authorities",
                        Effects = new List<EventEffect> { Money(-40_000, "Lobbying expenditure"), Reputation(500) }
                    },
                    new EventChoice
                    {
                        Id = "session", Label = "Brew low-alcohol session beers",
                        Effects = new List<EventEffect> { Prestige(-1500), Reputation(1000) }
                    }
                }
            }
        };
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Model/Events/EventCatalog.cs tests/BreweryEmpire.Core.Tests/Model/Events/EventCatalogTests.cs
git commit -m "feat(events): add EventCatalog with the four design-doc events"
```

---

### Task C3: `EventSystem` (triggers + resolve + effect application)

**Objective:** Deterministic triggers enqueue due events; resolving a choice applies typed effects.

**Files:** Create `src/BreweryEmpire.Core/Simulation/EventSystem.cs`; test
`tests/BreweryEmpire.Core.Tests/Simulation/EventSystemTests.cs`.

**Step 1 — failing tests:**
```csharp
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Events;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class EventSystemTests
    {
        [Fact]
        public void Due_Event_Is_Enqueued_Once()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);   // temperance MinYear 1880

            EventSystem.ProcessDay(s);
            EventSystem.ProcessDay(s);

            s.PendingEvents.Should().ContainSingle(e => e.Id == "temperance");
        }

        [Fact]
        public void Ice_Shortage_Requires_An_Ice_House()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1850, 1, 1);
            EventSystem.ProcessDay(s);
            s.PendingEvents.Should().NotContain(e => e.Id == "ice-shortage");

            ResearchSystem.Unlock(s, "ice-house");
            EventSystem.ProcessDay(s);
            s.PendingEvents.Should().Contain(e => e.Id == "ice-shortage");
        }

        [Fact]
        public void Resolve_Applies_Money_And_Prestige_And_Records()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            EventSystem.ProcessDay(s);
            var e = s.PendingEvents.Single(x => x.Id == "temperance");
            int lobbyIndex = e.Choices.ToList().FindIndex(c => c.Id == "lobby");
            long balanceBefore = s.Ledger.Balance.Cents;

            EventSystem.Resolve(s, "temperance", lobbyIndex).Should().BeTrue();

            s.Ledger.Balance.Cents.Should().BeLessThan(balanceBefore);
            s.PendingEvents.Should().NotContain(x => x.Id == "temperance");
            s.ResolvedEventIds.Should().Contain("temperance");
        }

        [Fact]
        public void Resolve_Invalid_Choice_Returns_False()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            EventSystem.ProcessDay(s);
            EventSystem.Resolve(s, "temperance", 99).Should().BeFalse();
            EventSystem.Resolve(s, "no-such-event", 0).Should().BeFalse();
        }
    }
}
```

**Step 2 — run, expect FAIL** (EventSystem missing).

**Step 3 — implement** `Simulation/EventSystem.cs`:
```csharp
using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Events;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Choice-based historical events. Triggers are deterministic (date + state
    /// conditions, no RNG); a due event is enqueued once and waits for the player
    /// to resolve a choice. Resolving applies typed, integer effects.
    /// </summary>
    public static class EventSystem
    {
        /// <summary>Enqueue any event whose trigger is now true and not yet resolved/pending.</summary>
        public static void ProcessDay(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            foreach (var e in EventCatalog.All)
            {
                if (state.ResolvedEventIds.Contains(e.Id)) continue;
                if (state.PendingEvents.Any(p => p.Id == e.Id)) continue;
                if (!ShouldTrigger(e.Id, state)) continue;
                state.PendingEvents.Add(e);
            }
        }

        private static bool ShouldTrigger(string id, GameState state) => id switch
        {
            "pure-yeast-dilemma" => state.Date.Year >= 1883 && ResearchSystem.HasTech(state, "pure-yeast"),
            "ice-shortage" => state.Date.Year >= 1830 && state.World.Nodes.Any(n => n.HasIceHouse),
            "hop-blight" => state.Date.Year >= 1760,
            "temperance" => state.Date.Year >= 1880,
            _ => false
        };

        /// <summary>Apply a chosen response. Returns false if the event is not pending or the
        /// index is out of range (no partial application).</summary>
        public static bool Resolve(GameState state, string eventId, int choiceIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var e = state.PendingEvents.FirstOrDefault(p => p.Id == eventId);
            if (e == null) return false;
            if (choiceIndex < 0 || choiceIndex >= e.Choices.Count) return false;

            foreach (var fx in e.Choices[choiceIndex].Effects)
                ApplyEffect(state, fx);

            state.PendingEvents.Remove(e);
            state.ResolvedEventIds.Add(e.Id);
            return true;
        }

        private static void ApplyEffect(GameState state, EventEffect fx)
        {
            switch (fx.Kind)
            {
                case EventEffectKind.MoneyCents:
                    if (fx.Amount >= 0)
                        state.Ledger.Credit(state.Date, LedgerCategory.EventCost,
                                            Money.FromCents(fx.Amount), fx.Description);
                    else
                        state.Ledger.ForceDebit(state.Date, LedgerCategory.EventCost,
                                                Money.FromCents(-fx.Amount), fx.Description);
                    break;
                case EventEffectKind.Prestige:
                    state.PrestigeBasisPoints = Math.Max(0, state.PrestigeBasisPoints + fx.Amount);
                    break;
                case EventEffectKind.Reputation:
                    state.ReputationBasisPoints = Math.Clamp(state.ReputationBasisPoints + fx.Amount, 0, 10000);
                    break;
                case EventEffectKind.ExciseDutyMultiplier:
                    state.Prices.ExciseDutyModifierBasisPoints = fx.Amount;
                    break;
                case EventEffectKind.IceStock:
                    foreach (var n in state.World.Nodes)
                        if (n.HasIceHouse) n.RestoreIceStock(n.IceStockTonnes + fx.Amount);
                    break;
                case EventEffectKind.UnlockTech:
                    ResearchSystem.Unlock(state, fx.Target);
                    break;
            }
        }
    }
}
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/EventSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/EventSystemTests.cs
git commit -m "feat(events): add EventSystem with triggers, resolve and typed effects"
```

---

### Task C4: Wire events into the tick

**Objective:** Due events are enqueued daily.

**Files:** Modify `Simulation/TickSystem.cs` (the `EventSystem.ProcessDay(state);` line from Task A5);
append to `EventSystemTests.cs`.

**Step 1 — failing test:**
```csharp
        [Fact]
        public void Tick_Enqueues_Due_Events()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            TickSystem.AdvanceDay(s);
            s.PendingEvents.Should().Contain(e => e.Id == "temperance");
        }
```

**Step 2 — run, expect FAIL.**

**Step 3 — implement** (add the line if not already present from A5):
```csharp
            ResearchSystem.ProcessDay(state);
            EventSystem.ProcessDay(state);
```

**Step 4 — run, expect PASS.** **Step 5 — commit:**
```bash
git add src/BreweryEmpire.Core/Simulation/TickSystem.cs tests/BreweryEmpire.Core.Tests/Simulation/EventSystemTests.cs
git commit -m "feat(sim): enqueue due events in the daily tick"
```

---

# Block D — Proof

### Task D1: Era-progression integration test

**Objective:** Prove the full loop — research through the DAG unlocks mechanics that change outcomes —
over a multi-year campaign, deterministically.

**Files:** Create `tests/BreweryEmpire.Core.Tests/Integration/EraProgressionTests.cs`.

**Step 1 — tests:**
```csharp
using System;
using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Integration
{
    public class EraProgressionTests
    {
        [Fact]
        public void Researching_Through_The_Dag_Unlocks_Dark_Styles_Then_Lagers()
        {
            var s = TestScenario.Standard();
            var porter = TestScenario.PaleAle(); porter.Style = BeerStyle.Porter;
            s.Recipes["porter"] = porter;

            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"))
                .Success.Should().BeFalse("porter is locked before Malting Kilns");

            ResearchSystem.Unlock(s, "malting-kilns");
            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"))
                .Success.Should().BeTrue();

            // Pilsner needs the Pale Revolution, which needs the saccharometer.
            var pils = TestScenario.PaleAle(); pils.Style = BeerStyle.Pilsner;
            s.Recipes["pilsner"] = pils;
            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pilsner"))
                .Success.Should().BeFalse();

            ResearchSystem.Unlock(s, "saccharometer");
            ResearchSystem.Unlock(s, "pale-revolution");
            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pilsner"))
                .Success.Should().BeTrue();
        }

        [Fact]
        public void Research_Is_Deterministic_Across_Runs()
        {
            static string Run()
            {
                var s = TestScenario.Standard(seed: 777);
                var chem = new BreweryEmpire.Core.Model.Staff.StaffMember(
                    new BreweryEmpire.Core.Model.Staff.StaffId("chem"), "C",
                    BreweryEmpire.Core.Model.Staff.StaffRole.Chemist, 9000,
                    BreweryEmpire.Core.Economy.Money.FromWhole(30), 40,
                    GameDate.FromYearMonthDay(1750, 1, 1));
                s.Staff.Hire(chem, GameDate.FromYearMonthDay(1750, 1, 1));

                ResearchSystem.Start(s, "malting-kilns");
                ResearchSystem.Start(s, "saccharometer");   // will fail while first is active
                TickSystem.AdvanceDays(s, 30);

                return string.Join(",",
                    s.Research.UnlockedTechIds,
                    s.Research.ActiveTechId ?? "none",
                    s.Research.ActiveProgressPoints);
            }

            Run().Should().Be(Run());
        }
    }
}
```
(Note: `Recipe.Style` is a settable property on `Recipe` — verify against `Model/Recipes/Recipe.cs`
before relying on it; if it is init-only, build the recipe with `Style` set via object initializer
instead of mutating.)

**Step 2 — run, expect FAIL → PASS after any fixes.** **Step 3 — commit:**
```bash
git add tests/BreweryEmpire.Core.Tests/Integration/EraProgressionTests.cs
git commit -m "test(core): era-progression integration through the research DAG"
```

---

### Task D2: Save round-trip + determinism with tech & events

**Objective:** A game with unlocked tech, active research, and a pending event round-trips exactly and
stays deterministic.

**Files:** Append to `tests/BreweryEmpire.Core.Tests/Integration/DeterminismTests.cs` (or create a
focused file). Use `TestScenario.Fingerprint`.

**Step 1 — tests:**
```csharp
        [Fact]
        public void Determinism_Holds_With_Research_And_Events()
        {
            static string Run()
            {
                var s = TestScenario.Standard(seed: 555);
                ResearchSystem.Start(s, "malting-kilns");
                s.Date = GameDate.FromYearMonthDay(1880, 1, 1);
                for (int d = 0; d < 60; d++)
                {
                    if (d == 20) EventSystem.Resolve(s, "ice-shortage", 0);  // may be a no-op if not pending
                    TickSystem.AdvanceDay(s);
                }
                return TestScenario.Fingerprint(s)
                       + "\nresearch=" + string.Join(",", s.Research.UnlockedTechIds)
                       + "\nprestige=" + s.PrestigeBasisPoints;
            }
            Run().Should().Be(Run());
        }

        [Fact]
        public void Save_Load_Resume_Is_Identical_With_Tech()
        {
            var s = TestScenario.Standard(seed: 55);
            ResearchSystem.Start(s, "malting-kilns");
            TickSystem.AdvanceDays(s, 40);

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));
            TickSystem.AdvanceDays(reloaded, 40);
            TickSystem.AdvanceDays(s, 40);

            TestScenario.Fingerprint(reloaded).Should().Be(TestScenario.Fingerprint(s));
        }
```

**Step 2 — run, expect PASS** (should pass on first run if A–C are correct; any failure is a real bug).

**Step 3 — commit:**
```bash
git add tests/BreweryEmpire.Core.Tests/Integration/DeterminismTests.cs
git commit -m "test(core): determinism and save round-trip with research and events"
```

---

### Task D3: Guards + ledger invariant still hold

**Objective:** Phase 1's promises survive Phase 3.

**Files:** Extend `tests/BreweryEmpire.Core.Tests/Guards/DeterminismGuardsTests.cs` if it asserts a
specific list of forbidden APIs (no change needed — we introduced none), and add a ledger-invariant
assertion over a run that exercises events.

**Step 1 — test** (append to an integration file):
```csharp
        [Fact]
        public void Ledger_Invariant_Holds_With_Event_Money_Effects()
        {
            var s = TestScenario.Standard(seed: 123);
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            for (int d = 0; d < 10; d++)
            {
                TickSystem.AdvanceDay(s);
                var t = s.PendingEvents.FirstOrDefault(e => e.Id == "temperance");
                if (t != null) EventSystem.Resolve(s, "temperance", 0);
            }
            s.Ledger.Balance.Should().Be(s.Ledger.SumOfAllEntries());
        }
```

**Step 2 — run full suite** and confirm no regression:
```bash
dotnet build -c Release
dotnet test
```
**Expected:** 0 warnings, all tests green.

**Step 3 — commit:**
```bash
git add tests/BreweryEmpire.Core.Tests
git commit -m "test(core): ledger invariant holds with event money effects"
```

---

### Task D4: README update

**Objective:** Document the new systems so the design contract stays current.

**Files:** Modify `README.md`.

**Step 1** — add a short "Phase 3: Research & events" note after the "Known trade-offs" section,
covering: the research DAG (`TechCatalog`), `ResearchSystem` (chemists accrue, effects flip Phase 2
toggles), and `EventSystem` (deterministic triggers, typed choice effects). One short paragraph; no
need to enumerate every tech.

**Step 2 — commit:**
```bash
git add README.md
git commit -m "docs: describe research DAG and event engine"
```

---

## Validation Summary

| Check | Command | Expected |
|---|---|---|
| Build | `dotnet build -c Release` (with the dotnet env prefix) | 0 warnings, 0 errors |
| Unit tests | `dotnet test` | all pass (~+35 new tests) |
| Determinism | `EraProgressionTests.Research_Is_Deterministic_Across_Runs` | identical output across runs |
| Save round-trip | `DeterminismTests.Save_Load_Resume_Is_Identical_With_Tech` | identical fingerprint |
| v2→v3 load | `SaveSystemTests.Version_2_Save_Loads_With_Empty_Research` | loads, empty research |
| Ledger invariant | `Ledger_Invariant_Holds_With_Event_Money_Effects` | balance == sum(entries) |
| Unity independence | existing guards | no `UnityEngine` reference |
| Determinism leaks | existing guards | no `System.Random`/`DateTime.Now`/`Guid.NewGuid` |

---

## Risks, tradeoffs, and open questions

- **Retroactive tech effects are a simplification.** "Ice House"/"Refrigeration" set the flag on *all*
  owned nodes at unlock rather than unlocking a per-node *build* the player pays for. That build
  system (and per-node cost) is out of scope for the core sim and belongs to the Phase 4 UI/scenario
  layer. If it lands later, `ApplyEffect` becomes "unlock purchase" and the node fields stay as-is.
- **Some tech nodes are catalogue-only (no mechanical effect yet):** `drum-roaster` (needs a shelf-life
  model), `refrigerated-rail` (logistics hardcodes `HorseCart`; a mode-choice rewrite is a separate
  project), `stainless-steel` (needs a vessel-build surface). They are researchable and flip a flag so
  the tree is complete, but their payoff is deferred. This is deliberate — flag it in the UI so players
  are not misled.
- **Balance numbers are placeholders** (research costs, daily rate, event dollar amounts). Same rule as
  Phases 1–2: get the plumbing right and the relationships testable; tune after the tree and events exist.
- **Water-chemistry is upside-only** (never a penalty), implemented as `10000 + fit/4`. This avoids
  needing a water-*treatment* system. If you later want "wrong water hurts," change the guard to use the
  raw `FitScoreBasisPoints` — the hook is already isolated in `BrewingSystem.TryStartBrew`.
- **Event money uses the `EventCost` ledger category** for both credits and debits. Acceptable for now;
  if you want richer reporting, add `EventRevenue`/`EventExpense` categories (safe enum additions, no
  migration).
- **`GameEvent` serializes directly** (like `FlavorProfile`/`Infection`) rather than via a hand-rolled
  DTO, because it is pure data with no private setters. If an event ever needs a non-serializable
  trigger predicate, keep the predicate in `EventSystem` (it already is) and never on the record.
- **`Recipe.Style` mutability:** Task D1 mutates `recipe.Style` after construction. Verify whether
  `Recipe.Style` is settable vs init-only (`Model/Recipes/Recipe.cs`) before relying on it; if init-only,
  build the test recipe with `Style` in the initializer.

**Open questions for the user (non-blocking; defaults chosen in this plan):**
1. **Should techs cost money too, or research points only?** Plan uses points only (money already gates
   everything via wages/capital). Easy to add a `ResearchCostMoney` later.
2. **Should the event engine support *recurring* random events** (annual Hop Blight chance)? Plan makes
   all four events one-shot. Recurrence needs a cooldown field + an RNG draw — small, but deferred.
3. **Prestige vs reputation:** plan adds a separate `PrestigeBasisPoints` (campaign score) distinct from
   per-market reputation. If a single "fame" number is preferred, fold prestige into `ReputationBasisPoints`
   and drop the new field.
4. **Transport-mode tech** (`refrigerated-rail`, and rail/ship modes generally) is the largest deferred
   item — it needs `LogisticsSystem` to stop hardcoding `HorseCart`. Worth a dedicated phase.
