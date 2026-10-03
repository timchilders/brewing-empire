using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Scenario;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
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
            // Era-4: grant the modern-era techs the scenario implies.
            ResearchSystem.Unlock(s, "refrigeration");
            ResearchSystem.Unlock(s, "bottling-line");
            ResearchSystem.Unlock(s, "stainless-steel");
            return s;
        }

        [Fact]
        public void Refrigerated_Rail_Shipment_Is_Dispatched_Refrigerated()
        {
            var s = London1890();
            var batch = BrewingSystem.TryStartBrew(s, new NodeId("london"), new RecipeId("pale-ale")).Batch!;
            batch.MarkReady();

            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("london"), new NodeId("hamburg"),
                batch.Id, 100, PackagingType.Bottle, TransportMode.SteamRail, 700, refrigerated: true);

            sh.Should().NotBeNull();
            sh!.IsRefrigerated.Should().BeTrue();
            sh.Mode.Should().Be(TransportMode.SteamRail);
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
        public void A_Year_Of_Scenario_Play_Is_Deterministic()
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
