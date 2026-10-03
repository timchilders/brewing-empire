using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class ResearchSystemTests
    {
        private static GameState WithChemist(int skill, int researchTrait = 0)
        {
            var s = TestScenario.Standard();
            var traits = researchTrait > 0
                ? new[]
                {
                    new StaffTrait { Id = "rt", DisplayName = "R", AppliesTo = StaffRole.Chemist,
                                      Effect = TraitEffect.ResearchRate, MagnitudeBasisPoints = researchTrait }
                }
                : Array.Empty<StaffTrait>();
            var chemist = new StaffMember(new StaffId("chem-1"), "Chemist", StaffRole.Chemist,
                                          skill, Money.FromWhole(30), 40,
                                          GameDate.FromYearMonthDay(1750, 1, 1), traits);
            s.Staff.Hire(chemist, GameDate.FromYearMonthDay(1750, 1, 1));
            return s;
        }

        [Fact]
        public void No_Staff_Accrues_One_Point_Per_Day()
        {
            ResearchSystem.DailyResearchPoints(TestScenario.Standard()).Should().Be(1);
        }

        [Fact]
        public void Chemist_Accelerates_Research()
        {
            ResearchSystem.DailyResearchPoints(WithChemist(skill: 5000)).Should().BeGreaterThan(1);
        }

        [Fact]
        public void Start_Requires_Era_And_Prerequisites()
        {
            var s = TestScenario.Standard();   // date = 1750
            ResearchSystem.Start(s, "saccharometer").Should().BeFalse(); // era not reached
            ResearchSystem.Start(s, "pure-yeast").Should().BeFalse();    // prereq saccharometer
            ResearchSystem.Start(s, "malting-kilns").Should().BeTrue();  // era-1, no prereq
            ResearchSystem.Start(s, "ice-house").Should().BeFalse();     // already researching
        }

        [Fact]
        public void ProcessDay_Completes_And_Unlocks()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Start(s, "malting-kilns");          // cost 120
            for (int i = 0; i < 120; i++) ResearchSystem.ProcessDay(s);
            ResearchSystem.HasTech(s, "malting-kilns").Should().BeTrue();
            s.Research.ActiveTechId.Should().BeNull();
        }

        [Fact]
        public void Ice_House_Unlock_Enables_Ice_Harvest()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));
            node.HasIceHouse.Should().BeFalse();

            ResearchSystem.Unlock(s, "ice-house");

            node.HasIceHouse.Should().BeTrue();
        }

        [Fact]
        public void Refrigeration_Unlock_Sets_IsRefrigerated()
        {
            var s = TestScenario.Standard();
            var node = s.World.Get(new NodeId("burton"));
            ResearchSystem.Unlock(s, "refrigeration");
            node.IsRefrigerated.Should().BeTrue();
        }

        [Fact]
        public void Packaging_Unlocked_Only_By_Bottling_Line()
        {
            var s = TestScenario.Standard();
            ResearchSystem.IsPackagingUnlocked(s, PackagingType.WoodenCask).Should().BeTrue();
            ResearchSystem.IsPackagingUnlocked(s, PackagingType.Bottle).Should().BeFalse();
            ResearchSystem.Unlock(s, "bottling-line");
            ResearchSystem.IsPackagingUnlocked(s, PackagingType.Bottle).Should().BeTrue();
        }

        [Fact]
        public void Style_Unlocked_Matches_Required_Tech()
        {
            var s = TestScenario.Standard();
            ResearchSystem.IsStyleUnlocked(s, BeerStyle.PaleAle).Should().BeTrue();
            ResearchSystem.IsStyleUnlocked(s, BeerStyle.Porter).Should().BeFalse();
            ResearchSystem.Unlock(s, "malting-kilns");
            ResearchSystem.IsStyleUnlocked(s, BeerStyle.Porter).Should().BeTrue();
        }

        [Fact]
        public void Tick_Advances_Research_Daily()
        {
            var s = TestScenario.Standard();
            ResearchSystem.Start(s, "malting-kilns");
            TickSystem.AdvanceDays(s, 120);
            ResearchSystem.HasTech(s, "malting-kilns").Should().BeTrue();
        }
    }
}
