# Phase 2: Markets, Logistics & Brewing Science — Implementation Plan (Rev 1)

> **For Hermes:** Implement task-by-task, TDD. Each task = failing test → see it fail →
> minimal implementation → see it pass → commit. Keep `dotnet build -c Release` warning-free
> (TreatWarningsAsErrors) and `dotnet test` green at every step.

**Goal:** Turn Phase 1's *functional-but-flat* systems into the real game by adding the
brewing chemistry, market economy, and logistics the design doc actually calls for. Phase 1
proved the engine runs headless and deterministically; Phase 2 makes it a *game* worth playing.

**Phase 1 left these as structures-with-no-math (verbatim from the Phase 1 plan's "Deferred to
Phase 2" list):** mash efficiency formula, IBU/SRM/ABV calculation, attenuation curves,
infection probability tuning, flavor→market-fit scoring, dynamic demand. Phase 2 lands all six,
plus the systems the 22-task plan put in Phase 1 but were intentionally leaned-out during the
build: `Market`/`Warehouse` nodes, the full `LogisticsSystem` (shipments + cask returns), the
two-stage `SpoilageSystem`, and a `FermentationSystem` split out of brewing. It also addresses
Open Questions #1 (rivals), #4 (staff progression) and #5 (tied houses).

---

## Post-Phase-1 state (what exists, what must change)

| Phase 1 built | Phase 2 must |
|---|---|
| `BrewingSystem.TryStartBrew` (brew + occupy vessel, one pass) | Split into `ProductionSystem` (brew day + chemistry) and `FermentationSystem` (vessel advance, temp, yeast) |
| `Batch` has `QualityBasisPoints` + rough `FlavorProfile`, no OG/FG/ABV/IBU/SRM | Compute `OriginalGravityPoints`, `FinalGravityPoints`, `AbvBasisPoints`, `IbuTenths`, `SrmLovibond` from recipe + efficiency + yeast |
| `SpoilageSystem` = single infection roll → `OffFlavor` + severity | Two-stage: infection roll **then** progression driven by temp/ABV/IBU/cold-chain; `SpoilageOrganism`; desirable-sour twist |
| `SalesSystem.ProcessNode` sells at the *brewery* with a flat `BaseDailyDemandLitres` and a flat price | `MarketSystem` sells across `Market` nodes using dynamic demand, reputation, and flavor→fit pricing |
| `WorldMap` = symmetric routes by distance only, no transport, no shipments | `Route` with `TransportMode`, Dijkstra `FindCheapestPath`/`FindFastestPath` (deterministic tie-break), `Shipment` forward + cask-return reverse |
| `NodeType.Warehouse`, `Pub`, `Farm` exist but are unused | `Warehouse` node with refrigeration / ice-house / ice stock — the cold-chain payoff |
| `GameState.Rivals` = empty `List<RivalBrewer>` stub | Light rival AI: brew + sell, compete for demand and historical figures |
| `StaffMember.SkillBasisPoints` fixed at hire | Skill grows with experience (Open Q#4) |
| No market ownership | Tied houses: own `Market` nodes for guaranteed demand + exclusivity (Open Q#5, optional) |

**Explicitly deferred (NOT Phase 2):** the tech research DAG and its nodes (Ice House, Linde,
Carlsberg, Pasteurization, saccharometer reveal) — those are Phase 3. **However**, Phase 2
implements the *mechanics* those nodes flip on (cold-chain negates temp penalty; pasteurization
near-immunity; yeast purity removes a spoilage class) so Phase 3 is a toggle, not a rewrite. The
Unity front end (Phase 4) is untouched; Phase 2 keeps the core Unity-free and extends the
existing `BreweryEmpire.Core` assembly (no new project needed).

**Cross-cutting, non-negotiable (inherited from Phase 1):**
- One tick = one day. `TickSystem.AdvanceDay` advances exactly one day.
- Integer-only state: litres, grams, cents (`Money`), basis points. No floats in saved state.
- Single PRNG (`GameState.Random`); no `System.Random`, `Guid.NewGuid`, `DateTime.Now`. The
  determinism guards in `tests/.../Guards/` must stay green.
- Every balance change via `Ledger.Post`. Invariant `Balance == sum(entries)` holds over 1000+
  ticks (asserted by integration tests).
- All collection iteration that affects outcomes is sorted by id (no `Dictionary`/`HashSet`
  order leaks). Pathfinding tie-breaks by node-id ordinal.
- `SaveVersion` is 1; a `from v2` migration shim is added when Phase 2 changes the save shape.

---

## Task Blocks

| Block | Tasks | Theme |
|---|---|---|
| A — Brewing science | A1–A3 | OG/FG/ABV/IBU/SRM, mash efficiency, fermentation split |
| B — Infection model | B1 | Two-stage spoilage with historical defenses |
| C — Market system | C1–C4 | Market nodes, dynamic demand, flavor→fit pricing, refactor sales |
| D — Logistics | D1–D3 | Transport modes, Dijkstra, shipments, cask returns |
| E — Warehouses & cold chain | E1 | Refrigeration / ice-house payoff |
| F — Rivals & progression | F1–F3 | Staff growth, rival AI, tied houses |
| G — Proof | G1–G2 | Burton v2 integration, golden master, guards |

---

# Block A — Brewing science (the deferred math)

### Task A1: Wort gravity, mash efficiency, ABV

**Objective:** A brew yields a *measured* OG that may fall short of the recipe target, and an
ABV derived from real attenuation — not a flat number.

**Files:** `Model/Brewing/Batch.cs` (add `OriginalGravityPoints`, `FinalGravityPoints`,
`AbvBasisPoints`, `AttenuationBasisPoints`), `Simulation/ProductionSystem.cs`
(rename of `BrewingSystem`, compute chemistry), tests.

**Mash efficiency (integer, basis points 0–10000):**
```
efficiency = EquipmentTierBase            // Wooden 7000, Copper 8000, Iron 8500, Stainless 9200
           × ConditionBasisPoints/10000
           × DiastaticSufficiency          // clamp(min(actualDP / requiredDP, 1))
           × WaterFitBasisPoints/10000     // from WaterProfile.FitScoreBasisPoints
           × (10000 + MaltsterMashEfficiencyBonus)/10000
```
`OriginalGravityPoints` = `Recipe.TargetOriginalGravityPoints × efficiency / 10000`, clamped to a
sane ceiling. `FinalGravityPoints` = `OG × (10000 − AttenuationBasisPoints) / 10000` where
attenuation comes from the yeast strain × temperature factor (see A3). `AbvBasisPoints =
(OG − FG) × 131` (the standard integer approximation, ×100 for basis points).

**Tests:**
- A stainless vessel at full condition yields higher OG than a wooden one on the same grist.
- Insufficient diastatic power (all-roasted grist) clamps efficiency to its floor and lowers OG.
- Lower mash temperature on the same grist → higher apparent attenuation → higher `AbvBasisPoints`
  and lower body (assert `FlavorProfile.BodyBasisPoints` drops vs. a high-temp mash).
- `AbvBasisPoints` is non-negative and monotonic in `(OG − FG)`.

**Commit:** `feat(brewing): compute OG, FG, ABV from mash efficiency and attenuation`

---

### Task A2: IBU and SRM

**Objective:** Hop bitterness and colour become real, computed values that the spoilage and
market systems can use (IBU is a spoilage defense; SRM is a flavor/style attribute).

**Files:** `Model/Recipes/HopAddition.cs` (utilization helper), `Model/Brewing/Batch.cs`
(`IbuTenths`, `SrmLovibond`), `Simulation/ProductionSystem.cs`, tests.

**IBU (integer tenths, e.g. 350 = 35.0 IBU):** per addition, `mg alpha-acid = Grams ×
AlphaAcidBasisPoints/10000`. Utilization from `MinutesFromEnd` via a clamped curve
(~60 min ≈ 0.24, ~5 min ≈ 0.05, 0 ≈ 0.02), scaled by `OG` (high gravity suppresses utilization).
`IbuTenths = Σ mg × utilization × 10 / volumeL`. **Deterministic, no float** — use integer
percentages of a reference table.
**SRM (Lovibond):** `Σ (MaltLovibond × MaltWeight) / TotalWeight`, simplified Morey-ish integer
mapping; crystal/roasted dominate.

**Tests:**
- A 60-minute addition of the same hops yields strictly more `IbuTenths` than a 5-minute addition.
- More crystal malt → darker `SrmLovibond`.
- `IbuTenths` is monotonic in hop grams and in boil time, and never negative.

**Commit:** `feat(brewing): compute IBU and SRM from recipe`

---

### Task A3: `FermentationSystem` (split from brewing)

**Objective:** Vessel advance, temperature-band effects, yeast generation drift, stuck
fermentation — as its own ordered system (plan order: Production → Fermentation → Logistics →
Spoilage → Market → Economy).

**Files:** `Simulation/FermentationSystem.cs`, `Model/Brewing/Batch.cs` (yeast generation,
`YeastStrainId`, `YeastGeneration`), `Simulation/TickSystem.cs` (new order), tests.

**Behaviour:** for each occupied vessel at each brewery (sorted id):
1. Decrement `DaysRemaining`; on zero, `MarkReady()` and free the vessel (unchanged contract).
2. **Temperature band:** if `Climate.AmbientTempOn(date)` is outside the yeast strain's tolerance
   band, apply a quality penalty and a seeded chance of `OffFlavor` (`Diacetyl` cold-stuck,
   `Phenolic`/`Sulfur` hot). A refrigerated/ice-stocked warehouse (Block E) or Linde tech (Phase 3)
   negates this — the mechanic lands now, the toggle later.
3. **Yeast generation:** repitched yeast (`YeastGeneration` increments each repitch) drifts —
   efficiency and attenuation fall with generation; generation 1 > generation 10 (the Carlsberg
   pure-culture fix, Phase 3).
4. **Stuck fermentation:** underpitch (low yeast health) → high `FinalGravityPoints`, residual
   sweetness (`FlavorProfile.SweetnessBasisPoints` rises), possible `Acetaldehyde`.

**Tests:**
- A summer ale in a hot region without ice suffers a quality penalty; the identical batch stored
  cold does not.
- `YeastGeneration` 10 underperforms generation 1 on attenuation.
- Vessel frees on exactly the expected tick (regression vs. Phase 1 vessel contract).
- Underpitching produces high FG + residual sweetness without throwing.

**Commit:** `feat(sim): split FermentationSystem from production with temperature and yeast`

---

# Block B — Infection model (two-stage)

### Task B1: `SpoilageSystem` upgrade

**Objective:** Replace the single-roll `OffFlavor` with a diagnosable two-stage model where the
*defenses* are the historical lessons (hops, strength, cold, pasteurization).

**Files:** `Model/Brewing/Infection.cs` (`SpoilageOrganism` enum + `ProgressionBasisPoints`),
`Simulation/SpoilageSystem.cs` (rewrite), tests. Keep `Batch.AddInfection(OffFlavor, severity)`
working for backward-compat and the simple test path; add `Batch.AddInfection(SpoilageOrganism, …)`.

**Two stages:**
1. **Infection roll** at vulnerable moments (open fermentation, cask fill, warm transit, high-O₂
   packaging). Probability from `vessel HygieneBasisPoints`, `EquipmentTier`, brewmaster
   `InfectionResistance`, and `IsPasteurized`. Roll via `DeterministicRandom.Chance`.
2. **Progression** each tick at `rate = f(temperature, ABV, IBU, coldChain)`:
   `ProgressionBasisPoints += rate`. At 10000 the batch spoils. **Defenses** (all already
   modelled, now wired in):
   - **IBU** — higher `IbuTenths` suppresses Gram-positive bacteria (Lacto/Pediococcus).
   - **ABV** — higher `AbvBasisPoints` resists spoilage.
   - **Pasteurization** — rate ≈ 0 (near-immunity) at a `FlavorProfile` quality cost.
   - **Cold chain** — warehouse refrigeration / ice slows progression.
3. **Desirable sour:** if the batch's *style* calls for it (Berliner Weisse, lambic), a
   `Lactobacillus`/`Brettanomyces` infection is **not** a defect — flag `IsIntentionalSour`.

**Consequences:** spoiled beer → `SpoilageWriteOff` at COGS; if it reached a market, **damages
that market's reputation** (Block C), depressing future prices there.

**Tests:**
- High-IBU batch resists `Lactobacillus` significantly better than low-IBU over the same span.
- Refrigerated storage slows progression vs. ambient.
- Unpasteurized cart-shipped beer spoils faster than pasteurized rail-shipped (ties to Block D).
- A soured Berliner Weisse is **not** flagged defective.
- Spoiled batches write off exactly once (no double-emit); market reputation drops on spoiled
  delivery.

**Commit:** `feat(sim): two-stage infection model with historical defenses`

---

# Block C — Market system

### Task C1: `Market` node

**Objective:** A place beer is *sold*, distinct from where it is *made*.

**Files:** `Model/Sites/MarketNode.cs` (new; `NodeType.Market` already exists),
`Model/Markets/DemandCurve.cs`, extend `GameState`/`WorldMap` registration, tests.

```
MarketNode : Node
    int Population
    Dictionary<BeerStyle,int> BaseDemandLitresPerTick   // per style
    int ReputationBasisPoints                          // clamped [0,10000]
    Money BasePricePerLitre
    bool IsTiedHouse                                   // Block F3
```

**Tests:** market reputation clamps [0,10000]; unknown style has zero base demand; node equality
by id; a tied house reports `IsTiedHouse == true` and zero competing supply.

**Commit:** `feat(market): add Market node with demand and reputation`

---

### Task C2: Dynamic demand

**Objective:** Demand that moves — season, market size, and your reputation — so the world feels
alive and brewing-to-calendar matters.

**Files:** `Model/Markets/DemandCurve.cs`, `Simulation/MarketSystem.cs` (partial), tests.

`EffectiveDemand(style, date) = BaseDemand × PopulationFactor × SeasonalFactor(climate, date) ×
ReputationFactor(reputation)`. Seasonal factor from the market's `RegionClimate` (summer ales up,
winter warm-beer up, etc.); deterministic and continuous across the year boundary. Reputation
factor in `[5000, 12000]bp` mapped from reputation.

**Tests:** summer raises demand for a refreshing style vs. winter; larger population → more
demand; higher reputation → more demand; factor is continuous across 31 Dec → 1 Jan.

**Commit:** `feat(market): dynamic seasonal and reputation-scaled demand`

---

### Task C3: Flavor→market-fit scoring & realized price

**Objective:** Land the deferred `FlavorProfile.DistanceTo` → market fit, and price = base ×
quality × fit × reputation × salesman.

**Files:** `Model/Brewing/FlavorProfile.cs` (add `MarketFitBasisPoints(target)`),
`Simulation/MarketSystem.cs`, tests.

Each market has a `PreferredProfile` per style (or a default). `fit = Profile.MarketFit(target)`.
`RealizedPrice = BasePrice × (5000 + Quality) / 10000 × fit / 10000 ×
ReputationFactor × (10000 + SalesmanPriceRealization) / 10000`. A well-brewed beer nobody wants
(real fit low) sells badly — this is the whole point of keeping Quality and Flavor separate.

**Tests:** identical quality, higher style-fit → higher realized price; a stout pushed to a
pale-ale market realizes less than the same stout in its own market; salesman bonus raises price;
`MarketFitBasisPoints` is symmetric and 10000 against an exact match.

**Commit:** `feat(market): flavor-fit pricing and reputation feedback`

---

### Task C4: `MarketSystem` refactor + reputation feedback

**Objective:** Replace `SalesSystem.ProcessNode` (which sold at the brewery) with `MarketSystem`
that moves beer to markets via the logistics graph and applies C1–C3. Breweries become producers
only.

**Files:** `Simulation/MarketSystem.cs` (replaces `SalesSystem`), `Simulation/TickSystem.cs`
(order: … → MarketSystem → EconomySystem), tests.

Behaviour: for each market (sorted id), compute effective demand, draw from locally-available
batches (those delivered via Block D shipments, or brewery-adjacent if distance 0), sell oldest
ready batch first, credit `BeerSales`, post `ExciseDuty`, emit `SaleCompleted`. On spoiled/low-quality
delivery, apply reputation penalty. Remove fully-sold batches.

**Tests:** a sale credits ledger + reduces batch volume; high quality realizes higher price at
equal demand (regression); excise duty scales with volume; spoiled delivery drops market
reputation; doing nothing still trends to ruin (Phase 1 canary still holds with markets).

**Commit:** `refactor(sim): replace flat sales with MarketSystem across markets`

---

# Block D — Logistics

### Task D1: Transport modes, routes, Dijkstra

**Objective:** Real transport choice with deterministic pathfinding.

**Files:** `Model/Logistics/TransportMode.cs`, `Model/Logistics/Route.cs`, extend `WorldMap`
with `FindCheapestPath`/`FindFastestPath` (Dijkstra), tests.

`TransportMode { HorseCart, Canal, SteamRail, CargoShip }` each with `DaysPerDistanceUnit`,
`CostCentsPerLitrePerUnit`, `SpoilageModifierBasisPoints`, `IsRefrigeratedCapable`. `Route`
carries `FromNodeId, ToNodeId, DistanceUnits, Mode` and computes `TransitDays`, `CostPerLitre`.
Dijkstra tie-break by node-id ordinal (no `Dictionary` order leaks).

**Tests:** cheapest path prefers a 2-hop canal chain over a 1-hop expensive cart; fastest prefers
rail even when pricier; disconnected nodes return empty (not throw); duplicate route throws;
deterministic tie-break verified by 100 runs.

**Commit:** `feat(logistics): transport modes and deterministic Dijkstra routing`

---

### Task D2: Shipments forward + cask returns

**Objective:** Beer moves forward; empties flow back. This is the logistics graph's reason to exist.

**Files:** `Model/Logistics/Shipment.cs`, `Simulation/LogisticsSystem.cs`, `State/GameState`
(`ShipmentsInTransit`), tests.

Behaviour: dispatch ready batches along cheapest viable path; check container availability and
debit transport cost; `Shipment { BatchId, Path, HopIndex, DaysRemaining, PackagingType }`.
Drayman transit-speed bonus. Decrement daily; on arrival apply **ullage loss** (per `PackagingSpec`),
advance hop or deliver to market. **Returnable containers generate a reverse shipment** back to
brewery with seeded loss rate reduced by cooper bonus. No containers → block shipment, emit
`ShipmentBlockedNoContainers`, beer stays put.

**Tests:** a 3-day route arrives on tick 3; multi-hop traverses in order; full warehouse rejects
(arrival emits `ShipmentRejected` rather than vanishing); no containers blocks shipment; empties
return and restore pool; transport cost hits ledger on dispatch; seeded cask loss is deterministic.

**Commit:** `feat(logistics): shipments forward with cask-return reverse flow`

---

### Task D3: Wire `LogisticsSystem` into the tick

**Objective:** Insert logistics between fermentation and spoilage (plan order), so beer is in
transit (and thus exposed to warm-transit infection) before the spoilage roll.

**Files:** `Simulation/TickSystem.cs`, integration test.

New order: Production → Fermentation → **Logistics** → Spoilage → Market → Economy. Assert order
with recording stub systems (Phase 1 pattern). Ensure a throwing system leaves date/tick
unchanged (commit date advance only after all systems succeed — already true).

**Tests:** order assertion; warm-transit shipment can contract infection (links D2 ↔ B1);
ledger invariant still holds with shipments + markets over 1000 ticks.

**Commit:** `feat(sim): insert LogisticsSystem into the tick order`

---

# Block E — Warehouses & cold chain

### Task E1: `Warehouse` node + ice/cold chain

**Objective:** The mechanical payoff for the Ice House / Linde tech nodes (Phase 3) — built now so
those nodes are toggles.

**Files:** `Model/Sites/WarehouseNode.cs` (`NodeType.Warehouse` exists),
`Simulation/FermentationSystem.cs` + `SpoilageSystem.cs` read `IsRefrigerated`/`HasIceHouse`/
`IceStockTonnes`, tests.

`WarehouseNode : Node { int CapacityLitres; bool IsRefrigerated; bool HasIceHouse; int IceStockTonnes; }`.
Batches conditioned/stored in a refrigerated or ice-stocked warehouse **negate** the fermentation
temperature penalty (A3) and **slow** spoilage progression (B1). `HasIceHouse` allows a winter
ice-harvest that fills `IceStockTonnes` (deterministic, once per cold season).

**Tests:** a batch conditioned in a refrigerated warehouse shows no temp penalty vs. the same
batch in an open brewery in summer; ice harvest fills stock in winter only; cold storage slows
spoilage progression.

**Commit:** `feat(sites): add Warehouse with refrigeration and ice-house cold chain`

---

# Block F — Rivals & progression

### Task F1: Staff skill progression

**Objective:** Long-tenured staff become better (Open Q#4) — makes retention strategic.

**Files:** `Model/Staff/StaffMember.cs` (`GainExperience(days)`), `Simulation` tick hook, tests.

On a successful brew the assigned brewmaster/maltster gains a small clamped skill bump
(deterministic, capped by a trait ceiling). Loyalty also drifts up while happily employed.

**Tests:** skill rises after N successful brews, clamps at 10000, and is deterministic for a seed;
unassigned staff do not gain brewing skill.

**Commit:** `feat(staff): gain skill with experience`

---

### Task F2: Rival brewers (light AI)

**Objective:** The world has competitors (Open Q#1). Rivals brew + sell at markets, competing for
demand and historical figures, so the `GameState.Rivals` stub becomes alive.

**Files:** `Model/Rivals/RivalBrewer.cs` (flesh out), `Simulation/RivalSystem.cs`, tests.

Each rival has a skill, a home region, a simple brew-and-sell loop at neutral markets, and can
pre-empt a historical-figure hire. Rivals consume a share of market demand, lowering the player's
realized volume/price — deterministic via the same PRNG. Keep it lightweight: no rival logistics,
no rival loans.

**Tests:** a market with an active rival yields the player less demand than the same market
alone; a rival can hire a historical figure the player hasn't yet; rival behaviour is seed-stable
over 1000 ticks.

**Commit:** `feat(sim): add lightweight rival brewers`

---

### Task F3: Tied houses (optional)

**Objective:** Own your pubs (Open Q#5) — guaranteed demand + exclusivity at capital cost.

**Files:** `Model/Sites/MarketNode.cs` (`IsTiedHouse`, `OwnerBreweryId`), `Economy` purchase hook,
tests. Owning a market sets `IsTiedHouse`, blocks rival supply there, and guarantees a demand
floor, paid for by a capital `Ledger` debit. **Optional** — include only if scope allows; the
market node already carries the fields.

**Tests:** a tied house blocks rival supply and guarantees a demand floor; purchase debits capital.

**Commit:** `feat(market): tied-house ownership (optional)`

---

# Block G — Proof

### Task G1: Burton 1750 v2 integration + golden master

**Objective:** Prove the full loop with markets, logistics, warehouses, rivals.

**Files:** `tests/Integration/Burton1750V2Tests.cs`, `tests/Fixtures/burton_v2_365ticks.json`,
`src/BreweryEmpire.Headless/` (extend console runner to print a market+P&L summary), tests.

Scenario: Burton brewery (2 wooden fermenters, 1 copper), 1 refrigerated-capable warehouse, 2
markets (Burton pale-ale market + a distant London porter market), cart + canal routes, pale ale
recipe, journeyman brewmaster, starting loan, seed 1750, one rival.

**Tests:**
- 365 ticks: ≥1 batch brewed, ≥1 shipment arrived, ≥1 batch spoiled, casks returned, wages paid
  12×, rival active, no exceptions.
- **Ledger invariant:** `Balance == sum(entries)` over 1000 ticks (regression).
- **Golden master:** serialized end state matches fixture; document regeneration command in the
  test header.
- **Economic sanity:** do-nothing → bankrupt within ~a year; default loop → solvent.
- **Performance:** 10,000 ticks (with shipments + markets + rival) in < 2s (`Stopwatch`).

**Commit:** `test(core): Burton 1750 v2 end-to-end with markets, logistics, rivals`

---

### Task G2: Guards & save migration

**Objective:** Keep Phase 1's promises and handle the save-shape change.

**Files:** `State/SaveSystem.cs` (add v1→v2 migration shim when schema changes; bump
`CurrentSaveVersion` to 2 only when a breaking field is added), `tests/Guards/` (extend
determinism guards: no `HashSet`/`Dictionary` order leak in pathfinding; graph iteration sorted),
tests.

**Tests:** a v1 save still loads via the migration shim; future `SaveVersion` refused; determinism
guards stay green; pathfinding tie-break deterministic (regression).

**Commit:** `ci: phase 2 guards, save migration shim, determinism extensions`

---

## Validation Summary

| Check | Command | Expected |
|---|---|---|
| Build | `dotnet build -c Release` | 0 warnings, 0 errors |
| Unit tests | `dotnet test` | all pass (target ~250–320 tests post-Phase-2) |
| Determinism | in test run | identical JSON over 1000 ticks with markets + logistics + rival |
| Ledger invariant | in test run | `Balance == sum(entries)` over 1000 ticks |
| Golden master | in test run | matches `burton_v2_365ticks.json` |
| Perf | in test run | 10k ticks < 2s |
| Unity independence | in test run | no `UnityEngine` reference (Phase 1 guard stays green) |
| Determinism leaks | in test run | no `System.Random`/`DateTime.Now`/`Guid.NewGuid` |

## Risks & Tradeoffs

- **Scope is large.** Phase 2 is the "real game" layer. Recommend implementing in block order
  A→G; Block F3 (tied houses) is the only optional task. If time-boxed, drop F3 first, then F2
  (rivals can be a Phase 3 stub still — but the `Rivals` field already exists, so a light F2 is
  cheap).
- **Brewing math tuning will be wrong.** Same as Phase 1: the *plumbing* (OG/FG/ABV/IBU/SRM exist,
  efficiency feeds them) matters more than the exact constants. Balance passes come after the
  market exists.
- **Two-stage spoilage adds state** (progression per organism). Keep it integer `ProgressionBasisPoints`
  so saves stay exact; the Phase 1 `OffFlavor` report stays as the player-facing summary.
- **Markets make the early game Economy-sensitive.** Watch the "do nothing → bankrupt" canary:
  with logistics + market costs it should still take most of a year, not two months. If it
  tightens, the issue is price/demand constants, not structure.
- **Save migration.** Phase 2 changes the save shape (new node types, shipments, organisms).
  Bump `SaveVersion` to 2 and ship a v1→v2 shim so existing Phase 1 saves still load.
- **Rivals touching demand is a balance lever.** Keep rival demand-share small and seed-stable so
  it reads as competition, not randomness.

## Open Questions (resolve before/while building)

1. **Rival depth** — light loop (F2) vs. full rival logistics/loans. Recommend light for Phase 2.
2. **Tied houses** (F3) — include now or defer to a distribution-focused Phase 3? Fields already
   exist; recommend include-if-time.
3. **Saccarometer reveal** — Phase 1 hid `OriginalGravityPoints`/`FinalGravityPoints` on `Batch`
   but Phase 2 computes them. Should they be *visible* to the player now (tech-gated later)? Keep
   computed-but-unrevealed until the Phase 3 tech node; the fields exist regardless.
4. **Pub vs. Market** — `NodeType.Pub` exists; is a pub just a small tied `Market`, or a distinct
   node? Recommend: pub = tied `Market` with a demand floor (folds into F3).
