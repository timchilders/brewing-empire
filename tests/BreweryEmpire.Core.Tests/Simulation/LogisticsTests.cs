using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class LogisticsTests
    {
        // ---- D1: transport modes and Dijkstra ----

        [Fact]
        public void Cheapest_Path_Prefers_A_Cheap_Two_Hop_Chain()
        {
            var graph = new LogisticsGraph();
            graph.AddNode(new NodeId("a"));
            graph.AddNode(new NodeId("b"));
            graph.AddNode(new NodeId("c"));

            graph.AddRoute(new NodeId("a"), new NodeId("b"), 100, TransportMode.Canal);
            graph.AddRoute(new NodeId("b"), new NodeId("c"), 100, TransportMode.Canal);
            graph.AddRoute(new NodeId("a"), new NodeId("c"), 100, TransportMode.SteamRail);

            var path = graph.FindCheapestPath(new NodeId("a"), new NodeId("c"));

            path.Should().NotBeNull();
            path!.Nodes.Select(n => n.Value).Should().ContainInOrder("a", "b", "c");
        }

        [Fact]
        public void Fastest_Path_Prefers_Rail()
        {
            var graph = new LogisticsGraph();
            graph.AddNode(new NodeId("a"));
            graph.AddNode(new NodeId("b"));
            graph.AddNode(new NodeId("c"));

            graph.AddRoute(new NodeId("a"), new NodeId("b"), 50, TransportMode.HorseCart);
            graph.AddRoute(new NodeId("b"), new NodeId("c"), 50, TransportMode.HorseCart);
            graph.AddRoute(new NodeId("a"), new NodeId("c"), 50, TransportMode.SteamRail);

            var path = graph.FindFastestPath(new NodeId("a"), new NodeId("c"));

            path.Should().NotBeNull();
            path!.Nodes.Should().ContainInOrder(new NodeId("a"), new NodeId("c"));
        }

        [Fact]
        public void Disconnected_Nodes_Return_Null()
        {
            var graph = new LogisticsGraph();
            graph.AddNode(new NodeId("a"));
            graph.AddNode(new NodeId("b"));

            graph.FindCheapestPath(new NodeId("a"), new NodeId("b")).Should().BeNull();
        }

        [Fact]
        public void Duplicate_Route_Throws()
        {
            var graph = new LogisticsGraph();
            graph.AddNode(new NodeId("a"));
            graph.AddNode(new NodeId("b"));
            graph.AddRoute(new NodeId("a"), new NodeId("b"), 10, TransportMode.HorseCart);

            System.Action act = () => graph.AddRoute(new NodeId("a"), new NodeId("b"), 10, TransportMode.HorseCart);
            act.Should().Throw<System.InvalidOperationException>();
        }

        [Fact]
        public void Route_To_Unknown_Node_Throws()
        {
            var graph = new LogisticsGraph();
            graph.AddNode(new NodeId("a"));

            System.Action act = () => graph.AddRoute(new NodeId("a"), new NodeId("ghost"), 10, TransportMode.Canal);
            act.Should().Throw<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void Pathfinding_Is_Deterministic()
        {
            string Run()
            {
                var graph = new LogisticsGraph();
                graph.AddNode(new NodeId("a"));
                graph.AddNode(new NodeId("b"));
                graph.AddNode(new NodeId("c"));
                graph.AddNode(new NodeId("d"));

                // Two equal-cost routes a->d: via b and via c.
                graph.AddRoute(new NodeId("a"), new NodeId("b"), 100, TransportMode.Canal);
                graph.AddRoute(new NodeId("b"), new NodeId("d"), 100, TransportMode.Canal);
                graph.AddRoute(new NodeId("a"), new NodeId("c"), 100, TransportMode.Canal);
                graph.AddRoute(new NodeId("c"), new NodeId("d"), 100, TransportMode.Canal);

                var path = graph.FindCheapestPath(new NodeId("a"), new NodeId("d"))!;
                return string.Join(">", path.Nodes.Select(n => n.Value));
            }

            string expected = Run();
            for (int i = 0; i < 50; i++) Run().Should().Be(expected);
        }

        // ---- D2: shipments ----

        [Fact]
        public void A_Three_Day_Route_Arrives_On_Tick_Three()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);

            var shipment = new Shipment(new ShipmentId("s1"), batch.Id,
                new NodeId("burton"), new NodeId("burton"), 3, PackagingType.WoodenCask, litres: 100);

            int arrived = 0;
            for (int day = 1; day <= 3; day++)
            {
                shipment.AdvanceDay();
                if (shipment.IsArrived) arrived = day;
            }

            arrived.Should().Be(3);
        }

        [Fact]
        public void Shipment_Does_Not_Arrive_Early()
        {
            var shipment = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 5, PackagingType.WoodenCask, litres: 100);

            shipment.AdvanceDay();
            shipment.AdvanceDay();
            shipment.IsArrived.Should().BeFalse();
        }

        [Fact]
        public void Ullage_Loss_Reduces_Volume_Per_Leg()
        {
            var shipment = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 1, PackagingType.WoodenCask, litres: 1000);

            int before = shipment.VolumeLitres;
            shipment.ApplyUllageLoss(PackagingSpec.WoodenCask);
            shipment.VolumeLitres.Should().BeLessThan(before);
        }

        [Fact]
        public void Bottles_Lose_Nothing_To_Ullage()
        {
            var shipment = new Shipment(new ShipmentId("s1"), new BatchId("b1"),
                new NodeId("a"), new NodeId("b"), 1, PackagingType.Bottle, litres: 1000);

            int before = shipment.VolumeLitres;
            shipment.ApplyUllageLoss(PackagingSpec.Bottle);
            shipment.VolumeLitres.Should().Be(before);
        }

        [Fact]
        public void Transport_Cost_Is_Charged_On_Dispatch()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            long before = s.Ledger.Balance.Cents;

            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                                            batch.Id, 100, PackagingType.WoodenCask, distanceKm: 10);

            s.Ledger.Balance.Cents.Should().BeLessThan(before);
        }

        [Fact]
        public void Dispatch_Uses_Registered_Route_Mode()
        {
            var s = TestScenario.Standard();
            s.World.AddNode(new BreweryNode(new NodeId("london"), "London", NodeType.Warehouse,
                new RegionId("london"), WaterProfile.London, RegionClimate.Dublin));
            s.World.SetRoute(new NodeId("burton"), new NodeId("london"), 180, TransportMode.SteamRail);
            var batch = BrewAndReady(s);

            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("london"),
                batch.Id, 100, PackagingType.WoodenCask, distanceKm: 999)!;

            sh.Mode.Should().Be(TransportMode.SteamRail);
            sh.Cargo.Should().NotBeNull();
            sh.Cargo!.Style.Should().Be(batch.Style);
            sh.Cargo.QualityBasisPoints.Should().Be(batch.QualityBasisPoints);
        }

        [Fact]
        public void Dispatch_Preserves_Brewing_Work_In_Cargo()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            var sh = LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.WoodenCask, distanceKm: 10)!;

            sh.Cargo!.Flavor.Should().Be(batch.Flavor);
            sh.Cargo.AbvBasisPoints.Should().Be(batch.AbvBasisPoints);
            sh.Cargo.IsPasteurized.Should().Be(batch.IsPasteurized);
        }

        [Fact]
        public void Delivered_Beer_Keeps_Its_Recipe_And_Quality()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.WoodenCask, distanceKm: 1);

            LogisticsSystem.ProcessShipments(s);

            var dest = s.World.Get(new NodeId("burton"));
            var arrived = dest.Batches.SingleOrDefault(b => b.Id != batch.Id && b.RecipeId == batch.RecipeId);
            arrived.Should().NotBeNull();
            arrived!.QualityBasisPoints.Should().Be(batch.QualityBasisPoints);
            arrived.IbuTenths.Should().Be(batch.IbuTenths);
        }

        [Fact]
        public void Cask_Shipment_Arrives_Lighter_Than_Dispatched()
        {
            var s = TestScenario.Standard();
            var batch = BrewAndReady(s);
            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 1000, PackagingType.WoodenCask, distanceKm: 1);

            LogisticsSystem.ProcessShipments(s);

            var dest = s.World.Get(new NodeId("burton"));
            var arrived = dest.Batches.Single(b => b.Id != batch.Id);
            arrived.VolumeLitres.Should().BeLessThan(1000);
        }

        private static Batch BrewAndReady(GameState s)
        {
            var res = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            res.Success.Should().BeTrue();
            var batch = res.Batch!;
            batch.MarkReady();
            return batch;
        }
    }
}
