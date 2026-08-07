using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Sites
{
    public class BreweryNodeTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);

        private static BreweryNode NewNode(string id = "burton") =>
            new BreweryNode(new NodeId(id), "Burton Brewery", NodeType.Brewery,
                            new RegionId("midlands"), WaterProfile.Burton,
                            RegionClimate.BurtonEngland);

        [Fact]
        public void Vessels_Can_Be_Added_And_Are_Unique()
        {
            var n = NewNode();
            n.AddVessel(Vessel.Create("v1", VesselType.OpenFermenter, EquipmentTier.Wooden, 1000));

            Action duplicate = () => n.AddVessel(
                Vessel.Create("v1", VesselType.Copper, EquipmentTier.Copper, 500));

            duplicate.Should().Throw<InvalidOperationException>();
            n.Vessels.Should().ContainSingle();
        }

        [Fact]
        public void Finds_The_Smallest_Vessel_That_Fits()
        {
            var n = NewNode();
            n.AddVessel(Vessel.Create("big", VesselType.OpenFermenter, EquipmentTier.Copper, 5000));
            n.AddVessel(Vessel.Create("small", VesselType.OpenFermenter, EquipmentTier.Copper, 500));
            n.AddVessel(Vessel.Create("medium", VesselType.OpenFermenter, EquipmentTier.Copper, 1500));

            n.FindAvailableVessel(VesselType.OpenFermenter, 400)!.Id.Value.Should().Be("small");
            n.FindAvailableVessel(VesselType.OpenFermenter, 1000)!.Id.Value.Should().Be("medium");
            n.FindAvailableVessel(VesselType.OpenFermenter, 4000)!.Id.Value.Should().Be("big");
        }

        [Fact]
        public void Returns_Null_When_Nothing_Fits()
        {
            var n = NewNode();
            n.AddVessel(Vessel.Create("v1", VesselType.OpenFermenter, EquipmentTier.Copper, 100));

            n.FindAvailableVessel(VesselType.OpenFermenter, 5000).Should().BeNull();
            n.FindAvailableVessel(VesselType.MashTun, 50).Should().BeNull();
        }

        [Fact]
        public void Occupied_Vessels_Are_Not_Offered()
        {
            var n = NewNode();
            var v = Vessel.Create("v1", VesselType.OpenFermenter, EquipmentTier.Copper, 1000);
            n.AddVessel(v);
            v.Occupy(new BatchId("b1"), 5);

            n.FindAvailableVessel(VesselType.OpenFermenter, 500).Should().BeNull();
        }

        [Fact]
        public void Vessel_Selection_Is_Deterministic_On_Equal_Capacity()
        {
            for (int run = 0; run < 5; run++)
            {
                var n = NewNode();
                n.AddVessel(Vessel.Create("zebra", VesselType.Copper, EquipmentTier.Copper, 1000));
                n.AddVessel(Vessel.Create("alpha", VesselType.Copper, EquipmentTier.Copper, 1000));

                n.FindAvailableVessel(VesselType.Copper, 100)!.Id.Value.Should().Be("alpha");
            }
        }

        [Fact]
        public void Total_Capacity_Sums_Vessels()
        {
            var n = NewNode();
            n.AddVessel(Vessel.Create("a", VesselType.Copper, EquipmentTier.Copper, 1000));
            n.AddVessel(Vessel.Create("b", VesselType.OpenFermenter, EquipmentTier.Copper, 2000));

            n.TotalCapacityLitres.Should().Be(3000);
        }

        [Fact]
        public void Daily_Upkeep_Includes_Overhead_And_Vessels()
        {
            var n = NewNode();
            n.DailyOverhead = Money.FromWhole(5);
            n.AddVessel(Vessel.Create("a", VesselType.Copper, EquipmentTier.Copper, 1000));

            n.TotalDailyUpkeep.Should().BeGreaterThan(Money.FromWhole(5));
        }

        [Fact]
        public void Average_Hygiene_Reflects_Vessel_Mix()
        {
            var n = NewNode();
            n.AddVessel(Vessel.Create("wood", VesselType.OpenFermenter, EquipmentTier.Wooden, 1000));
            n.AddVessel(Vessel.Create("steel", VesselType.StainlessTank, EquipmentTier.Stainless, 1000));

            int expected = (Vessel.BaseHygieneFor(EquipmentTier.Wooden) +
                            Vessel.BaseHygieneFor(EquipmentTier.Stainless)) / 2;

            n.AverageHygieneBasisPoints.Should().Be(expected);
        }

        [Fact]
        public void Empty_Node_Has_Zero_Hygiene()
        {
            NewNode().AverageHygieneBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Advancing_A_Day_Ticks_Every_Vessel()
        {
            var n = NewNode();
            var v = Vessel.Create("v1", VesselType.OpenFermenter, EquipmentTier.Copper, 1000);
            n.AddVessel(v);
            v.Occupy(new BatchId("b1"), 2);

            n.AdvanceVesselsOneDay();
            v.IsAvailable.Should().BeFalse();

            n.AdvanceVesselsOneDay();
            v.IsAvailable.Should().BeTrue();
        }

        [Fact]
        public void Batches_Can_Be_Added_And_Removed()
        {
            var n = NewNode();
            n.AddBatch(new Batch(new BatchId("b1"), new RecipeId("r"), "burton",
                                 new VesselId("v1"), 500, D(0), D(20)));

            n.Batches.Should().ContainSingle();
            n.RemoveBatch(new BatchId("b1")).Should().BeTrue();
            n.RemoveBatch(new BatchId("ghost")).Should().BeFalse();
            n.Batches.Should().BeEmpty();
        }

        [Fact]
        public void Container_Pools_Are_Created_On_Demand_And_Reused()
        {
            var n = NewNode();
            var first = n.GetOrCreateContainerPool(
                BreweryEmpire.Core.Model.Packaging.PackagingType.WoodenCask, Money.FromWhole(3));
            var second = n.GetOrCreateContainerPool(
                BreweryEmpire.Core.Model.Packaging.PackagingType.WoodenCask, Money.FromWhole(3));

            second.Should().BeSameAs(first);
            n.Containers.Should().HaveCount(1);
        }

        [Fact]
        public void Node_Carries_Its_Own_Water_And_Climate()
        {
            var n = NewNode();
            n.Water.Should().Be(WaterProfile.Burton);
            n.Climate.SupportsWinterIceHarvest.Should().BeFalse();
        }
    }

    public class WorldMapTests
    {
        private static BreweryNode Node(string id) =>
            new BreweryNode(new NodeId(id), id, NodeType.Brewery, new RegionId("r"),
                            WaterProfile.London, RegionClimate.BurtonEngland);

        [Fact]
        public void Nodes_Can_Be_Added_And_Fetched()
        {
            var map = new WorldMap();
            map.AddNode(Node("burton"));

            map.Get(new NodeId("burton")).Name.Should().Be("burton");
            map.NodeCount.Should().Be(1);
        }

        [Fact]
        public void Duplicate_Nodes_Are_Rejected()
        {
            var map = new WorldMap();
            map.AddNode(Node("burton"));

            Action act = () => map.AddNode(Node("burton"));
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Unknown_Node_Lookup_Throws()
        {
            var map = new WorldMap();
            Action act = () => map.Get(new NodeId("nowhere"));
            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public void Routes_Are_Symmetric()
        {
            var map = new WorldMap();
            var a = new NodeId("burton");
            var b = new NodeId("london");

            map.SetRoute(a, b, 200);

            map.DistanceKm(a, b).Should().Be(200);
            map.DistanceKm(b, a).Should().Be(200);
            map.HasRoute(b, a).Should().BeTrue();
        }

        [Fact]
        public void Missing_Route_Throws()
        {
            var map = new WorldMap();
            Action act = () => map.DistanceKm(new NodeId("a"), new NodeId("b"));
            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public void Self_Routes_Are_Rejected()
        {
            var map = new WorldMap();
            Action act = () => map.SetRoute(new NodeId("a"), new NodeId("a"), 10);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Negative_Distance_Is_Rejected()
        {
            var map = new WorldMap();
            Action act = () => map.SetRoute(new NodeId("a"), new NodeId("b"), -1);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Node_Enumeration_Is_Deterministically_Ordered()
        {
            var map = new WorldMap();
            map.AddNode(Node("zebra"));
            map.AddNode(Node("alpha"));
            map.AddNode(Node("mango"));

            map.Nodes.Select(n => n.Id.Value)
               .Should().ContainInOrder("alpha", "mango", "zebra");
        }
    }
}
