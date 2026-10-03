using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Integration
{
    /// <summary>
    /// Phase 3 end-to-end proof: the research DAG changes outcomes, and the whole
    /// thing stays deterministic and save/load-safe.
    /// </summary>
    public class Phase3IntegrationTests
    {
        [Fact]
        public void Researching_Through_The_Dag_Unlocks_Dark_Styles_Then_Lagers()
        {
            var s = TestScenario.Standard();
            var porter = TestScenario.PaleAle(); porter.Style = BeerStyle.Porter;
            s.Recipes["porter"] = porter;
            var pils = TestScenario.PaleAle(); pils.Style = BeerStyle.Pilsner;
            s.Recipes["pilsner"] = pils;

            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"))
                .Success.Should().BeFalse("porter is locked before Malting Kilns");

            ResearchSystem.Unlock(s, "malting-kilns");
            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("porter"))
                .Success.Should().BeTrue();

            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pilsner"))
                .Success.Should().BeFalse("pilsner needs the Pale Revolution");

            ResearchSystem.Unlock(s, "saccharometer");
            ResearchSystem.Unlock(s, "pale-revolution");
            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pilsner"))
                .Success.Should().BeTrue();
        }

        [Fact]
        public void Research_Is_Deterministic_Across_Runs()
        {
            static string Run()
            {
                var s = TestScenario.Standard(seed: 777);
                var chem = new StaffMember(new StaffId("chem"), "C", StaffRole.Chemist, 9000,
                                           Money.FromWhole(30), 40, GameDate.FromYearMonthDay(1750, 1, 1));
                s.Staff.Hire(chem, GameDate.FromYearMonthDay(1750, 1, 1));

                ResearchSystem.Start(s, "malting-kilns");
                ResearchSystem.Start(s, "saccharometer");   // fails while the first is active
                TickSystem.AdvanceDays(s, 30);

                return string.Join(",", s.Research.UnlockedTechIds)
                       + "|" + (s.Research.ActiveTechId ?? "none")
                       + "|" + s.Research.ActiveProgressPoints;
            }

            Run().Should().Be(Run());
        }

        [Fact]
        public void Determinism_Holds_With_Research_And_Events()
        {
            static string Run()
            {
                var s = TestScenario.Standard(seed: 555);
                ResearchSystem.Start(s, "malting-kilns");
                s.Date = GameDate.FromYearMonthDay(1880, 1, 1);
                for (int d = 0; d < 60; d++)
                {
                    if (d == 20) EventSystem.Resolve(s, "ice-shortage", 0);  // no-op: not pending
                    TickSystem.AdvanceDay(s);
                }
                return TestScenario.Fingerprint(s)
                       + "\nresearch=" + string.Join(",", s.Research.UnlockedTechIds)
                       + "\nprestige=" + s.PrestigeBasisPoints;
            }
            Run().Should().Be(Run());
        }

        [Fact]
        public void Save_Load_Resume_Is_Identical_With_Tech()
        {
            var s = TestScenario.Standard(seed: 55);
            ResearchSystem.Start(s, "malting-kilns");
            TickSystem.AdvanceDays(s, 40);

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));
            TickSystem.AdvanceDays(reloaded, 40);
            TickSystem.AdvanceDays(s, 40);

            TestScenario.Fingerprint(reloaded).Should().Be(TestScenario.Fingerprint(s));
        }

        [Fact]
        public void Ledger_Invariant_Holds_With_Event_Money_Effects()
        {
            var s = TestScenario.Standard(seed: 123);
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            for (int d = 0; d < 10; d++)
            {
                TickSystem.AdvanceDay(s);
                var t = s.PendingEvents.FirstOrDefault(e => e.Id == "temperance");
                if (t != null) EventSystem.Resolve(s, "temperance", 0);
            }
            s.Ledger.Balance.Should().Be(s.Ledger.SumOfAllEntries());
        }
    }
}
