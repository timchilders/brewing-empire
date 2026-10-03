using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class TransitSpoilageTests
    {
        private static Batch MakeInfectedCargo()
        {
            var b = new Batch(new BatchId("cargo"), new RecipeId("r"), "burton", new VesselId("v"),
                              100, GameDate.FromYearMonthDay(1750, 7, 1), GameDate.FromYearMonthDay(1750, 7, 1));
            b.ContractInfection(SpoilageOrganism.Lactobacillus, "test");
            return b;
        }

        private static Shipment ShipmentWith(Batch cargo, TransportMode mode, bool refrigerated) =>
            new Shipment(new ShipmentId("s1"), new BatchId("b"), new NodeId("burton"), new NodeId("burton"),
                         3, PackagingType.WoodenCask, 100)
            {
                Cargo = cargo,
                Mode = mode,
                IsRefrigerated = refrigerated
            };

        [Fact]
        public void Cargo_Ship_Is_Most_Spoilage_Prone()
        {
            TransportSpec.For(TransportMode.CargoShip).SpoilageModifierBasisPoints
                .Should().BeGreaterThan(TransportSpec.For(TransportMode.HorseCart).SpoilageModifierBasisPoints);
        }

        [Fact]
        public void Infected_Cargo_Progresses_In_Transit()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1750, 7, 15);
            var cargo = MakeInfectedCargo();
            var sh = ShipmentWith(cargo, TransportMode.HorseCart, refrigerated: false);

            SpoilageSystem.ProcessShipment(s, sh);

            cargo.Infections.Single(i => !i.IsIntentionalSour).ProgressionBasisPoints.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Refrigerated_Cargo_Progresses_Slower()
        {
            int Run(bool refrigerated)
            {
                var s = TestScenario.Standard();
                s.Date = GameDate.FromYearMonthDay(1750, 7, 15);
                var cargo = MakeInfectedCargo();
                var sh = ShipmentWith(cargo, TransportMode.SteamRail, refrigerated);
                SpoilageSystem.ProcessShipment(s, sh);
                return cargo.Infections.Single(i => !i.IsIntentionalSour).ProgressionBasisPoints;
            }

            Run(true).Should().BeLessThan(Run(false));
        }
    }
}
