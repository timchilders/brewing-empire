using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class ProgressionAndRivalTests
    {
        // ---- F1: staff progression ----

        [Fact]
        public void Staff_Gain_Skill_With_Experience()
        {
            var s = TestScenario.Standard();
            var brewmaster = s.Staff.All.Single();
            int before = brewmaster.SkillBasisPoints;

            StaffProgression.AwardExperience(brewmaster, 100);
            StaffProgression.AwardExperience(brewmaster, 100);

            brewmaster.SkillBasisPoints.Should().BeGreaterThan(before);
        }

        [Fact]
        public void Skill_Clamps_At_Maximum()
        {
            var s = TestScenario.Standard();
            var brewmaster = s.Staff.All.Single();
            brewmaster.SkillBasisPoints = 9950;

            StaffProgression.AwardExperience(brewmaster, 1000);

            brewmaster.SkillBasisPoints.Should().Be(10000);
        }

        [Fact]
        public void Unassigned_Staff_Do_Not_Gain_Brewing_Skill()
        {
            var s = TestScenario.Standard();
            var brewmaster = s.Staff.All.Single();
            brewmaster.AssignTo(null);   // unassigned

            int before = brewmaster.SkillBasisPoints;

            // Award experience only if assigned — simulating a brew day at the node.
            StaffProgression.OnSuccessfulBrew(s, new NodeId("burton"), StaffRole.Brewmaster, 200);

            brewmaster.SkillBasisPoints.Should().Be(before);
        }

        [Fact]
        public void Assigned_Staff_Gain_On_Successful_Brew()
        {
            var s = TestScenario.Standard();
            var brewmaster = s.Staff.All.Single();
            int before = brewmaster.SkillBasisPoints;

            StaffProgression.OnSuccessfulBrew(s, new NodeId("burton"), StaffRole.Brewmaster, 200);

            brewmaster.SkillBasisPoints.Should().BeGreaterThan(before);
        }

        // ---- F2: rivals ----

        [Fact]
        public void A_Rival_Reduces_Market_Demand_For_The_Player()
        {
            var s = TestScenario.Standard();
            var market = s.Markets[0];
            int demandAlone = market.EffectiveDemandLitres(
                BreweryEmpire.Core.Model.Brewing.BeerStyle.PaleAle,
                GameDate.FromYearMonthDay(1750, 7, 15));

            var rival = new RivalBrewer
            {
                Id = "r1", Name = "Rival & Co",
                StrengthBasisPoints = 6000,
                HomeRegionId = "burton"
            };
            s.Rivals.Add(rival);

            int demandWithRival = RivalSystem.EffectivePlayerDemand(
                market, BreweryEmpire.Core.Model.Brewing.BeerStyle.PaleAle,
                GameDate.FromYearMonthDay(1750, 7, 15), s.Rivals);

            demandWithRival.Should().BeLessThan(demandAlone);
        }

        [Fact]
        public void Rival_Behaviour_Is_Seed_Stable()
        {
            string Run(int seed)
            {
                var s = TestScenario.Standard(seed: seed);
                var rival = new RivalBrewer { Id = "r1", Name = "Rival", StrengthBasisPoints = 6000, HomeRegionId = "burton" };
                s.Rivals.Add(rival);

                RivalSystem.ProcessRivals(s);
                return s.Rivals[0].Name + ":" + s.Rivals[0].StrengthBasisPoints;
            }

            Run(42).Should().Be(Run(42));
        }
    }
}
