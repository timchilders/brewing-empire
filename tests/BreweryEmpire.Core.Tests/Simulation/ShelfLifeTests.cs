using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class ShelfLifeTests
    {
        [Fact]
        public void Batch_Has_A_Positive_Shelf_Life()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            b.ShelfLifeDays.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Dark_Beer_Keeps_Longer_Than_Pale()
        {
            var s = TestScenario.Standard();
            int pale = BrewingSystem.ShelfLifeFor(s, BeerStyle.PaleAle, 500);
            int porter = BrewingSystem.ShelfLifeFor(s, BeerStyle.Porter, 500);
            porter.Should().BeGreaterThan(pale);
        }

        [Fact]
        public void Drum_Roaster_Extends_Dark_Beer_Shelf_Life()
        {
            var s = TestScenario.Standard();
            int before = BrewingSystem.ShelfLifeFor(s, BeerStyle.Stout, 500);
            ResearchSystem.Unlock(s, "drum-roaster");
            int after = BrewingSystem.ShelfLifeFor(s, BeerStyle.Stout, 500);
            after.Should().BeGreaterThan(before);
        }

        [Fact]
        public void Drum_Roaster_Does_Not_Extend_Pale_Ale()
        {
            var s = TestScenario.Standard();
            int before = BrewingSystem.ShelfLifeFor(s, BeerStyle.PaleAle, 500);
            ResearchSystem.Unlock(s, "drum-roaster");
            BrewingSystem.ShelfLifeFor(s, BeerStyle.PaleAle, 500).Should().Be(before);
        }

        [Fact]
        public void Age_Days_Counts_From_Ready()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            b.MarkReady();
            var now = b.ReadyOn.AddDays(10);
            b.AgeDays(now).Should().Be(10);
        }
    }
}
