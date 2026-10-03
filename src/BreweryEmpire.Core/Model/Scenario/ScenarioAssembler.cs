using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
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
                    DailyOverhead = Money.FromCents(site.DailyOverheadCents)
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
                    id, grams, on.Year, 10000, on, Money.FromCents(centsPerKg)));

            Add("pale-malt", kilos * 1000, 45);
            Add("crystal-malt", kilos * 100, 65);
            Add("goldings-hops", kilos * 20, 320);
            Add("ale-yeast", kilos * 5, 150);
        }
    }
}
