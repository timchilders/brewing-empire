using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class TechEffectTests
    {
        private static GameState WithRecipe(BeerStyle style, string id)
        {
            var s = TestScenario.Standard();
            var r = TestScenario.PaleAle();
            r.Style = style;
            s.Recipes[id] = r;
            return s;
        }

        [Fact]
        public void Locked_Style_Is_Rejected()
        {
            var s = WithRecipe(BeerStyle.Porter, "porter");
            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"));
            result.Success.Should().BeFalse();
            result.Reason.Should().Be(BrewFailureReason.LockedStyle);
        }

        [Fact]
        public void Unlocked_Style_Brews()
        {
            var s = WithRecipe(BeerStyle.Porter, "porter");
            ResearchSystem.Unlock(s, "malting-kilns");
            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"));
            result.Success.Should().BeTrue();
        }

        [Fact]
        public void Saccharometer_Raises_Original_Gravity()
        {
            static int Og(bool hasTech)
            {
                var s = TestScenario.Standard();
                if (hasTech) ResearchSystem.Unlock(s, "saccharometer");
                var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                return r.Batch!.OriginalGravityPoints;
            }
            Og(hasTech: true).Should().BeGreaterThan(Og(hasTech: false));
        }

        [Fact]
        public void Water_Chemistry_Bonus_Tracks_Water_Fit()
        {
            static int Og(bool hasTech)
            {
                var s = TestScenario.Standard();   // Burton water + Pale Ale = perfect fit
                if (hasTech) ResearchSystem.Unlock(s, "water-chemistry");
                var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                return r.Batch!.OriginalGravityPoints;
            }
            Og(hasTech: true).Should().BeGreaterThan(Og(hasTech: false));
        }

        [Fact]
        public void Pure_Yeast_Raises_Base_Quality()
        {
            static int Q(bool hasTech)
            {
                var s = TestScenario.Standard();
                if (hasTech) ResearchSystem.Unlock(s, "pure-yeast");
                var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                return r.Batch!.QualityBasisPoints;
            }
            Q(hasTech: true).Should().BeGreaterThan(Q(hasTech: false));
        }

        [Fact]
        public void Pure_Yeast_Never_Rolls_Wild_Yeast()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Unlock(s, "pure-yeast");
            for (int i = 0; i < 5000; i++)
                SpoilageSystem.RollOrganism(s).Should().NotBe(SpoilageOrganism.WildYeast);
        }

        [Fact]
        public void Pasteurization_Requires_Tech_And_Ready_Batch()
        {
            var s = TestScenario.Standard();
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!;

            PasteurizationSystem.TryPasteurize(s, new NodeId("burton"), batch.Id).Should().BeFalse(); // no tech

            ResearchSystem.Unlock(s, "pasteurization");
            PasteurizationSystem.TryPasteurize(s, new NodeId("burton"), batch.Id).Should().BeFalse(); // not ready
        }

        [Fact]
        public void Pasteurization_Sets_Flag_And_Lowers_Quality()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Unlock(s, "pasteurization");
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!;
            batch.MarkReady();
            int before = batch.QualityBasisPoints;

            PasteurizationSystem.TryPasteurize(s, new NodeId("burton"), batch.Id).Should().BeTrue();

            batch.IsPasteurized.Should().BeTrue();
            batch.QualityBasisPoints.Should().BeLessThan(before);
        }

        [Fact]
        public void Bottle_Shipment_Requires_Bottling_Line()
        {
            var s = TestScenario.Standard();
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!;
            batch.MarkReady();

            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.Bottle, 10)
                .Should().BeNull();

            ResearchSystem.Unlock(s, "bottling-line");

            LogisticsSystem.DispatchShipment(s, new NodeId("burton"), new NodeId("burton"),
                batch.Id, 100, PackagingType.Bottle, 10)
                .Should().NotBeNull();
        }

        [Fact]
        public void Refrigerated_Shipping_Requires_Refrigeration_Tech_And_Rail()
        {
            var s = TestScenario.Standard();
            ResearchSystem.CanRefrigerateShipment(s, TransportMode.SteamRail).Should().BeFalse();
            ResearchSystem.CanRefrigerateShipment(s, TransportMode.HorseCart).Should().BeFalse();

            ResearchSystem.Unlock(s, "refrigeration");
            ResearchSystem.CanRefrigerateShipment(s, TransportMode.SteamRail).Should().BeTrue();
            ResearchSystem.CanRefrigerateShipment(s, TransportMode.HorseCart).Should().BeFalse();
        }
    }
}
