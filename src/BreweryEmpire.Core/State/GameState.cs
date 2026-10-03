using System;
using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;

namespace BreweryEmpire.Core.State
{
    /// <summary>
    /// The complete simulation state. Everything needed to resume a game must
    /// live here — if it is not in GameState it does not survive a save.
    ///
    /// SaveVersion exists from day one because adding it later is a migration
    /// nightmare; the loader can then branch on it forever.
    /// </summary>
    public sealed class GameState
    {
        public const int CurrentSaveVersion = 4;

        public int SaveVersion { get; set; } = CurrentSaveVersion;
        public int Seed { get; set; }
        public GameDate Date { get; set; }
        public Ledger Ledger { get; set; } = new Ledger();
        public WorldMap World { get; set; } = new WorldMap();
        public Roster Staff { get; set; } = new Roster();
        public PriceBook Prices { get; set; } = new PriceBook();

        /// <summary>Player-authored recipes, keyed by id.</summary>
        public Dictionary<string, Recipe> Recipes { get; set; } =
            new Dictionary<string, Recipe>(StringComparer.Ordinal);

        /// <summary>Markets where beer is sold. Owned and neutral alike.</summary>
        public List<BreweryEmpire.Core.Model.Markets.MarketNode> Markets { get; set; } =
            new List<BreweryEmpire.Core.Model.Markets.MarketNode>();

        /// <summary>Shipments in transit between nodes.</summary>
        public List<BreweryEmpire.Core.Model.Logistics.Shipment> Shipments { get; set; } =
            new List<BreweryEmpire.Core.Model.Logistics.Shipment>();

        /// <summary>Reputation drives price realisation; starts neutral.</summary>
        public int ReputationBasisPoints { get; set; } = 5000;

        /// <summary>Campaign-level research progress.</summary>
        public ResearchState Research { get; set; } = new ResearchState();

        /// <summary>Global prestige score; raised by tech/events, never negative.</summary>
        public int PrestigeBasisPoints { get; set; }

        /// <summary>Events awaiting a player choice.</summary>
        public List<BreweryEmpire.Core.Model.Events.GameEvent> PendingEvents { get; set; } =
            new List<BreweryEmpire.Core.Model.Events.GameEvent>();

        /// <summary>Ids of events already resolved (one-shot dedupe).</summary>
        public List<string> ResolvedEventIds { get; set; } = new List<string>();

        public bool IsBankrupt { get; set; }

        /// <summary>Monotonic counter used to mint deterministic entity ids.</summary>
        public int NextEntityNumber { get; set; } = 1;

        /// <summary>
        /// Rival brewers. A stub in Phase 1 — the field exists so saves written
        /// now remain loadable once Phase 2 fills the type in.
        /// </summary>
        public List<RivalBrewer> Rivals { get; set; } = new List<RivalBrewer>();

        /// <summary>PRNG state, persisted so a reload continues the same stream.</summary>
        public ulong[] RandomState { get; set; } = Array.Empty<ulong>();

        [System.Text.Json.Serialization.JsonIgnore]
        public DeterministicRandom Random { get; private set; } = new DeterministicRandom(0);

        [System.Text.Json.Serialization.JsonIgnore]
        public IngredientCatalog Catalog { get; set; } = IngredientCatalog.CreateDefault();

        public static GameState NewGame(int seed, GameDate start, Money openingCapital)
        {
            var state = new GameState
            {
                Seed = seed,
                Date = start,
                Random = new DeterministicRandom(seed)
            };

            state.Ledger = new Ledger(openingCapital, start);
            state.SyncRandomState();
            return state;
        }

        /// <summary>Mint a deterministic id — never Guid.NewGuid, which would break replays.</summary>
        public string MintId(string prefix) => prefix + "-" + NextEntityNumber++;

        /// <summary>Copy live PRNG state into the serializable field. Call before saving.</summary>
        public void SyncRandomState() => RandomState = Random.State;

        /// <summary>Rebuild the live PRNG from deserialized state. Call after loading.</summary>
        public void RestoreRandom()
        {
            Random = RandomState != null && RandomState.Length == 2
                ? DeterministicRandom.FromState(RandomState)
                : new DeterministicRandom(Seed);
        }
    }

    /// <summary>
    /// Placeholder competitor. Phase 2 gives them behaviour; for now they only
    /// need to round-trip through a save.
    /// </summary>
    public sealed class RivalBrewer
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int StrengthBasisPoints { get; set; }
        public string HomeRegionId { get; set; } = string.Empty;
    }
}
