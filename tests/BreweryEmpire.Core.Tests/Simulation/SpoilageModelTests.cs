using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class SpoilageModelTests
    {
        [Fact]
        public void High_IBU_Resists_Lactobacillus_Better_Than_Low_IBU()
        {
            int Defense(int ibu) =>
                SpoilageSystem.DefenseBasisPoints(SpoilageOrganism.Lactobacillus, ibu, abvBasisPoints: 400);

            Defense(800).Should().BeGreaterThan(Defense(100));
        }

        [Fact]
        public void ABV_Resists_All_Organisms()
        {
            int Defense(int abv) =>
                SpoilageSystem.DefenseBasisPoints(SpoilageOrganism.WildYeast, ibuTenths: 100, abvBasisPoints: abv);

            Defense(800).Should().BeGreaterThan(Defense(100));
        }

        [Fact]
        public void Refrigeration_Slows_Progression()
        {
            int Rate(bool cold) => SpoilageSystem.ProgressionRateBasisPoints(
                SpoilageOrganism.Lactobacillus, ambientC: 18, ibuTenths: 100,
                abvBasisPoints: 400, cold: cold, pasteurized: false);

            Rate(cold: true).Should().BeLessThan(Rate(cold: false));
        }

        [Fact]
        public void Pasteurization_Near_Eliminates_Progression()
        {
            int Rate(bool pasteurized) => SpoilageSystem.ProgressionRateBasisPoints(
                SpoilageOrganism.Lactobacillus, ambientC: 18, ibuTenths: 100,
                abvBasisPoints: 400, cold: false, pasteurized: pasteurized);

            Rate(pasteurized: true).Should().BeLessThan(Rate(pasteurized: false) / 10);
        }

        [Fact]
        public void Warm_Ambient_Progresses_Faster_Than_Cold()
        {
            SpoilageSystem.ProgressionRateBasisPoints(
                SpoilageOrganism.Pediococcus, 28, 100, 400, false, false)
                .Should().BeGreaterThan(SpoilageSystem.ProgressionRateBasisPoints(
                    SpoilageOrganism.Pediococcus, 8, 100, 400, false, false));
        }

        [Fact]
        public void An_Infected_Batch_Spoils_And_Writes_Off_Exactly_Once()
        {
            var s = TestScenario.Standard(seed: 77);
            var node = s.World.Get(new NodeId("burton"));

            var res = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            res.Success.Should().BeTrue();
            var batch = res.Batch!;

            // Contract an infection directly, then let progression ruin it.
            batch.ContractInfection(SpoilageOrganism.Lactobacillus, "test infection");
            batch.State.Should().Be(BatchState.Fermenting);

            // Force progression to the spoilage threshold in one call.
            bool spoiled = batch.AdvanceInfection(10_000);
            spoiled.Should().BeTrue();
            batch.State.Should().Be(BatchState.Spoiled);

            // Write off exactly once.
            SpoilageSystem.WriteOff(s, node, batch);
            int writeOffs = s.Ledger.Entries.Count(e => e.Category == LedgerCategory.SpoilageWriteOff);
            writeOffs.Should().Be(1);
        }

        [Fact]
        public void A_Soured_Berliner_Weisse_Is_Not_Defective()
        {
            SpoilageSystem.IsIntentionalSour(BeerStyle.BerlinerWeisse, SpoilageOrganism.Lactobacillus)
                .Should().BeTrue();
            SpoilageSystem.IsIntentionalSour(BeerStyle.Lambic, SpoilageOrganism.Brettanomyces)
                .Should().BeTrue();
            SpoilageSystem.IsIntentionalSour(BeerStyle.PaleAle, SpoilageOrganism.Lactobacillus)
                .Should().BeFalse();
        }
    }
}
