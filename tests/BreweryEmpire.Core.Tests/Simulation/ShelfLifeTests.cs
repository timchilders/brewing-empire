using System.Linq;
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

        [Fact]
        public void Staleness_Decay_Applies_Only_Past_Shelf_Life()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            b.MarkReady();

            SpoilageSystem.StalenessDecayBasisPoints(b, b.ReadyOn.AddDays(1)).Should().Be(0);
            SpoilageSystem.StalenessDecayBasisPoints(b, b.ReadyOn.AddDays(b.ShelfLifeDays + 1))
                .Should().BeGreaterThan(0);
        }

        [Fact]
        public void Stale_Ready_Beer_Loses_Quality()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            b.MarkReady();
            b.SetQuality(5000);
            var node = s.World.Get(new NodeId("burton"));

            s.Date = b.ReadyOn.AddDays(b.ShelfLifeDays + 1);
            SpoilageSystem.ProcessNode(s, node);

            b.QualityBasisPoints.Should().BeLessThan(5000);
        }

        [Fact]
        public void Shelf_Life_Round_Trips_Through_Save()
        {
            var s = TestScenario.Standard();
            var b = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale")).Batch!;
            int expected = b.ShelfLifeDays;

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));
            var rb = reloaded.World.Get(new NodeId("burton")).Batches.First(x => x.Id == b.Id);
            rb.ShelfLifeDays.Should().Be(expected);
        }
    }
}
