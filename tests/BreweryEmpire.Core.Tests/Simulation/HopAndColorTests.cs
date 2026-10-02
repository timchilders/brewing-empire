using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class HopAndColorTests
    {
        private static BrewResult Brew(GameState s, Recipe recipe)
        {
            var node = s.World.Get(new NodeId("burton"));
            TestScenario.StockIngredients(node, s.Date, kilos: 2000);
            s.Recipes[recipe.Id.Value] = recipe;
            return BrewingSystem.TryStartBrew(s, new NodeId("burton"), recipe.Id);
        }

        [Fact]
        public void Long_Boil_Yields_More_IBU_Than_Short_Boil()
        {
            int BrewIbu(int boilMinutes)
            {
                var r = new Recipe
                {
                    Id = new RecipeId("ibu-test-" + boilMinutes),
                    Name = "IBU Test",
                    TargetVolumeLitres = 1000,
                    FermentationDays = 7,
                    ConditioningDays = 14
                };
                r.AddGrain("pale-malt", 200_000);
                r.AddHop("goldings-hops", 2000, boilMinutes);

                var s = TestScenario.Standard();
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.IbuTenths;
            }

            BrewIbu(60).Should().BeGreaterThan(BrewIbu(5));
            BrewIbu(5).Should().BeGreaterThan(BrewIbu(0));
        }

        [Fact]
        public void Ibu_Rises_With_Hop_Mass()
        {
            int BrewIbu(int grams)
            {
                var r = new Recipe
                {
                    Id = new RecipeId("ibu-mass-" + grams),
                    Name = "IBU Mass",
                    TargetVolumeLitres = 1000,
                    FermentationDays = 7,
                    ConditioningDays = 14
                };
                r.AddGrain("pale-malt", 200_000);
                r.AddHop("goldings-hops", grams, 60);

                var s = TestScenario.Standard();
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.IbuTenths;
            }

            BrewIbu(4000).Should().BeGreaterThan(BrewIbu(1000));
        }

        [Fact]
        public void More_Roasted_Grain_Yields_Darker_Srm()
        {
            int BrewSrm(int crystalGrams)
            {
                var r = new Recipe
                {
                    Id = new RecipeId("srm-" + crystalGrams),
                    Name = "SRM Test",
                    TargetVolumeLitres = 1000,
                    FermentationDays = 7,
                    ConditioningDays = 14
                };
                r.AddGrain("pale-malt", 200_000);
                r.AddGrain("crystal-malt", crystalGrams);

                var s = TestScenario.Standard();
                var res = Brew(s, r);
                res.Success.Should().BeTrue();
                return res.Batch!.SrmLovibond;
            }

            BrewSrm(50_000).Should().BeGreaterThan(BrewSrm(5_000));
        }

        [Fact]
        public void Ibu_Is_Never_Negative()
        {
            var r = new Recipe
            {
                Id = new RecipeId("ibu-zero"),
                Name = "No Hops",
                TargetVolumeLitres = 1000,
                FermentationDays = 7,
                ConditioningDays = 14
            };
            r.AddGrain("pale-malt", 200_000);

            var s = TestScenario.Standard();
            var res = Brew(s, r);

            res.Success.Should().BeTrue();
            res.Batch!.IbuTenths.Should().Be(0);
        }
    }
}
