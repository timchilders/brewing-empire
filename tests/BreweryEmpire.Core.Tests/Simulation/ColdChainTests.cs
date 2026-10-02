using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class ColdChainTests
    {
        private static BreweryNode Node(RegionClimate climate) =>
            new BreweryNode(new NodeId("n"), "N", NodeType.Warehouse,
                            new RegionId("r"), WaterProfile.London, climate);

        [Fact]
        public void Ice_House_Harvests_In_A_Freezing_Month_Only()
        {
            var n = Node(RegionClimate.Bavaria);
            n.HasIceHouse = true;

            n.HarvestIce(GameDate.FromYearMonthDay(1750, 1, 15)).Should().BeGreaterThan(0);
            n.HarvestIce(GameDate.FromYearMonthDay(1750, 7, 15)).Should().Be(0);
        }

        [Fact]
        public void No_Ice_House_Means_No_Harvest()
        {
            var n = Node(RegionClimate.Bavaria);
            n.HasIceHouse = false;

            n.HarvestIce(GameDate.FromYearMonthDay(1750, 1, 15)).Should().Be(0);
            n.IceStockTonnes.Should().Be(0);
        }

        [Fact]
        public void Warm_Region_Never_Supports_Ice_Harvest()
        {
            var n = Node(RegionClimate.Mediterranean);
            n.HasIceHouse = true;

            n.HarvestIce(GameDate.FromYearMonthDay(1750, 1, 15)).Should().Be(0);
        }

        [Fact]
        public void Refrigeration_Is_Cold_Regardless_Of_Ice()
        {
            var n = Node(RegionClimate.Mediterranean);
            n.IsRefrigerated = true;
            n.HasIceHouse = false;

            n.HasColdStorage.Should().BeTrue();
        }

        [Fact]
        public void Ice_House_Is_Only_Cold_While_Ice_Lasts()
        {
            var n = Node(RegionClimate.Bavaria);
            n.HasIceHouse = true;
            n.HarvestIce(GameDate.FromYearMonthDay(1750, 1, 15));

            n.HasColdStorage.Should().BeTrue();

            n.ConsumeIce(n.IceStockTonnes);
            n.HasColdStorage.Should().BeFalse();
        }
    }
}
