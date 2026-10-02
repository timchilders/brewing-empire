using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    /// <summary>
    /// Task A1: a brew yields a measured OG (possibly short of the recipe
    /// target), a FG from attenuation, and an ABV from the two — all integer,
    /// all deterministic.
    /// </summary>
    public class BrewingChemistryTests
    {
        private static GameState Standard() => TestScenario.Standard();

        private static BrewResult Brew(GameState s, Recipe recipe)
        {
            var node = s.World.Get(new NodeId("burton"));
            // Ensure ingredients are stocked for whatever the recipe asks for.
            TestScenario.StockIngredients(node, s.Date, kilos: 2000);
            s.Recipes[recipe.Id.Value] = recipe;
            return BrewingSystem.TryStartBrew(s, new NodeId("burton"), recipe.Id);
        }

        [Fact]
        public void Stainless_Yields_Higher_OG_Than_Wooden()
        {
            // Same grist, same site water; only the fermenter tier differs.
            var recipe = TestScenario.PaleAle();
            recipe.TargetOriginalGravityPoints = 50;   // 1.050

            int BrewOnTier(EquipmentTier tier)
            {
                var s = Standard();
                var node = s.World.Get(new NodeId("burton"));
                var vessel = node.Vessels.First(v => v.Type == VesselType.OpenFermenter);
                vessel.Tier = tier;
                vessel.HygieneBasisPoints = Vessel.BaseHygieneFor(tier);

                var r = Brew(s, recipe);
                r.Success.Should().BeTrue();
                return r.Batch!.OriginalGravityPoints;
            }

            BrewOnTier(EquipmentTier.Stainless).Should().BeGreaterThan(BrewOnTier(EquipmentTier.Wooden));
        }

        [Fact]
        public void Low_Diastatic_Grist_Yields_Lower_OG()
        {
            // All pale malt (~140 Lintner) vs a munich-heavy grist (~105 Lintner)
            // at the same tier: the enzyme-poor grist extracts less sugar.
            int BrewOg(Recipe r)
            {
                var s = Standard();
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.OriginalGravityPoints;
            }

            var pale = TestScenario.PaleAle();
            pale.TargetOriginalGravityPoints = 50;

            var munich = new Recipe
            {
                Id = new RecipeId("munich-heavy"),
                Name = "Munich Heavy",
                TargetVolumeLitres = 1000,
                TargetOriginalGravityPoints = 50,
                FermentationDays = 7,
                ConditioningDays = 14
            };
            munich.AddGrain("pale-malt", 100_000);
            munich.AddGrain("munich-malt", 120_000);   // DP 70, drags the weighted average down

            // Stock munich-malt for the second run (StockIngredients lacks it).
            int BrewMunich(Recipe r)
            {
                var s = Standard();
                var node = s.World.Get(new NodeId("burton"));
                node.Inventory.AddLot(new BreweryEmpire.Core.Model.Ingredients.IngredientLot(
                    new LotId("munich-lot"), "munich-malt", 1_000_000, 1750, 10000, s.Date,
                    BreweryEmpire.Core.Economy.Money.FromCents(52)));
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.OriginalGravityPoints;
            }

            BrewOg(pale).Should().BeGreaterThan(BrewMunich(munich));
        }

        [Fact]
        public void Lower_Mash_Temperature_Yields_Higher_ABV_And_Lower_Body()
        {
            int BrewAbv(int mashTempC)
            {
                var r = TestScenario.PaleAle();
                r.TargetOriginalGravityPoints = 50;
                r.Mash = MashSchedule.SingleInfusion(mashTempC);

                var s = Standard();
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.AbvBasisPoints;
            }

            int BrewBody(int mashTempC)
            {
                var r = TestScenario.PaleAle();
                r.TargetOriginalGravityPoints = 50;
                r.Mash = MashSchedule.SingleInfusion(mashTempC);

                var s = Standard();
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.Flavor.BodyBasisPoints;
            }

            // 63C = dry/thin (high ABV, low body); 72C = sweet/full (low ABV, high body).
            BrewAbv(63).Should().BeGreaterThan(BrewAbv(72));
            BrewBody(63).Should().BeLessThan(BrewBody(72));
        }

        [Fact]
        public void Abv_Is_Derived_From_Og_And_Fg()
        {
            var s = Standard();
            var res = Brew(s, TestScenario.PaleAle());
            res.Success.Should().BeTrue();

            var b = res.Batch!;
            int expected = (b.OriginalGravityPoints - b.FinalGravityPoints) * 131 / 10;

            b.AbvBasisPoints.Should().Be(expected);
            b.AbvBasisPoints.Should().BeGreaterThan(0);
            b.FinalGravityPoints.Should().BeLessThan(b.OriginalGravityPoints);
        }
    }
}
