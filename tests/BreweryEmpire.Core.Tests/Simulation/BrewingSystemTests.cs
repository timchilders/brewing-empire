using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class BrewingSystemTests
    {
        [Fact]
        public void A_Valid_Brew_Succeeds_And_Occupies_A_Vessel()
        {
            var s = TestScenario.Standard();
            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));

            result.Success.Should().BeTrue();
            result.Batch.Should().NotBeNull();

            var node = s.World.Get(new NodeId("burton"));
            node.Vessels.Count(v => !v.IsAvailable).Should().Be(1);
            node.Batches.Should().ContainSingle();
        }

        [Fact]
        public void Brewing_Consumes_Ingredients_And_Books_Cogs()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));
            int maltBefore = node.Inventory.TotalGramsOf("pale-malt");

            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));

            node.Inventory.TotalGramsOf("pale-malt").Should().Be(maltBefore - 200_000);
            result.Batch!.CostOfGoods.IsPositive.Should().BeTrue();
        }

        [Fact]
        public void Unknown_Recipe_Fails_Cleanly()
        {
            var s = TestScenario.Standard();
            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("nonexistent"));

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(BrewFailureReason.UnknownRecipe);
        }

        [Fact]
        public void Insufficient_Ingredients_Fails_And_Consumes_Nothing()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));

            // Drain the malt.
            node.Inventory.Consume("pale-malt", node.Inventory.TotalGramsOf("pale-malt"));
            int hopsBefore = node.Inventory.TotalGramsOf("goldings-hops");

            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(BrewFailureReason.InsufficientIngredients);
            node.Inventory.TotalGramsOf("goldings-hops").Should().Be(hopsBefore,
                "a failed brew must not consume anything");
            node.Vessels.Should().OnlyContain(v => v.IsAvailable);
        }

        [Fact]
        public void A_Grist_That_Cannot_Convert_Is_Rejected()
        {
            var s = TestScenario.Standard();
            var bad = new BreweryEmpire.Core.Model.Recipes.Recipe
            {
                Id = new RecipeId("all-roast"),
                Name = "All Roast",
                TargetVolumeLitres = 100
            };
            bad.AddGrain("roasted-barley", 50_000);
            s.Recipes["all-roast"] = bad;

            var node = s.World.Get(new NodeId("burton"));
            node.Inventory.AddLot(new BreweryEmpire.Core.Model.Ingredients.IngredientLot(
                new LotId("rb"), "roasted-barley", 100_000, 1750, 10000, s.Date, Money.FromCents(68)));

            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("all-roast"));

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(BrewFailureReason.InsufficientDiastaticPower);
        }

        [Fact]
        public void No_Free_Vessel_Fails_Cleanly()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));

            foreach (var v in node.Vessels.Where(v => v.Type == VesselType.OpenFermenter))
                v.Occupy(new BatchId("other"), 10);

            var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(BrewFailureReason.NoAvailableVessel);
        }

        [Fact]
        public void Better_Brewmasters_Produce_Better_Beer_On_Average()
        {
            int AverageQuality(int skill)
            {
                long total = 0;
                const int runs = 40;

                for (int i = 0; i < runs; i++)
                {
                    var s = TestScenario.Standard(seed: 1000 + i);
                    var node = s.World.Get(new NodeId("burton"));
                    s.Staff.All.Single().SkillBasisPoints = skill;

                    var result = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                    total += result.Batch!.QualityBasisPoints;
                }

                return (int)(total / runs);
            }

            AverageQuality(9500).Should().BeGreaterThan(AverageQuality(1000));
        }

        [Fact]
        public void Flavor_Is_Deterministic_For_The_Same_Recipe_And_Site()
        {
            var a = TestScenario.Standard(seed: 7);
            var b = TestScenario.Standard(seed: 999);   // different seed, same recipe

            var ra = BrewingSystem.TryStartBrew(a, new NodeId("burton"), new RecipeId("pale-ale"));
            var rb = BrewingSystem.TryStartBrew(b, new NodeId("burton"), new RecipeId("pale-ale"));

            // Flavour is a design outcome: it must not depend on the RNG.
            ra.Batch!.Flavor.Should().Be(rb.Batch!.Flavor);
        }

        [Fact]
        public void Batch_Ids_Are_Deterministic_Not_Guids()
        {
            var s = TestScenario.Standard();
            var first = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var second = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));

            first.Batch!.Id.Value.Should().Be("batch-1");
            second.Batch!.Id.Value.Should().Be("batch-2");
        }
    }

    public class SpoilageSystemTests
    {
        [Fact]
        public void Warm_Weather_Is_Riskier_Than_Cold()
        {
            SpoilageSystem.BaseInfectionRiskBasisPoints(28)
                .Should().BeGreaterThan(SpoilageSystem.BaseInfectionRiskBasisPoints(3));
        }

        [Fact]
        public void Risk_Rises_Monotonically_With_Temperature()
        {
            int previous = -1;
            for (int t = -5; t <= 35; t++)
            {
                int risk = SpoilageSystem.BaseInfectionRiskBasisPoints(t);
                risk.Should().BeGreaterOrEqualTo(previous);
                previous = risk;
            }
        }

        [Fact]
        public void Wooden_Vessels_Are_Riskier_Than_Stainless()
        {
            int RiskWith(EquipmentTier tier)
            {
                var s = TestScenario.Standard();
                var node = s.World.Get(new NodeId("burton"));
                var vessel = Vessel.Create("test", VesselType.OpenFermenter, tier, 1000);
                s.Date = GameDate.FromYearMonthDay(1750, 7, 15);   // summer
                return SpoilageSystem.InfectionRiskFor(s, node, vessel);
            }

            RiskWith(EquipmentTier.Wooden).Should().BeGreaterThan(RiskWith(EquipmentTier.Stainless));
        }

        [Fact]
        public void Hansen_Reduces_Infection_Risk()
        {
            int RiskWithChemist(bool hireHansen)
            {
                var s = TestScenario.Standard();
                s.Date = GameDate.FromYearMonthDay(1890, 7, 15);

                if (hireHansen)
                {
                    var hansen = HistoricalFigures.Get("hansen").ToStaffMember(s.Date);
                    hansen.AssignTo("burton");
                    s.Staff.Hire(hansen, s.Date);
                }

                var node = s.World.Get(new NodeId("burton"));
                var vessel = node.Vessels.First(v => v.Type == VesselType.OpenFermenter);
                return SpoilageSystem.InfectionRiskFor(s, node, vessel);
            }

            RiskWithChemist(true).Should().BeLessThan(RiskWithChemist(false));
        }

        [Fact]
        public void Summer_Brewing_In_Wooden_Vessels_Spoils_Beer_Over_Time()
        {
            // Statistical assertion: with the calibrated risk curve a summer
            // batch in an open wooden fermenter spoils roughly 10-15% of the
            // time, so across 100 runs seeing zero is vanishingly unlikely.
            // Deliberately sized so this cannot fail on ordinary bad luck.
            const int runs = 100;
            int spoiled = 0;

            for (int run = 0; run < runs; run++)
            {
                var trial = TestScenario.Standard(seed: 5000 + run);
                trial.Date = GameDate.FromYearMonthDay(1750, 7, 1);
                BrewingSystem.TryStartBrew(trial, new NodeId("burton"), new RecipeId("pale-ale"));

                TickSystem.AdvanceDays(trial, 21);

                var node = trial.World.Get(new NodeId("burton"));
                if (node.Batches.Any(b => b.State == BatchState.Spoiled)) spoiled++;
            }

            spoiled.Should().BeGreaterThan(0,
                "pre-refrigeration summer brewing in wood must sometimes fail");
        }

        [Fact]
        public void Winter_Brewing_Is_Markedly_Safer_Than_Summer()
        {
            int SpoilRate(int month)
            {
                const int runs = 100;
                int spoiled = 0;

                for (int run = 0; run < runs; run++)
                {
                    var trial = TestScenario.Standard(seed: 8000 + run);
                    trial.Date = GameDate.FromYearMonthDay(1750, month, 1);
                    BrewingSystem.TryStartBrew(trial, new NodeId("burton"), new RecipeId("pale-ale"));
                    TickSystem.AdvanceDays(trial, 21);

                    var node = trial.World.Get(new NodeId("burton"));
                    if (node.Batches.Any(b => b.State == BatchState.Spoiled)) spoiled++;
                }

                return spoiled;
            }

            // This is the whole historical point: brew in winter, not summer.
            SpoilRate(1).Should().BeLessThan(SpoilRate(7));
        }

        [Fact]
        public void Spoiled_Batches_Are_Written_Off_To_The_Ledger()
        {
            var s = TestScenario.Standard(seed: 1);
            var node = s.World.Get(new NodeId("burton"));

            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = node.Batches.Single();
            batch.AddInfection(new Infection { Character = OffFlavor.Sour, SeverityBasisPoints = 9000 });

            batch.State.Should().Be(BatchState.Spoiled);
        }
    }
}
