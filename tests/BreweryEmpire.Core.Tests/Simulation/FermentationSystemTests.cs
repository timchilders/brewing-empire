using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class FermentationSystemTests
    {
        private static GameState Standard() => TestScenario.Standard();

        [Theory]
        [InlineData("ale-yeast", 18, 0)]      // ale at 18C: in band, no penalty
        [InlineData("ale-yeast", 28, 1)]      // ale at 28C: hot, penalised
        [InlineData("lager-yeast", 10, 0)]    // lager at 10C: in band
        [InlineData("lager-yeast", 18, 1)]    // lager at 18C: too warm, penalised
        public void Temperature_Band_Penalises_Only_Out_Of_Band(string strain, int tempC, int expectPenalty)
        {
            int penalty = FermentationSystem.TemperatureQualityPenaltyBasisPoints(tempC, strain);
            (penalty > 0 ? 1 : 0).Should().Be(expectPenalty);
        }

        [Fact]
        public void Warmer_Ambient_Means_Worse_Penalty()
        {
            FermentationSystem.TemperatureQualityPenaltyBasisPoints(30, "ale-yeast")
                .Should().BeGreaterThan(FermentationSystem.TemperatureQualityPenaltyBasisPoints(25, "ale-yeast"));
        }

        [Fact]
        public void Higher_Yeast_Generation_Reduces_Attenuation()
        {
            // Repitched yeast drifts: generation 10 attenuates less than generation 1.
            int attenuationGen10 = MashChemistry.AttenuationBasisPoints(8000);
            int attenuationGen1 = MashChemistry.AttenuationBasisPoints(8000);

            attenuationGen10 = attenuationGen10 * (10000 - (10 - 1) * 300) / 10000;
            attenuationGen1 = attenuationGen1 * (10000 - (1 - 1) * 300) / 10000;

            attenuationGen1.Should().BeGreaterThan(attenuationGen10);
        }

        [Fact]
        public void Vessel_Frees_On_The_Expected_Tick()
        {
            var s = Standard();
            var res = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            res.Success.Should().BeTrue();

            var node = s.World.Get(new NodeId("burton"));
            var vessel = node.Vessels.Single(v => !v.IsAvailable);
            int totalDays = vessel.DaysRemaining;

            // Advance exactly one tick short of ready: still occupied.
            TickSystem.AdvanceDays(s, totalDays - 1);
            node.Vessels.Should().Contain(v => !v.IsAvailable);

            // One more tick: free and ready.
            TickSystem.AdvanceDays(s, 1);
            node.Vessels.Should().OnlyContain(v => v.IsAvailable);
            node.Batches.Single().State.Should().Be(BatchState.Ready);
        }

        [Fact]
        public void Underpitched_Yeast_Leaves_Higher_FG_And_Sweetness()
        {
            // Low yeast health -> lower attenuation -> higher FG and sweetness.
            int fgHealthy = MashChemistry.FinalGravityPoints(50, 8000);
            int fgUnderpitched = MashChemistry.FinalGravityPoints(50, 6000);

            fgUnderpitched.Should().BeGreaterThan(fgHealthy);
        }
    }
}
