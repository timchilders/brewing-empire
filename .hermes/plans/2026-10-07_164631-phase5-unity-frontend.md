# Phase 5: Unity Front End — First Playable

> **For Hermes / the implementer:** this phase changes the shape of the work. Phases
> 1–4 were a `dotnet test` TDD loop; Phase 5 is **Unity UI**, where the interesting,
> testable logic is extracted into a new netstandard2.1 library (`BreweryEmpire.App`)
> so the TDD discipline survives, and Unity itself becomes a thin rendering/input layer
> verified by EditMode/PlayMode tests and manual play, not `dotnet test`.

**Goal:** Turn the deterministic core into a playable brewing-empire game: a new-game
flow, a tick/pause loop, and the core screens (brewery, world/markets, tech tree, events,
finance, staff, save/load) — without ever letting the UI mutate sim state directly.

---

## Architecture (3 layers)

```
BreweryEmpire.Core     (netstandard2.1)  — unchanged. Rules only. Integer, deterministic.
        ▲ referenced by
BreweryEmpire.App      (netstandard2.1, NEW)  — GameSession + read-only view models + formatting.
        ▲ consumed by (compiled DLL, like Core)
Unity Assets/Scripts   (MonoBehaviours)  — render view models, forward commands to GameSession.
```

**The one rule that keeps the game honest:** Unity never calls `TickSystem.AdvanceDay`
or mutates `GameState` itself. All commands go through `GameSession`, which is the single
owner of `GameState`. That way the *exact same command sequence* can be replayed headlessly
(via `BreweryEmpire.Headless`) and must produce the *exact same* world — determinism is
re-asserted at the UI boundary, not just inside the core.

**Why a separate `App` assembly instead of putting it all in Unity:** formatting, view-model
projections, and "can the player afford/do this right now" logic are pure and belong under
`dotnet test`. Keeping them in a netstandard2.1 library means Phase 5 can make real, tested
progress *before Unity is installed* (Block A is doable today), and Unity stays thin.

**Tech stack:** C# 9 / netstandard2.1 (Core + App), net8.0 (tests + headless), Unity
2021.2+ (UI). **New projects:** `src/BreweryEmpire.App`, `tests/BreweryEmpire.App.Tests`.
`tools/publish-unity.sh` is extended to also publish `BreweryEmpire.App`.

---

## Current context / assumptions (verified)

- Core is feature-complete (Phases 1–4). Public API the UI will use:
  `ScenarioAssembler.Assemble(seed, ScenarioDefinition)`; `ScenarioCatalog.Burton1750` /
  `London1890`; `RecipeCatalog.PaleAle/Porter/Pilsner`; `TickSystem.AdvanceDay(s)` /
  `AdvanceDays(s, n)`; `BrewingSystem.TryStartBrew(s, nodeId, recipeId)` →
  `BrewResult{Success,Reason,Batch}`; `ResearchSystem.Start/Unlock/HasTech/CanStart/IsStyleUnlocked/IsPackagingUnlocked`; `EventSystem.Resolve(s, eventId, choiceIndex)`; `BuildSystem.TryPurchaseVessel`; `LogisticsSystem.DispatchShipment`; `SaveSystem.Save/Load`; `GameState` exposes `World`, `Markets`, `Ledger`, `Staff`, `Research`, `PendingEvents`, `Rivals`, `Date`, `IsBankrupt`, `PrestigeBasisPoints`, `ReputationBasisPoints`.
- **No event bus.** The core mutates `GameState` in place; the UI reads it between ticks.
  `GameSession` is the seam that makes this safe and replayable.
- `unity/Assets/Scripts/BreweryEmpireBootstrap.cs` is the Phase-4 spike (one-file
  immediate-mode dashboard). It is **superseded** by this phase; delete it once Block B lands.
- **Unity is not yet installed on this machine** (verified). Blocks A (App) and the
  headless proof are runnable now; Blocks B–C need Unity 2021.2+ installed.
- `SaveVersion` is 4. `SaveSystem.Save/Load` round-trips exactly (asserted by core tests).
- dotnet is not on PATH — every command needs the standard env prefix (see Task A0).

---

## Task blocks

| Block | Tasks | Theme | Runnable now? |
|---|---|---|---|
| A — App layer | A0–A4 | GameSession, formatting, view models (TDD, `dotnet test`) | **yes** |
| B — Unity UI | B1–B9 | Screens, scene, canvas, thin MonoBehaviours | needs Unity |
| C — Proof | C1–C4 | UI↔headless determinism, save/load, perf, player build | needs Unity |

---

# Block A — `BreweryEmpire.App` (do this first; it's testable today)

### Task A0: Scaffold the App project and re-verify baseline

**Files:** `src/BreweryEmpire.App/BreweryEmpire.App.csproj`,
`tests/BreweryEmpire.App.Tests/BreweryEmpire.App.Tests.csproj`.

```bash
export PATH="$HOME/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
unset DOTNET_ROOT

dotnet new classlib -o src/BreweryEmpire.App -f netstandard2.1
dotnet new xunit   -o tests/BreweryEmpire.App.Tests -f net8.0
dotnet sln add src/BreweryEmpire.App tests/BreweryEmpire.App.Tests
dotnet add src/BreweryEmpire.App reference src/BreweryEmpire.Core
dotnet add tests/BreweryEmpire.App.Tests reference src/BreweryEmpire.App src/BreweryEmpire.Core
dotnet add tests/BreweryEmpire.App.Tests package FluentAssertions
```

Edit `src/BreweryEmpire.App/BreweryEmpire.App.csproj` to match Core's discipline:
```xml
<PropertyGroup>
  <TargetFramework>netstandard2.1</TargetFramework>
  <LangVersion>9.0</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>disable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <RootNamespace>BreweryEmpire.App</RootNamespace>
</PropertyGroup>
```
Delete the template `Class1.cs`. **Verify:**
```bash
dotnet build -c Release && dotnet test
```
**Expected:** 0 warnings/errors; all existing 442 tests still green. Commit:
`chore(app): scaffold BreweryEmpire.App presentation layer`.

---

### Task A1: `GameSession` — the single owner of `GameState`

**Objective:** One object owns the live state and routes every command, so the same
command sequence replays identically headless or in-editor.

**Files:** `src/BreweryEmpire.App/GameSession.cs`; test
`tests/BreweryEmpire.App.Tests/GameSessionTests.cs`.

**Step 1 — failing test:**
```csharp
using BreweryEmpire.App;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.App.Tests
{
    public class GameSessionTests
    {
        [Fact]
        public void New_Game_And_Advance_Is_Deterministic()
        {
            string Run()
            {
                var s = GameSession.NewGame(ScenarioCatalog.Burton1750, seed: 42);
                s.Recipes.Register(RecipeCatalog.PaleAle());   // see A3 for Register
                for (int d = 0; d < 120; d++)
                {
                    if (d % 14 == 0) s.Commands.TryBrew("burton", "pale-ale");
                    s.AdvanceDay();
                }
                return s.Fingerprint;
            }
            Run().Should().Be(Run());
        }

        [Fact]
        public void Advance_Days_Moves_The_Clock_Exactly()
        {
            var s = GameSession.NewGame(ScenarioCatalog.Burton1750, seed: 1);
            var before = s.Date;
            s.AdvanceDays(30);
            s.Date.TotalDays.Should().Be(before.TotalDays + 30);
        }
    }
}
```

**Step 2 — run, expect FAIL** (compile error: `GameSession` missing).

**Step 3 — implement** `GameSession.cs` (skeleton — A3 fills `Recipes`/`Commands`):
```csharp
using System;
using BreweryEmpire.Core.Model.Scenario;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.App
{
    /// <summary>The single owner of GameState. Unity talks to this, never to the core.</summary>
    public sealed class GameSession
    {
        public GameState State { get; private set; }
        public RecipeRegistry Recipes { get; }
        public SessionCommands Commands { get; }

        private GameSession(GameState state)
        {
            State = state;
            Recipes = new RecipeRegistry(state);
            Commands = new SessionCommands(state);
        }

        public static GameSession NewGame(ScenarioDefinition scenario, int seed)
            => new GameSession(ScenarioAssembler.Assemble(seed, scenario));

        public static GameSession Load(string json)
            => new GameSession(SaveSystem.Load(json));

        public string Save() => SaveSystem.Save(State);

        public GameDate Date => State.Date;
        public bool IsBankrupt => State.IsBankrupt;

        /// <summary>Exactly one day. Call this (or AdvanceDays), never TickSystem directly.</summary>
        public void AdvanceDay() => TickSystem.AdvanceDay(State);

        public void AdvanceDays(int days)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
            for (int i = 0; i < days; i++) TickSystem.AdvanceDay(State);
        }

        /// <summary>Compact replay fingerprint, identical to the headless runner's.</summary>
        public string Fingerprint => TestScenario.Fingerprint(State);   // move helper into App (A3)
    }
}
```
(Note: `TestScenario.Fingerprint` is test-only today. In A3, move a copy of that fingerprint
logic into `BreweryEmpire.App` as `GameSession.Fingerprint` / `WorldSnapshot`, so the headless
runner and the App share one definition. Do not reference the test assembly from App.)

**Step 4 — run, expect PASS.** **Step 5 — commit:** `feat(app): GameSession owns state and tick`.

---

### Task A2: `Format` — pure display helpers

**Objective:** All number/date/percent formatting in one place, unit-tested, so the UI
never hand-rolls "$1234.56" or "65.5 IBU" strings.

**Files:** `src/BreweryEmpire.App/Format.cs`; test `tests/BreweryEmpire.App.Tests/FormatTests.cs`.

**Step 1 — failing test:**
```csharp
using BreweryEmpire.App;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.App.Tests
{
    public class FormatTests
    {
        [Fact]
        public void Money_Uses_Dollars_And_Cents()
        {
            Format.Money(Money.FromCents(123456)).Should().Be("$1,234.56");
            Format.Money(Money.FromCents(-25)).Should().Be("-$0.25");
        }

        [Fact]
        public void Date_Is_Iso()
        {
            Format.Date(GameDate.FromYearMonthDay(1750, 6, 1)).Should().Be("1750-06-01");
        }

        [Fact]
        public void Basis_Points_Show_As_Percent()
        {
            Format.Percent(6500).Should().Be("65.0%");
            Format.Percent(10000).Should().Be("100%");
        }
    }
}
```

**Step 2 — run, expect FAIL.** **Step 3 — implement** `Format.cs` with `Money`, `Date`,
`Percent(int basisPoints)`, and add `Ibu(int ibuTenths)` (e.g. `350` → `"35.0 IBU"`) as a
fourth test. Use `CultureInfo.InvariantCulture` for the grouping separator.

**Step 4 — run, expect PASS.** **Step 5 — commit:** `feat(app): pure display formatting helpers`.

---

### Task A3: Read-only view models + command registry

**Objective:** Immutable, UI-ready snapshots of `GameState` (sorted, grouped, formatted)
plus a `Commands` facade that returns a reason string instead of a bare bool. This is what
Unity binds to — it never reaches into live mutable state.

**Files:** `src/BreweryEmpire.App/Views/WorldView.cs`, `.../Views/BreweryView.cs`,
`.../Views/MarketView.cs`, `.../Views/TechView.cs`, `.../Views/EventView.cs`,
`.../Views/FinanceView.cs`, `.../Views/StaffView.cs`; `src/BreweryEmpire.App/RecipeRegistry.cs`,
`.../SessionCommands.cs`, `.../WorldSnapshot.cs` (fingerprint). Tests under
`tests/BreweryEmpire.App.Tests/Views/`.

**Design (representative — implement one view at a time, TDD):**

```csharp
// WorldView — top-level read-only snapshot, rebuilt after every tick.
public sealed class WorldView
{
    public GameDate Date { get; init; }
    public string BalanceText { get; init; }
    public bool IsBankrupt { get; init; }
    public IReadOnlyList<SiteRow> Sites { get; init; }     // sorted by id
    public IReadOnlyList<MarketRow> Markets { get; init; } // sorted by id
    public static WorldView From(GameState s) => /* project + sort + format */ ...;
}

public sealed record SiteRow(string Id, string Type, int VesselCount, int BatchCount, int CapacityLitres);
public sealed record MarketRow(string Id, int Population, int ReputationBasisPoints, int DemandLitres);
```

**`SessionCommands`** wraps core Try* with reasons (thin — no new rules, just ergonomics):
```csharp
public sealed class SessionCommands
{
    public Result TryBrew(string nodeId, string recipeId);       // BrewResult → "ok" | "no vessel" | ...
    public Result TryDispatch(string from, string to, string batchId, int litres, string packaging, string mode);
    public Result TryPurchaseVessel(string nodeId, string vesselType, string tier, int capacity);
    public Result TryStartResearch(string techId);
    public Result TryResolveEvent(string eventId, int choiceIndex);
}
public readonly record struct Result(bool Success, string? Reason);   // Reason is player-facing
```

**`RecipeRegistry`** holds `Dictionary<string,Recipe>` on the session and exposes
`BrewableRecipes(GameState)` (style unlocked + diastatic power OK) — the list the brew
screen shows.

**`WorldSnapshot.Fingerprint(GameState)`** — the one canonical fingerprint (date, balance,
entry count, per-node vessels/batches, rng state). Move the logic out of `TestScenario`
and make the **headless runner** print it too, so `headless == App == UI` all compare the
same string.

**Step 4 — run, expect PASS** per view. **Step 5 — commit** per view (7 small commits,
e.g. `feat(app): WorldView snapshot`, `feat(app): TechView researchable list`, …).

---

### Task A4: Update `tools/publish-unity.sh` to publish App too

**Files:** `tools/publish-unity.sh`.

Add a second `dotnet publish` of `BreweryEmpire.App` into
`unity/Assets/Plugins/BreweryEmpire.App/`, and add
`unity/Assets/Plugins/BreweryEmpire.App/` to `.gitignore`. **Verify:**
```bash
bash tools/publish-unity.sh
ls unity/Assets/Plugins/BreweryEmpire.App/*.dll
```
**Expected:** `BreweryEmpire.App.dll` present. Commit: `feat(app): publish App DLL for Unity`.

---

# Block B — Unity UI (needs Unity 2021.2+ installed)

> Each task ships one screen as a thin `MonoBehaviour` binding a `WorldView` (etc.) to
> `uGUI` widgets. Acceptance is manual play + EditMode tests where they add value. Delete
> `BreweryEmpireBootstrap.cs` after B2. No new rules logic here — if a task starts wanting
> a rule, push it down into `BreweryEmpire.App` and test it there.

### Task B1: App scaffold — `GameController` + screen manager + scene

**Files:** `unity/Assets/Scripts/GameController.cs`, `unity/Assets/Scripts/ScreenManager.cs`,
`unity/Assets/Scripts/Screens/Screen.cs` (base).

`GameController` is a singleton that owns a `GameSession`, exposes `Session` to screens,
and drives a "day boundary" event (rebuild views + refresh active screen after each
`AdvanceDay`). `ScreenManager` shows/hides screens. Create one empty scene
`unity/Assets/Scenes/Main.unity` with an `EventSystem`, a `Canvas`, and a `GameController`.

**Verify:** Play → no exceptions, empty screen visible. Commit: `feat(ui): GameController + screen manager`.

### Task B2: New-game screen

**Files:** `unity/Assets/Scripts/Screens/NewGameScreen.cs`.

Lists `ScenarioCatalog.All`, shows seed + name, and a "Start" button that calls
`GameSession.NewGame(scenario, seed)` then hands off to the main hub. (This replaces the
spike's `Start()`.) **Verify:** start both scenarios → hub opens with the right world.
Commit: `feat(ui): new game screen`.

### Task B3: Main hub (date / balance / time controls / nav)

**Files:** `unity/Assets/Scripts/Screens/HubScreen.cs`.

Shows date, balance, bankruptcy flag, research/prestige; buttons: `+1 day`, `+30 days`,
`Pause/Play (auto-advance every frame)`, and nav to all screens. Auto-advance uses a
frame-scaled timer (UI-side `Time.deltaTime` is fine — it never feeds the sim). **Verify:**
+1/+30 move the clock by exactly that; pause stops. Commit: `feat(ui): hub with time controls`.

### Task B4: Brewery screen

**Files:** `unity/Assets/Scripts/Screens/BreweryScreen.cs`.

Left: list of sites → vessels (free/occupied + days left) and batches (state, volume,
quality, style). Right: brew panel — dropdown of `Recipes.BrewableRecipes`, a `TryBrew`
button showing the reason on failure. **Verify:** brew succeeds when a fermenter is free,
fails with a reason when full. Commit: `feat(ui): brewery screen`.

### Task B5: World / markets screen

**Files:** `unity/Assets/Scripts/Screens/WorldScreen.cs`.

Render nodes + routes on a simple 2D map (positions from a small per-scenario layout file,
not computed in the sim). Markets show demand/reputation; shipments in transit show origin
→ destination. **Verify:** Burton→London route drawn; dispatching (from a market screen
action) creates a visible shipment. Commit: `feat(ui): world map and markets`.

### Task B6: Tech tree screen

**Files:** `unity/Assets/Scripts/Screens/TechScreen.cs`.

List `TechCatalog` grouped by era, showing prereqs, cost, and lock state from
`ResearchSystem.CanStart`; a "Research" button calls `Commands.TryStartResearch`. Show the
active research's progress bar. **Verify:** can start era-1 tech, blocked on prereq/era.
Commit: `feat(ui): tech tree`.

### Task B7: Events popup

**Files:** `unity/Assets/Scripts/Screens/EventPopup.cs`.

When `Session.State.PendingEvents` is non-empty, show the event + its choices; choosing
calls `Commands.TryResolveEvent`. **Verify:** temperance event (London 1890) appears,
choice applies its ledger/prestige effects. Commit: `feat(ui): event popup`.

### Task B8: Finance screen

**Files:** `unity/Assets/Scripts/Screens/FinanceScreen.cs`.

Balance + `FinanceView` table (per-category totals, history by month). **Verify:** selling
beer moves `BeerSales`; wages hit on the 1st. Commit: `feat(ui): finance screen`.

### Task B9: Staff + save/load

**Files:** `unity/Assets/Scripts/Screens/StaffScreen.cs`, `.../Screens/SaveLoadScreen.cs`.

Staff: list roster, assign to site. Save/load: write `GameSession.Save()` to
`Application.persistentDataPath`, list saved files, load. Autosave on month boundary
(optional). **Verify:** save → new game → load restores exactly. Commit:
`feat(ui): staff and save/load screens`.

---

# Block C — Proof

### Task C1: UI ↔ headless determinism

Run the *same* scripted command sequence through (a) `BreweryEmpire.Headless` and
(b) `GameSession` driven by the UI, and assert `WorldSnapshot.Fingerprint` is identical.
Extend the headless runner with a `--script <file>` mode that replays a JSON command list
(brew/dispatch/research/resolve/advance), so a single command file is the source of truth.
**Verify:** both paths print the same fingerprint. Commit: `test(app): UI and headless replay identically`.

### Task C2: Save/load round-trip from the UI

Play → advance 100 days → save → quit → load → advance 100 more; assert identical to a
session that advanced 200 straight. **Verify:** fingerprints match. Commit:
`test(ui): save/load resume is identical`.

### Task C3: Performance + "do nothing → bankrupt" sanity

Assert per-tick cost stays under the 0.5 ms/frame budget while the UI is live (the core's
existing 10k-tick test already covers the sim; re-check the *session* path). Confirm the
economic canary still holds (idle play bankrupts within ~a year; active play doesn't).
Commit: `test(ui): performance and economic sanity through the session`.

### Task C4: Standalone player build

`File → Build Settings → Build` a Windows standalone (Mono first, then IL2CPP). Run the
same `--script` sequence against the built player and diff the fingerprint. **This is the
final determinism gate** — IL2CPP recompiles the core and can expose float/ordering leaks
the editor hid. Commit: `ci: standalone player determinism check`.

---

## Validation Summary

| Check | How | Expected |
|---|---|---|
| App tests | `dotnet test` | green, +~20 new |
| Core tests | `dotnet test` | 442 still green |
| Publish | `bash tools/publish-unity.sh` | Core.dll + App.dll in Plugins |
| UI determinism | C1 script replay | headless fingerprint == session fingerprint |
| Save/resume | C2 | identical fingerprint |
| Perf | C3 | < 0.5 ms/tick with UI live |
| Player build | C4 | Mono + IL2CPP fingerprints identical |

---

## Risks, tradeoffs, open questions

- **Unity is not installed yet** — Block A is real, testable progress now; Blocks B–C wait.
  The plan is written so Block A doesn't idle.
- **`System.Text.Json` in Unity** (from the spike): validate at B1 first compile; fallback
  is source-linking Core/App or pinning `System.Text.Json` down. Recorded in the
  `deterministic-dotnet-core` skill.
- **No event bus in the core.** `GameSession` + "rebuild views on day boundary" is the
  chosen answer; a push-based event system is YAGNI for a first playable but is the obvious
  upgrade if the UI starts polling wastefully.
- **View models are rebuilt each tick** (O(world size) projection). At 4400 L total capacity
  and a handful of nodes this is negligible; revisit only if the world grows to hundreds of
  sites.
- **Scene/map layout is data, not sim** — node positions live in a Unity-side layout asset,
  deliberately outside the deterministic core (geography ≠ simulation). Keep it there.
- **Scope is "first playable," not "shipped game."** Art, audio, tutorials, and balance are
  explicitly out. The balance pass (economy is still placeholder-tuned — the headless runner
  showed upkeep dominates revenue) is its own later phase and is **not** Phase 5.

**Open questions (non-blocking, defaults chosen):**
1. **uGUI vs UI Toolkit** — plan uses uGUI (immediate, well-trodden, no extra packages).
   UI Toolkit is cleaner long-term but adds ramp-up; switch later if desired.
2. **View-model assembly** — `BreweryEmpire.App` as a separate DLL (chosen) vs folding into
   Unity scripts. Separate keeps TDD alive; fold only if the extra publish step annoys you.
3. **Multi-hop routing** (deferred from Phase 4) and **economy balance** are still open in
   the core — neither blocks the UI, but the map screen will look sparse until multi-hop lands.
