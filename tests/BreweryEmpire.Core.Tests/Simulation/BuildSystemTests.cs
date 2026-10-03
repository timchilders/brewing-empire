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
            var s = TestScenario.Standard(openingCapitalWhole: 0);
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
    }
}
