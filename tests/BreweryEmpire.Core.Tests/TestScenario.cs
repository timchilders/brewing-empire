using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Tests
{
    /// <summary>
    /// Builds a realistic, fully-populated game so integration tests exercise
    /// the same shape of state the real game will produce.
    /// </summary>
    public static class TestScenario
    {
        public static GameState Standard(int seed = 12345, long openingCapitalWhole = 2000)
        {
            var start = GameDate.FromYearMonthDay(1750, 1, 1);
            var state = GameState.NewGame(seed, start, Money.FromWhole(openingCapitalWhole));

            var node = new BreweryNode(new NodeId("burton"), "Burton Brewery", NodeType.Brewery,
                                       new RegionId("midlands"), WaterProfile.Burton,
                                       RegionClimate.BurtonEngland)
            {
                DailyOverhead = Money.FromWhole(1)
            };

            node.AddVessel(Vessel.Create("mash-1", VesselType.MashTun, EquipmentTier.Wooden, 2000));
            node.AddVessel(Vessel.Create("ferm-1", VesselType.OpenFermenter, EquipmentTier.Wooden, 1200));
            node.AddVessel(Vessel.Create("ferm-2", VesselType.OpenFermenter, EquipmentTier.Copper, 1200));

            StockIngredients(node, start, kilos: 500);

            state.World.AddNode(node);

            var brewmaster = new StaffMember(new StaffId("bm-1"), "Sam Allsopp",
                                             StaffRole.Brewmaster, 6000,
                                             Money.FromWhole(25), 42, start);
            brewmaster.AssignTo("burton");
            state.Staff.Hire(brewmaster, start);

            state.Recipes["pale-ale"] = PaleAle();

            state.SyncRandomState();
            return state;
        }

        public static void StockIngredients(BreweryNode node, GameDate on, int kilos)
        {
            void Add(string id, int grams, long centsPerKg)
            {
                node.Inventory.AddLot(new IngredientLot(
                    new LotId(id + "-lot-" + on.TotalDays), id, grams, on.Year, 10000,
                    on, Money.FromCents(centsPerKg)));
            }

            Add("pale-malt", kilos * 1000, 45);
            Add("crystal-malt", kilos * 100, 65);
            Add("goldings-hops", kilos * 20, 320);
            Add("ale-yeast", kilos * 5, 150);
        }

        public static Recipe PaleAle()
        {
            var r = new Recipe
            {
                Id = new RecipeId("pale-ale"),
                Name = "Burton Pale Ale",
                TargetVolumeLitres = 1000,
                FermentationDays = 7,
                ConditioningDays = 14,
                Mash = MashSchedule.SingleInfusion(66)
            };

            r.AddGrain("pale-malt", 200_000);
            r.AddGrain("crystal-malt", 20_000);
            r.AddHop("goldings-hops", 2000, 60);
            r.AddHop("goldings-hops", 1000, 10);
            return r;
        }

        /// <summary>Compact fingerprint of the whole world, for determinism checks.</summary>
        public static string Fingerprint(GameState s)
        {
            var parts = new List<string>
            {
                "date=" + s.Date.TotalDays,
                "balance=" + s.Ledger.Balance.Cents,
                "entries=" + s.Ledger.Entries.Count,
                "bankrupt=" + s.IsBankrupt,
                "rep=" + s.ReputationBasisPoints,
                "next=" + s.NextEntityNumber,
                "rng=" + string.Join(",", s.RandomState.Select(x => x.ToString()))
            };

            foreach (var node in s.World.Nodes)
            {
                parts.Add("node=" + node.Id.Value);
                foreach (var v in node.Vessels.OrderBy(v => v.Id.Value, StringComparer.Ordinal))
                    parts.Add("  v=" + v.Id.Value + ":" + v.DaysRemaining + ":" +
                              (v.OccupiedBy?.Value ?? "free") + ":" + v.ConditionBasisPoints);

                foreach (var b in node.Batches.OrderBy(b => b.Id.Value, StringComparer.Ordinal))
                    parts.Add("  b=" + b.Id.Value + ":" + b.State + ":" + b.VolumeLitres + ":" +
                              b.QualityBasisPoints + ":" + b.Infections.Count);

                foreach (var l in node.Inventory.Lots.OrderBy(l => l.Id.Value, StringComparer.Ordinal))
                    parts.Add("  l=" + l.Id.Value + ":" + l.QuantityGrams);
            }

            foreach (var m in s.Staff.All.OrderBy(m => m.Id.Value, StringComparer.Ordinal))
                parts.Add("staff=" + m.Id.Value + ":" + m.LoyaltyBasisPoints + ":" +
                          (m.AssignedNodeId ?? "none"));

            return string.Join("\n", parts);
        }
    }
}
