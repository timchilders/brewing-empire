using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Logistics
{
    public class ShipmentTests
    {
        [Fact]
        public void Defaults_To_HorseCart_Unrefrigerated()
        {
            var s = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 3, PackagingType.WoodenCask, 100);
            s.Mode.Should().Be(TransportMode.HorseCart);
            s.IsRefrigerated.Should().BeFalse();
            s.Cargo.Should().BeNull();
        }

        [Fact]
        public void Can_Carry_Mode_Refrigeration_And_Cargo()
        {
            var cargo = new Batch(new BatchId("c1"), new RecipeId("r1"), "a", new VesselId("v1"),
                                  100, GameDate.FromYearMonthDay(1750, 1, 1), GameDate.FromYearMonthDay(1750, 1, 1));
            var s = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 3, PackagingType.Bottle, 100)
            {
                Mode = TransportMode.SteamRail,
                IsRefrigerated = true,
                Cargo = cargo
            };
            s.Mode.Should().Be(TransportMode.SteamRail);
            s.IsRefrigerated.Should().BeTrue();
            s.Cargo.Should().BeSameAs(cargo);
        }
    }
}
