# Phase 1: Pure Logic Foundation — Implementation Plan (Rev 2)

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Build a standalone, Unity-free C# class library (`BreweryEmpire.Core`) containing the full domain model — ingredients, water, vessels, recipes, batches, staff, packaging, logistics, and **money** — plus a deterministic `SimulationEngine` tick runner, with an xUnit suite proving determinism and correctness.

**Architecture:** .NET Standard 2.1 class library, **zero UnityEngine references**, consumed later by Unity as a compiled DLL. Simulation advances in discrete daily ticks. All mutable state lives in `GameState`; systems are stateless services operating on it. Randomness flows through one seeded, serializable PRNG. Unity subscribes to the engine's event stream in Phase 4 — Phase 1 only emits the events.

**Tech Stack:** C# 9 / .NET Standard 2.1 (library), .NET 8 (tests), xUnit + FluentAssertions, `dotnet` CLI.

---

## Revision 2 — What Changed and Why

Rev 1 modelled brewing as "consume ingredients → wait N ticks → beer appears." That's a factory, not a brewery. Rev 2 adds the decision points that make brewing a *game*, plus the economic spine that motivates all of it.

**Added:**
- **Money as a first-class system** (Tasks 3, 20). Previously a single `TreasuryCents` field with nothing touching it. Now a full ledger: capital costs, wages, ingredient purchasing, excise duty, loans, interest, and bankruptcy. This is the primary driver of player motivation and the constraint every other system pushes against.
- **Staff management** (Task 12). Hireable brewmasters, coopers, draymen, chemists, and salesmen — including historical figures — with bonuses that land on specific, already-existing mechanics.
- **Quality *and* FlavorProfile** as orthogonal axes (Tasks 10, 11). `FlavorProfile` = what you *designed*. `Quality` = how well it was *executed* (brewmaster skill, equipment tier, luck). Market fit = profile match × quality.
- **Ingredient lots** with harvest year, diastatic power, and alpha acid (Task 4).
- **Water as a property of place** (Task 5) — Burton gypsum vs. Pilsen soft water makes brewery siting strategic.
- **Vessel occupancy** (Task 7) — fermenters are the real bottleneck; lagers cost you by tying up tanks.
- **Mash schedule + hop addition schedule** on recipes (Task 10) — real dials with opposed outcomes.
- **Typed infections** replacing the freshness counter (Task 19) — with hop bitterness and ABV as genuine defenses, linking recipe design to shipping range.
- **Packaging and cask return logistics** (Tasks 9, 18) — empty containers flow *backwards* through the network.
- **Regional seasonal temperature** (Task 5) — without it, refrigeration and ice-house tech unlock nothing.

**Explicitly excluded (per review):**
- **Batch stage timeline.** Kept the minimal `BatchStage` (`Fermenting / Ready / Spoiled`). Vessels carry a plain `DaysRemaining` counter instead of a stage machine. Lagers still tie up tanks longer via recipe duration, so the strategic cost survives without extra states.
- Malting as gameplay — buy malt from maltsters; the interesting decisions are downstream.
- Per-hop-variety oil chemistry — alpha acid alone carries the gameplay weight.

**Deferred to Phase 2** (structures exist here, math lands later): mash efficiency formula, IBU/SRM/ABV calculation, attenuation curves, infection probability tuning, flavor→market-fit scoring, dynamic demand.

---

## Current Context / Assumptions

- Repo: `C:\Users\Child\OneDrive\Projects\brewing-empire` — `IDEA.md`, `README.md`, one commit (`da6a4e5`). No Unity project yet.
- **Blocker:** `dotnet` host is on PATH but **no .NET SDK is installed**. Task 0 fixes this.
- Unity is not needed for Phase 1 — the point is proving the sim runs headless.
- Library targets `netstandard2.1` (Unity 2021.2+). Tests target `net8.0`.
- **1 tick = 1 day.** Committed. Hourly would 24× the tick count for day-scale logistics.
- **Integer-only state.** Litres, grams, cents, and basis points (0–10000). No floats in serialized state — makes cross-platform determinism and exact save round-trips free.

## Repository Layout

```
brewing-empire/
├── BreweryEmpire.sln
├── src/
│   ├── BreweryEmpire.Core/
│   │   ├── Model/
│   │   │   ├── Ingredients/   Ingredient, IngredientLot, Inventory
│   │   │   ├── Sites/         WaterProfile, RegionClimate, Vessel
│   │   │   ├── Brewing/       Recipe, MashSchedule, HopAddition,
│   │   │   │                  FlavorProfile, Batch, Infection
│   │   │   ├── Staff/         StaffMember, StaffRole, Trait, Roster
│   │   │   ├── Packaging/     PackagingType, ContainerPool, Shipment
│   │   │   ├── Nodes/         Node, Brewery, Warehouse, Market
│   │   │   └── Logistics/     Route, TransportMode, LogisticsGraph
│   │   ├── Economy/           Money, Ledger, Loan, PriceBook
│   │   ├── State/             GameState, GameDate, DeterministicRandom
│   │   ├── Simulation/        ISimulationSystem, SimulationEngine,
│   │   │                      SimulationContext, Systems/
│   │   └── Events/            ISimulationEvent, SimulationEventBus
│   └── BreweryEmpire.Headless/    console runner
└── tests/
    └── BreweryEmpire.Core.Tests/
```

## Task Blocks

| Block | Tasks | Theme |
|---|---|---|
| A — Foundations | 0–3 | SDK, calendar, RNG, money primitives |
| B — Domain Model | 4–13 | Ingredients, sites, vessels, recipes, batches, staff, state |
| C — Simulation | 14–20 | Engine, production, fermentation, logistics, spoilage, economy |
| D — Proof | 21–22 | Integration scenario, CI, guards |

Each task is one TDD cycle: failing test → run and see it fail → minimal implementation → run and see it pass → commit.

---

# Block A — Foundations

### Task 0: Install .NET SDK and scaffold solution

**Objective:** Working `dotnet build` / `dotnet test` from repo root.

**Steps:**
1. `winget install Microsoft.DotNet.SDK.8` — verify `dotnet --list-sdks` shows `8.x`.
2. `dotnet new sln -n BreweryEmpire`
3. `dotnet new classlib -o src/BreweryEmpire.Core -f netstandard2.1`
4. `dotnet new xunit -o tests/BreweryEmpire.Core.Tests -f net8.0`
5. `dotnet sln add src/BreweryEmpire.Core tests/BreweryEmpire.Core.Tests`
6. `dotnet add tests/BreweryEmpire.Core.Tests reference src/BreweryEmpire.Core`
7. `dotnet add tests/BreweryEmpire.Core.Tests package FluentAssertions`
8. `.gitignore` covering dotnet + Unity (`Library/`, `Temp/`, `obj/`, `bin/`, `*.csproj.user`).
9. In the csproj: `<LangVersion>9.0</LangVersion>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
10. Delete template `UnitTest1.cs`. Verify `dotnet test` runs clean.
11. Commit: `chore: scaffold BreweryEmpire.Core solution`

**Pitfall:** `netstandard2.1` caps you at C# 9 for safety — records and `init` are fine, file-scoped namespaces are not.

---

### Task 1: `GameDate` value type

**Objective:** Integer-day calendar with season and era derivation.

**Files:** `State/GameDate.cs`, `tests/State/GameDateTests.cs`

**Test first:**
```csharp
[Fact] public void Epoch_Is_1750_01_01() =>
    GameDate.FromTotalDays(0).Year.Should().Be(1750);

[Theory]
[InlineData(0, Season.Winter)]
[InlineData(180, Season.Summer)]
public void Season_Derives_From_DayOfYear(int days, Season expected) =>
    GameDate.FromTotalDays(days).Season.Should().Be(expected);

[Fact] public void AddDays_Is_Pure() {
    var d = GameDate.FromTotalDays(10);
    d.AddDays(5).TotalDays.Should().Be(15);
    d.TotalDays.Should().Be(10);
}
```

**Implementation:** `readonly struct GameDate` over `int TotalDays`. Simplified 365-day year, no leap years — document the simplification. `Season` from `DayOfYear`. Implement `IEquatable`, `IComparable`, `ToString()` → `"1750-01-01"`. Add `Era` derivation (Pre-Industrial / Industrial / Scientific / Modern) matching the design doc's four eras.

**Commit:** `feat(core): add GameDate integer calendar`

---

### Task 2: `DeterministicRandom`

**Objective:** Seeded, serializable, platform-stable PRNG.

**Test first:**
```csharp
[Fact] public void Same_Seed_Yields_Same_Sequence() {
    var a = new DeterministicRandom(12345);
    var b = new DeterministicRandom(12345);
    Enumerable.Range(0,100).Select(_ => a.NextInt(0,1000))
      .Should().Equal(Enumerable.Range(0,100).Select(_ => b.NextInt(0,1000)));
}

[Fact] public void State_Round_Trips() { /* serialize State, restore, next draw matches */ }
[Fact] public void NextInt_Respects_Bounds() { /* 10k draws all within [0,10) */ }
[Fact] public void Chance_BasisPoints_Is_Unbiased() { /* 5000bp ≈ 50% over 100k draws, ±1% */ }
```

**Implementation:** xorshift128+ over two `ulong`s. Expose `ulong[] State` and `FromState`. `NextInt(min, maxExclusive)` uses **rejection sampling** — modulo bias would quietly corrupt balance tuning. Add `bool Chance(int basisPoints)` since infection and quality rolls need it constantly. Integers only, no `double` API.

**Commit:** `feat(core): add deterministic seeded PRNG`

---

### Task 3: `Money`, `Ledger`, and `PriceBook`

**Objective:** The economic spine. Every later system posts to this.

**Why first:** Money is the main driver of player motivation and the constraint every other system pushes against. Building it early means production, logistics, staff, and spoilage all wire into a real ledger instead of being retrofitted.

**Files:** `Economy/Money.cs`, `Economy/Ledger.cs`, `Economy/LedgerEntry.cs`, `Economy/PriceBook.cs`, tests.

**Design:**
```csharp
public readonly struct Money {          // wraps long cents
    public long Cents { get; }
    public static Money FromCents(long c);
    public static Money operator +(Money a, Money b);
    public static Money operator -(Money a, Money b);
    public static Money operator *(Money a, int qty);
    public Money PercentBasisPoints(int bp);   // integer-safe scaling
    public bool IsNegative { get; }
}

public enum LedgerCategory {
    IngredientPurchase, Wages, Upkeep, CapitalExpenditure,
    TransportCost, ExciseDuty, LoanPrincipal, LoanInterest,
    BeerSales, ByproductSales, SpoilageWriteOff, EventCost
}

public sealed class LedgerEntry {
    GameDate Date; LedgerCategory Category; Money Amount;   // signed
    string Description; string? NodeId;
}

public sealed class Ledger {
    Money Balance { get; }
    void Post(GameDate date, LedgerCategory cat, Money amount, string desc, string? nodeId = null);
    bool TryDebit(...);                       // false if insufficient funds
    Money TotalFor(LedgerCategory cat, GameDate from, GameDate to);
    IReadOnlyList<LedgerEntry> EntriesForPeriod(GameDate from, GameDate to);
}
```
`PriceBook` holds base prices for ingredients, transport, wages, and equipment, plus an `ExciseDutyPerLitreCents` that varies by era (rises sharply in wartime — a Phase 3 event hook).

**Tests:**
- `Post` of a credit then a debit yields the correct balance.
- `TryDebit` beyond balance returns `false` and posts nothing (**no partial state mutation**).
- `Money` arithmetic never silently truncates; `PercentBasisPoints(2500)` on 100 cents = 25 cents.
- `TotalFor` filters by category and inclusive date range.
- Ledger is bounded: entries older than N days roll into a monthly summary (prevents an unbounded save file over a 100-year campaign — **do this now, it is painful later**).

**Pitfall:** Never let a system mutate `Balance` directly. Everything goes through `Post`, so the player can always be shown *why* they're broke. This is the single most important debuggability decision in the economy.

**Commit:** `feat(economy): add Money, Ledger and PriceBook`

# Block B — Domain Model

### Task 4: Ingredients and `IngredientLot`

**Objective:** Typed ingredients with the properties that actually drive brewing outcomes.

**Design:**
```csharp
public enum IngredientType { BaseMalt, SpecialtyMalt, RoastedGrain, Hops, Yeast, Adjunct, Finings }

public sealed record Ingredient {
    string Id; IngredientType Type; string DisplayName;
    Money BasePrice;                    // per kg
    int DiastaticPowerLintner;          // 0 for roasted/crystal — the key constraint
    int ColorLovibond;                  // malt colour contribution
    int YieldBasisPoints;               // extract potential
    int AlphaAcidBasisPoints;           // hops only
    int ShelfLifeDays;
    string? RegionId;                   // agricultural provenance
}

public sealed class IngredientLot {
    LotId Id; string IngredientId;
    int QuantityGrams;
    int HarvestYear;
    int QualityBasisPoints;             // varies by harvest — good/bad years
    GameDate AcquiredOn;
    Money UnitCostPaid;                 // for COGS accounting
    int EffectiveAlphaAcidBasisPoints(GameDate now);  // hops degrade with age
}
```

**Why lots:** fungible piles can't carry a bad harvest into a specific batch. Lots make traceability, disasters, and honest cost accounting possible for one extra class.

**Tests:**
- Roasted grain has `DiastaticPowerLintner == 0`.
- Negative quantity throws `ArgumentOutOfRangeException`.
- Removing more than available throws; the lot is unchanged.
- Hop alpha acid degrades monotonically with age and never goes below zero.
- `Inventory` consumes lots **oldest-first (FIFO)**, deterministically, ties broken by `LotId` ordinal.

**Commit:** `feat(core): add ingredients with diastatic power and lot tracking`

---

### Task 5: `WaterProfile` and `RegionClimate`

**Objective:** Make place matter — water chemistry and seasonal temperature.

**Design:**
```csharp
public sealed record WaterProfile {
    int CalciumPpm; int SulfatePpm; int ChloridePpm;
    int CarbonatePpm; int MagnesiumPpm; int SodiumPpm;
    int SulfateChlorideRatioBasisPoints { get; }   // derived: >2:1 crisp/bitter, <1:1 malty/round
    bool IsSoft { get; }                           // low carbonate → pale lagers
}

public sealed record RegionClimate {
    string RegionId;
    int[] MonthlyAvgTempCelsius;    // length 12
    int AmbientTempOn(GameDate d);
    bool SupportsWinterIceHarvest { get; }
}
```

Ship historical presets: **Burton-on-Trent** (gypsum-heavy, sulfate ~600ppm — pale ale), **Pilsen** (very soft, carbonate ~15ppm — pilsner), **Munich** (carbonate-rich — dark lager), **Dublin** (carbonate-rich — stout), **London** (moderate carbonate — porter).

**Why this matters:** ambient temperature is load-bearing for the whole Era 1–3 arc. "Brewing was seasonal until refrigeration" is your central historical story — without a temperature curve, the Ice House and Linde refrigeration tech nodes unlock nothing.

**Tests:**
- Burton preset reports a sulfate:chloride ratio above 2:1; Pilsen reports `IsSoft == true`.
- `AmbientTempOn` is continuous across a year boundary and deterministic.
- A northern region supports winter ice harvest; a Mediterranean one does not.

**Commit:** `feat(core): add water profiles and regional climate`

---

### Task 6: `Vessel` and equipment tiers

**Objective:** Fermenters as a discrete, occupiable, capital-cost resource.

**Design:**
```csharp
public enum VesselType { MashTun, Copper, OpenFermenter, ClosedFermenter, ConditioningTank, StainlessTank }
public enum EquipmentTier { Wooden, Copper, Iron, Stainless }

public sealed class Vessel {
    VesselId Id; VesselType Type; EquipmentTier Tier;
    int CapacityLitres;
    Money PurchaseCost; Money DailyUpkeep;
    int HygieneBasisPoints;        // rises with tier — drives infection resistance
    int ConditionBasisPoints;      // degrades with use, restored by maintenance spend
    BatchId? OccupiedBy;           // null = free
    int DaysRemaining;             // plain counter, per the no-stage-machine decision
    bool IsAvailable { get; }
}
```

**Why vessels:** fermenters are the real bottleneck in a brewery. `CapacityLitresPerTick` hid that entirely. Discrete vessels occupied for a duration make capacity planning the interesting problem it should be — and they're *why* lagers are expensive: long conditioning ties up tanks you've paid for. Retrofitting this later would mean rewriting `ProductionSystem`, so it goes in now.

**Tests:**
- Occupying a busy vessel throws.
- `DaysRemaining` decrements to zero and the vessel frees itself exactly once.
- Stainless has strictly higher `HygieneBasisPoints` than wooden.
- Condition degrades per use; maintenance spend restores it and posts to the ledger.
- A brewery with 3 fermenters cannot start a 4th concurrent batch.

**Commit:** `feat(core): add vessels with occupancy and equipment tiers`

---

### Task 7: `Recipe`, `MashSchedule`, `HopAddition`, `FlavorProfile`

**Objective:** The player's design surface — real dials with opposed outcomes.

**Design:**
```csharp
public sealed record MashSchedule {
    int TemperatureCelsius;    // ~63°C → fermentable, dry, higher ABV, thin body
    int DurationMinutes;       // ~70°C → dextrinous, sweet, full body, lower ABV
    MashMethod Method;         // Infusion | Decoction
}

public sealed record HopAddition {
    string IngredientId; int Grams;
    int MinutesFromEnd;        // 60 = bitterness, 5 = aroma, 0 = whirlpool
}

public sealed record FlavorProfile {          // all basis points — the DESIGN axis
    int Bitterness; int Body; int Sweetness;
    int Maltiness; int Clarity; int Sourness;
    int DistanceTo(FlavorProfile other);      // for Phase 2 market fit
}

public enum OffFlavor { Diacetyl, DimethylSulfide, Acetaldehyde, Phenolic, Sour, Oxidized, Vinegary }

public sealed record Recipe {
    RecipeId Id; string Name; BeerStyle Style;
    IReadOnlyList<GristComponent> Grist;      // ingredientId + proportion bp
    MashSchedule Mash;
    IReadOnlyList<HopAddition> Hops;
    int TargetOriginalGravityPoints;
    int TargetAbvBasisPoints;
    int FermentationDays; int ConditioningDays;
    WaterProfile? TargetWater;
    int TotalDiastaticPower(IngredientCatalog c);
    bool HasSufficientDiastaticPower(IngredientCatalog c);   // the >30% specialty malt trap
}
```

**Gravity is deliberately hidden pre-tech.** `OriginalGravity` / `FinalGravity` exist on `Batch` from day one, but the saccharometer tech node (Era 2) is what *reveals* them to the player. A tech that grants **information** rather than a stat bonus is the best educational hook in the design — build for it now by keeping the fields separate from any "observed" projection.

**Tests:**
- A grist of 100% roasted barley fails `HasSufficientDiastaticPower`.
- A grist of 80% base + 20% roasted passes.
- Recipes are immutable — mutating the source grist list after construction does not change the recipe.
- `FlavorProfile.DistanceTo` is symmetric and zero against itself.
- Hop additions sort deterministically by `MinutesFromEnd` then ingredient id.

**Commit:** `feat(core): add recipes, mash and hop schedules, flavor profiles`

---

### Task 8: `Batch` with `Quality` and `Infection`

**Objective:** A brewed lot carrying both design and execution outcomes.

**Design:**
```csharp
public sealed class Batch {
    BatchId Id; RecipeId RecipeId; string BreweryId;
    int VolumeLitres; GameDate BrewedOn;
    BatchStage Stage;                     // Fermenting | Ready | Spoiled — minimal, per decision

    // EXECUTION axis — how well was it made
    int QualityBasisPoints;
    // DESIGN axis — what was aimed for, as achieved
    FlavorProfile Profile;
    List<OffFlavor> OffFlavors;

    int OriginalGravityPoints; int FinalGravityPoints;
    int AbvBasisPoints; int IbuTenths;
    string YeastStrainId; int YeastGeneration;
    Infection? Infection;
    PackagingType Packaging; bool IsPasteurized;
    Money ProductionCost;                 // COGS carried from lots + wages + upkeep
}
```

**Quality vs. FlavorProfile — the split that keeps them non-redundant:**
- `FlavorProfile` answers *"what did you design?"* — set by recipe parameters.
- `Quality` answers *"how well did you execute it?"* — driven by **brewmaster skill, equipment tier, vessel condition, and a seeded roll**.
- Phase 2 market fit = `profile match × quality`. A perfectly-designed recipe brewed badly still sells poorly, and a crude recipe brewed excellently is respectable. Both axes stay meaningful.

**Quality roll (implemented here, tuned in Phase 2):**
```
base 5000bp
  + brewmaster skill contribution (up to +2500)
  + equipment tier bonus (Wooden 0 → Stainless +1500)
  + vessel condition scaling
  + water fit bonus
  - diastatic shortfall penalty
  ± seeded variance (±500, narrowed by brewmaster consistency trait)
clamped [0, 10000]
```

**Tests:**
- Same seed + same inputs → identical quality (determinism).
- A master brewmaster yields strictly higher expected quality than no brewmaster over 1000 seeded rolls.
- Stainless beats wooden at equal skill.
- Quality clamps at both bounds and never escapes [0, 10000].
- A batch with `Infection` progressed to threshold reports `Stage == Spoiled`.
- `ProductionCost` equals the sum of consumed lot costs plus apportioned wages.

**Commit:** `feat(core): add batch model with quality and flavor axes`

---

### Task 9: `PackagingType` and `ContainerPool`

**Objective:** Containers as owned, returnable, losable assets.

**Design:**
```csharp
public enum PackagingType { WoodenCask, Bottle, MetalKeg, Can }

public sealed record PackagingSpec {
    PackagingType Type; int CapacityLitres;
    Money UnitCost; bool IsReturnable;
    int ShelfLifeDays;                    // cask short, bottle long
    int OxygenIngressBasisPoints;         // drives oxidation/Acetobacter risk
    int UllageLossBasisPointsPerLeg;      // wooden casks leak/evaporate
}

public sealed class ContainerPool {
    PackagingType Type;
    int OwnedTotal; int InUse; int AtCustomerSites; int Lost;
    int Available { get; }
    Money ReplacementCost;
}
```

**Cask return is a genuine logistics mechanic.** Empties must flow *backwards* through the network. Coopers were a major brewery expense, and lost barrels cost real money. This gives the logistics graph bidirectional flow — considerably more interesting than one-way shipping, and it creates a recurring capital drain the player must actively manage.

**Tests:**
- Shipping beer decrements `Available` and increments `InUse`.
- A returned cask restores availability minus a seeded loss rate.
- Running out of containers **blocks shipment** and emits `ShipmentBlockedNoContainers` — it does not silently teleport beer.
- Ullage loss reduces delivered volume per leg; bottles lose none.

**Commit:** `feat(core): add packaging types and container pools`

---

### Task 10: Staff — `StaffMember`, `StaffRole`, `Roster`

**Objective:** Hireable people whose bonuses land on specific existing mechanics.

**Design:**
```csharp
public enum StaffRole {
    Brewmaster,     // batch quality, off-flavor avoidance
    Cooper,         // cask durability, container loss reduction
    Drayman,        // transit speed, spoilage in transit
    Chemist,        // unlocks/accelerates research, water adjustment accuracy
    Maltster,       // mash efficiency, ingredient yield
    Salesman,       // market price realized, demand capture
    Foreman         // vessel condition upkeep, wage efficiency
}

public sealed record StaffTrait {
    string Id; string DisplayName; string Description;
    StaffRole AppliesTo;
    int MagnitudeBasisPoints;
    TraitEffect Effect;    // enum: QualityBonus, ConsistencyBonus, InfectionResistance,
                           // TransitSpeed, ContainerLossReduction, ResearchRate,
                           // MashEfficiency, PriceRealization, UpkeepReduction, WageDiscount
}

public sealed class StaffMember {
    StaffId Id; string Name; StaffRole Role;
    int SkillBasisPoints;              // 0–10000
    Money MonthlyWage;
    int Age; GameDate HiredOn;
    string? AssignedNodeId;            // null = unassigned, still paid
    IReadOnlyList<StaffTrait> Traits;
    bool IsHistoricalFigure;
    int LoyaltyBasisPoints;            // falls if underpaid or unpaid; can quit
}

public sealed class Roster {
    IReadOnlyList<StaffMember> All;
    IEnumerable<StaffMember> AtNode(string nodeId);
    StaffMember? BestFor(string nodeId, StaffRole role);   // deterministic tie-break by StaffId
    Money TotalMonthlyWages { get; }
    int AggregateBonus(string nodeId, TraitEffect effect); // sums applicable traits
}
```

**Where each bonus lands — every effect maps to a mechanic that already exists in this plan:**

| Role | Effect | Lands on |
|---|---|---|
| Brewmaster | QualityBonus, ConsistencyBonus | Task 8 quality roll — raises mean, narrows variance |
| Brewmaster | InfectionResistance | Task 19 infection roll |
| Maltster | MashEfficiency | Task 16 wort yield — actual OG vs. target |
| Chemist | ResearchRate, water accuracy | Phase 3 tech DAG; Task 5 water fit bonus |
| Cooper | ContainerLossReduction | Task 9 cask return loss rate |
| Drayman | TransitSpeed | Task 18 shipment `DaysRemaining` |
| Salesman | PriceRealization | Task 20 sale price achieved |
| Foreman | UpkeepReduction | Task 6 vessel condition decay + daily upkeep cost |

**Historical figures as hireable/recruitable characters** (each ties to a tech node in your design doc — hiring them should discount or accelerate that research, making them feel like a real strategic acquisition rather than a stat stick):

| Figure | Role | Signature trait | Ties to |
|---|---|---|---|
| Emil Christian Hansen | Chemist | Pure yeast isolation — eliminates wild-yeast infection class | Carlsberg 1883 node |
| Louis Pasteur | Chemist | Pasteurization research; large infection resistance | Pasteurization node |
| Carl von Linde | Chemist | Mechanical refrigeration — removes seasonal temp penalty | Linde 1873 node |
| Gabriel Sedlmayr II | Brewmaster | Lager mastery; espionage-derived efficiency | Bohemian Pale Revolution |
| Anton Dreher | Brewmaster | Vienna lager; pale malt kilning bonus | Era 2 malting |
| Josef Groll | Brewmaster | Pilsner specialist; huge bonus on soft water | Pilsen siting |
| Arthur Guinness | Foreman | Long-lease shrewdness; upkeep and rent reduction | Era 1–2 expansion |
| Adolphus Busch | Salesman | Refrigerated rail distribution; price realization at range | Refrigerated rail node |

Historical figures should be **era-gated** (unavailable before their historical floruit), **expensive**, and **exclusive** — one per campaign, and a rival may hire them first. That last part gives the AI competitors something to actually compete *over*.

**Wages are a real cost.** Staff are paid monthly from the ledger whether or not they're productively assigned. Unpaid wages drop `LoyaltyBasisPoints`; at zero the staff member quits. This makes over-hiring during expansion a genuine trap — exactly the kind of pressure a tycoon game needs.

**Tests:**
- `AggregateBonus` sums only traits matching the role, node, and effect.
- `BestFor` is deterministic under equal skill (tie-break by `StaffId` ordinal).
- Unassigned staff still accrue wages.
- Loyalty falls on a missed payroll and the member quits at zero, emitting `StaffResigned`.
- A historical figure cannot be hired before their era-gate date.
- Hiring the same historical figure twice throws.

**Commit:** `feat(core): add staff roster, roles, traits and historical figures`

---

### Task 11: Node hierarchy

**Objective:** Breweries, warehouses, and markets — now with sites, vessels, and staff.

**Design:**
- `NodeId` / `RegionId` as `readonly struct` wrappers over `string` — prevents id mix-ups once the graph grows.
- `abstract class Node { NodeId Id; string Name; RegionId Region; Inventory Inventory; Money DailyUpkeep; }`
- `Brewery : Node` — `WaterProfile Water`, `List<Vessel> Vessels`, `Recipe? CurrentRecipe`, `EquipmentTier Tier`, `Money PurchasePrice`.
- `Warehouse : Node` — `int CapacityLitres`, `bool IsRefrigerated`, `bool HasIceHouse`, `int IceStockTonnes`.
- `Market : Node` — `int Population`, `Dictionary<BeerStyle,int> DemandLitresPerTick`, `int ReputationBasisPoints`, `Money PricePerLitre`.

**Market reputation** is how spoilage bites back: bad beer arriving doesn't just vanish, it damages your brand in that city and depresses future prices. That's the feedback loop that makes cold-chain investment feel *necessary* rather than optional.

**Tests:** brewery rejects a batch when no vessel is free; warehouse `RemainingCapacity` is correct; node equality is by `Id`; market reputation clamps [0, 10000].

**Commit:** `feat(core): add brewery, warehouse and market nodes`

---

### Task 12: `Route` and `LogisticsGraph`

**Objective:** Directed weighted graph with deterministic pathfinding.

**Design:**
- `TransportMode` enum: `HorseCart, Canal, SteamRail, CargoShip` — each with `DaysPerDistanceUnit`, `CostCentsPerLitrePerUnit`, `SpoilageModifierBasisPoints`, `IsRefrigeratedCapable`.
- `Route` record: `FromNodeId`, `ToNodeId`, `DistanceUnits`, `Mode`, computed `TransitDays` and `CostPerLitre`.
- `LogisticsGraph`: adjacency list; `AddNode`, `AddRoute`, `TryGetRoute`, `FindCheapestPath`, `FindFastestPath` (Dijkstra).

**Tests:**
- Route referencing an unknown node throws.
- Cheapest path prefers a 2-hop canal chain over a 1-hop expensive cart route.
- Fastest path prefers rail even when pricier.
- Disconnected nodes return null/empty rather than throwing.
- Duplicate route (same from/to/mode) throws.

**Pitfall:** Dijkstra tie-breaking **must** be deterministic — when two paths tie, break by node id ordinal. Otherwise `Dictionary` enumeration order leaks into the simulation and the determinism tests flake intermittently, which is a miserable bug to chase.

**Commit:** `feat(core): add routes and logistics graph pathfinding`

---

### Task 13: `GameState` aggregate + JSON round-trip

**Objective:** One serializable root holding all mutable state.

**Design:**
```csharp
public sealed class GameState {
    GameDate Date; long TickCount;
    Ledger Ledger;                      // replaces the bare TreasuryCents
    DeterministicRandom Random;
    LogisticsGraph Graph;
    Roster Staff;
    IngredientCatalog Catalog;
    Dictionary<string, RegionClimate> Climates;
    Dictionary<PackagingType, ContainerPool> Containers;
    List<Shipment> ShipmentsInTransit;
    List<Batch> AllBatches;
    Dictionary<string, Recipe> Recipes;
    HashSet<string> UnlockedTechIds;    // Phase 3 fills this
    List<Loan> Loans;
    int PrestigeBasisPoints;
}
```
Plus `GameState.NewGame(int seed, ScenarioDefinition scenario)`.

**Tests:**
- `NewGame(42, scenario)` twice produces deeply-equal states.
- **Round-trip:** serialize with `System.Text.Json`, deserialize, run 100 ticks on both original and clone, assert final states are deeply equal.

This round-trip test is the single most valuable test in Phase 1 — it catches non-determinism, unserializable fields, and hidden state in one assertion.

**Pitfall:** `HashSet`/`Dictionary` iteration order is not guaranteed stable. Anywhere iteration order affects simulation outcome, **sort by id explicitly first**.

**Commit:** `feat(core): add GameState aggregate with save/load round-trip`

# Block C — Simulation

### Task 14: `ISimulationSystem` and `SimulationEventBus`

**Objective:** The extension seam every later phase plugs into.

**Design:**
```csharp
public interface ISimulationSystem {
    string Name { get; }
    void Tick(SimulationContext ctx);
}

public sealed class SimulationContext {
    GameState State { get; }
    GameDate Date => State.Date;
    DeterministicRandom Rng => State.Random;
    void Emit(ISimulationEvent e);
    void Post(LedgerCategory cat, Money amount, string desc, string? nodeId = null);
}
```

Events are `record` types implementing marker `ISimulationEvent`: `BatchBrewed`, `BatchReady`, `BatchInfected`, `BatchSpoiled`, `ShipmentDeparted`, `ShipmentArrived`, `ShipmentBlockedNoContainers`, `ContainersReturned`, `SaleCompleted`, `WagesPaid`, `LoanPaymentDue`, `BankruptcyWarning`, `StaffResigned`, `TickCompleted`.

**Tests:** events emitted during a tick are readable only after tick completion; the bus preserves emission order; the bus clears between ticks.

**Commit:** `feat(core): add simulation system interface and event bus`

---

### Task 15: `SimulationEngine` tick runner

**Objective:** Deterministic, ordered advancement of state.

**Design:**
```csharp
public sealed class SimulationEngine {
    SimulationEngine(GameState state, IReadOnlyList<ISimulationSystem> systems);
    IReadOnlyList<ISimulationEvent> Tick();        // exactly 1 day
    IReadOnlyList<ISimulationEvent> Advance(int days);
    event Action<TickReport>? TickCompleted;       // Phase 4 UI hook
}
```

**System execution order (pinned by a test — order is data, not accident):**
1. `ProductionSystem` — start brews, consume ingredients
2. `FermentationSystem` — advance vessels, finish batches
3. `LogisticsSystem` — dispatch, advance, deliver shipments, return empties
4. `SpoilageSystem` — infection rolls and progression
5. `SalesSystem` — sell at markets, realize revenue
6. `EconomySystem` — wages, upkeep, duty, loan interest, bankruptcy check

Economy runs **last** so it sees the full day's activity before settling accounts.

`Tick()`: run each system in order → increment `TickCount` → advance `Date` → flush bus → raise `TickCompleted`.

**Tests:**
- One tick advances date by exactly one day and `TickCount` by 1.
- Systems execute in registration order (recording stub systems).
- A throwing system does not corrupt state — the exception propagates and `Date`/`TickCount` are unchanged (commit the date advance only after all systems succeed).
- **Determinism:** two engines, same seed and scenario, 1000 ticks → identical `GameState` JSON.
- No system holds a reference back to the engine (no re-entrancy).

**Commit:** `feat(core): add deterministic SimulationEngine tick runner`

---

### Task 16: `ProductionSystem` — the brew day

**Objective:** Turn ingredients and decisions into wort, then into a fermenting batch.

**Behaviour** (breweries iterated in sorted-id order):
1. Check a free vessel exists — if not, skip (no silent overflow).
2. Check ingredient lots suffice; check `HasSufficientDiastaticPower`.
3. **Debit the ingredient cost** via `TryDebit`; abort cleanly if broke.
4. Consume lots FIFO, recording COGS onto the batch.
5. Compute **mash efficiency**: equipment tier × vessel condition × diastatic sufficiency × water fit × maltster bonus. Yields actual OG vs. target — falling short is the classic beginner failure and a one-tooltip teaching moment.
6. Compute hop utilization from the addition schedule and boil time → `IbuTenths`.
7. Roll **quality** (Task 8 formula) and derive `FlavorProfile` from mash temp, grist, hops, and water.
8. Roll rare **stuck mash** (low probability, worse with high wheat/fine crush) → lost brew day, ingredients consumed, emit failure event. Keep the rate low enough to be texture rather than frustration.
9. Occupy the vessel with `DaysRemaining = FermentationDays + ConditioningDays`.
10. Emit `BatchBrewed`.
11. Sell **spent grain** to farmers — small `ByproductSales` credit, historically accurate and characterful.

**Tests:** insufficient ingredients → no batch, no exception, no ledger movement; no free vessel → no batch; insufficient funds → no batch and no ingredient consumption; low mash temp yields higher ABV and lower body than high mash temp on the same grist; a 60-minute hop addition yields more IBU than the same hops at 5 minutes; spent grain credits the ledger.

**Commit:** `feat(core): add production system with mash and brew day`

---

### Task 17: `FermentationSystem`

**Objective:** Advance occupied vessels; complete or ruin batches.

**Behaviour:** Decrement `DaysRemaining` on occupied vessels. Apply **ambient temperature** from `RegionClimate`: fermenting outside the yeast strain's tolerance band degrades quality and can add off-flavors (`Diacetyl`, `Phenolic`). Refrigeration tech or an ice-stocked warehouse negates this — **this is the mechanical payoff for the Ice House and Linde tech nodes**. Track `YeastGeneration`: repitched yeast drifts and degrades over generations, which is precisely the problem the Carlsberg pure-yeast node solves. At zero days remaining, set `Stage = Ready` and free the vessel.

**Tests:** a summer ale in a hot region without ice suffers a quality penalty; the same batch with refrigeration does not; yeast generation 10 underperforms generation 1; the vessel frees on exactly the expected tick; underpitching produces a stuck fermentation with high final gravity and residual sweetness.

**Commit:** `feat(core): add fermentation system with temperature and yeast generation`

---

### Task 18: `LogisticsSystem`

**Objective:** Move beer forward, move empties back.

**Behaviour:** Dispatch ready batches along the cheapest viable path; check container availability and **debit transport cost**; create `Shipment { BatchId, Path, HopIndex, DaysRemaining, PackagingType }`. Apply drayman transit-speed bonus. Decrement daily; on arrival at a hop, apply **ullage loss**, advance to the next hop or deliver. On final delivery, deposit into the destination and emit `ShipmentArrived`. **Returnable containers generate a reverse shipment** back toward the brewery, with a seeded loss rate reduced by the cooper bonus.

**Tests:** a 3-day route arrives on tick 3, not 2 or 4; multi-hop paths traverse in order; arriving at a full warehouse emits rejection rather than vanishing inventory; no containers → shipment blocked, beer stays put; empties return and restore pool availability; transport cost hits the ledger on dispatch.

**Commit:** `feat(core): add logistics with shipments and cask returns`

---

### Task 19: `SpoilageSystem` — infection as an event, not a gradient

**Objective:** Diagnosable spoilage with real, teachable defenses.

**Design:**
```csharp
public enum SpoilageOrganism { Lactobacillus, Pediococcus, Brettanomyces, Acetobacter, WildYeast }

public sealed class Infection {
    SpoilageOrganism Organism;
    GameDate ContractedOn;
    int ProgressionBasisPoints;    // 10000 = batch ruined
    string SourceDescription;      // "open fermentation", "dirty cask", "warm transit"
}
```

**Two-stage model.** First an **infection roll** at vulnerable moments (open fermentation, cask filling, warm transit, high-oxygen packaging), with probability driven by vessel hygiene, equipment tier, brewmaster infection-resistance, and pasteurization. Then **progression** each tick at a rate driven by temperature, ABV, and IBU. This replaces the mystery decay counter with a cause the player can actually diagnose and fix.

**Defenses (all historically real, all already modelled):**
- **Hop bitterness** — iso-alpha-acids inhibit Gram-positive bacteria. This is *why* heavily-hopped export beers survived long voyages, and it's the best emergent hook in the design: **you can only ship this far if you brew it bitter enough.** Recipe design and the logistics graph now talk to each other.
- **ABV** — higher alcohol resists spoilage; the historical reason for strong export stouts.
- **Pasteurization** — near-immunity, at a flavor-quality cost.
- **Cold chain** — temperature slows progression.
- **Packaging oxygen ingress** — drives Acetobacter specifically.

**The twist:** in some markets and styles a *Lactobacillus* infection is **desirable** — Berliner Weisse, lambic, gueuze. Same mechanic, opposite valence depending on player intent. Model it as: infection is only a defect if it's off-style.

**Consequences:** spoiled beer is written off (`SpoilageWriteOff` on the ledger, at COGS) and, if it reached a market, **damages that market's reputation**, depressing future prices there.

**Tests:** unpasteurized cart-shipped beer spoils faster than pasteurized rail-shipped beer over the same span; a high-IBU batch resists Lactobacillus significantly better than a low-IBU one; refrigerated storage slows progression; spoiled batches are removed exactly once (no double-emit); a soured Berliner Weisse is *not* flagged as defective; spoilage posts a write-off and drops market reputation.

**Commit:** `feat(core): add typed infection and spoilage system`

---

### Task 20: `SalesSystem` and `EconomySystem`

**Objective:** Close the economic loop — revenue in, costs out, bankruptcy possible.

**`SalesSystem`:** At each market, match available inventory against `DemandLitresPerTick` per style. Realized price = base price × quality factor × profile-fit factor × reputation × salesman bonus. Sell, credit `BeerSales`, emit `SaleCompleted`. Unsold inventory stays and keeps ageing — a real carrying cost.

**`EconomySystem`** (runs last each tick):
1. **Daily upkeep** for every node and vessel.
2. **Monthly wages** on the first of the month; unpaid wages drop staff loyalty.
3. **Excise duty** per litre sold — era-dependent, and a natural Phase 3 event lever (wartime duty spikes).
4. **Loan interest** accrual and scheduled repayments.
5. **Bankruptcy check:** negative balance emits `BankruptcyWarning`; sustained beyond a grace period ends the run.

```csharp
public sealed class Loan {
    LoanId Id; Money Principal; Money Outstanding;
    int AnnualInterestBasisPoints;
    GameDate TakenOn; int TermDays;
    Money DailyInterestAccrual();
}
```

**Why this is the spine:** every earlier system now has a price. Better vessels cost capital. Better staff cost wages. Faster transport costs more per litre. Spoilage is a write-off. Lagers tie up tanks *and* capital. The player's real question stops being "what can I build?" and becomes **"what can I afford, and what will it return?"** — which is the actual tycoon loop.

**Tests:** a sale credits the ledger and decrements inventory; high quality realizes a higher price than low quality at identical demand; wages debit on the first of the month only; unpaid wages drop loyalty; loan interest accrues daily and compounds correctly over a year; excise duty scales with volume sold; sustained negative balance triggers bankruptcy; **every mutation to balance has a corresponding ledger entry** (assert `Balance == sum(entries)` after 1000 ticks — this is the invariant that keeps the economy debuggable).

**Commit:** `feat(economy): add sales and economy systems with loans and bankruptcy`

# Block D — Proof

### Task 21: Integration scenario + headless runner

**Objective:** Prove the whole loop works end to end without Unity.

**Files:** `tests/Integration/StarterScenarioTests.cs`, `src/BreweryEmpire.Headless/` (small `net8.0` console app running N ticks and printing a financial summary).

**Scenario "Burton 1750":** one small brewery on Burton water with 2 wooden open fermenters, one warehouse, two markets, cart and canal routes, one pale ale recipe, one journeyman brewmaster, a starting loan, seed 1750.

**Tests:**
- Run 365 ticks: at least one batch brewed, one shipment arrived, one batch spoiled, casks returned, wages paid 12 times, no exceptions.
- **Ledger invariant:** `Balance == sum(all entries)` — the economy never leaks money.
- **Golden master:** serialized end state matches `tests/Fixtures/burton_365ticks.json`. Regenerate deliberately when balance changes; a diff here is your early warning that a "harmless" refactor altered simulation behaviour. Document the regeneration command in the test file header or it becomes noise someone deletes.
- **Economic sanity:** a player who does nothing goes bankrupt (upkeep and wages bite); a player running the default loop stays solvent. If either fails, the economy is mistuned.
- **Performance:** 10,000 ticks in under 2 seconds (`Stopwatch`). Establishes the headroom Unity needs.

**Commit:** `test(core): add Burton 1750 end-to-end scenario and golden master`

---

### Task 22: CI and Unity-independence guard

**Objective:** Keep the foundation honest.

**Steps:**
1. `.github/workflows/ci.yml` — on push/PR: `dotnet restore`, `dotnet build -c Release`, `dotnet test --logger trx`.
2. **A test that fails if `UnityEngine` is ever referenced** from `BreweryEmpire.Core` (scan assembly references, or grep `src/`). This guard is what keeps Phase 1's core promise true once Phase 4 tempts you.
3. A test asserting **no `System.Random`, no `DateTime.Now`, no `Guid.NewGuid()`** anywhere in `src/BreweryEmpire.Core` — the three classic determinism leaks.
4. `docs/architecture.md`: tick contract, system execution order, determinism rules, the ledger invariant, and how Unity will consume the DLL.
5. Commit: `ci: add build/test workflow and determinism guards`

---

## Validation Summary

| Check | Command | Expected |
|---|---|---|
| Build | `dotnet build -c Release` | 0 warnings, 0 errors |
| Unit tests | `dotnet test` | all pass (~140–170 tests) |
| Determinism | in test run | identical JSON over 1000 ticks |
| Ledger invariant | in test run | balance == sum(entries) |
| Golden master | in test run | matches fixture |
| Perf | in test run | 10k ticks < 2s |
| Unity independence | in test run | no `UnityEngine` reference |
| Determinism leaks | in test run | no `System.Random`/`DateTime.Now`/`Guid.NewGuid` |

## Risks & Tradeoffs

- **Scope grew substantially** (15 → 22 tasks). Justified: every addition is a *structure* Phase 2's math will need, and each was picked because retrofitting it later means rewriting a system. Vessels and the ledger are the two that would have been genuinely painful to add after the fact.
- **Economy tuning is not Phase 1's job.** The numbers in `PriceBook` will be wrong. That's fine — the goal is that the *plumbing* is right and every cost has somewhere to land. Balance passes come after Phase 2's market system exists.
- **Quality/FlavorProfile split adds surface area.** Two axes are more to reason about than one, but collapsing them would make either brewmaster skill or recipe design meaningless. Keep both; they answer different questions.
- **Golden-master brittleness** — fails on every intentional balance change. That's the point, but it needs a documented regeneration path.
- **Staff wages + upkeep could make the early game brutally punishing.** Watch the "do nothing → bankrupt" test: it should take most of a year, not two months.
- **Integer-only state** is more upfront friction than floats but eliminates cross-platform determinism bugs and makes save round-trips exact. Right call for a tycoon sim with 100-year campaigns.
- **365-day years** will eventually look wrong to a history-minded player. Cheap now, moderately annoying later; flagged in the architecture doc.
- **Cask return doubles logistics complexity.** If Task 18 balloons, the fallback is a flat percentage container-loss cost with no reverse shipments — but you lose a genuinely fun mechanic, so try the real version first.

## Open Questions

1. **Rival brewers.** The historical-figure exclusivity mechanic implies AI competitors bidding against you. Not in Phase 1, but if rivals are coming, `GameState` should probably hold a `List<RivalBrewer>` stub now rather than a schema migration later.
2. **Unity consumption** — compiled DLL in `Assets/Plugins/`, or `src/` symlinked into `Assets/`? DLL is cleaner; symlink debugs better in-editor. Decide before Task 22.
3. **Save versioning** — add a `SaveVersion` int + migration hook in Task 13? Cheap now, painful later. Recommend yes.
4. **Staff progression** — do employees gain skill with experience, or is skill fixed at hire? Experience growth makes long-term staff investment meaningful and is ~one field plus a tick hook.
5. **Tied houses.** Historically the dominant distribution model — brewers owned their pubs. Currently markets are neutral. Owning market nodes outright (guaranteed demand, capital cost, exclusivity) is a strong Phase 2 candidate and worth confirming as a direction now, since it shapes how `Market` evolves.




