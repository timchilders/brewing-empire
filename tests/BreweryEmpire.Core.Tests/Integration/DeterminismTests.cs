using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Integration
{
    /// <summary>
    /// The Phase 1 acceptance tests. If these pass, the core is a trustworthy
    /// foundation to build a Unity front end on.
    /// </summary>
    public class DeterminismTests
    {
        [Fact]
        public void Same_Seed_Produces_Identical_Worlds_After_A_Year()
        {
            GameState Run()
            {
                var s = TestScenario.Standard(seed: 20260806);
                for (int day = 0; day < 365; day++)
                {
                    if (day % 30 == 0)
                        BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                    TickSystem.AdvanceDay(s);
                }
                return s;
            }

            TestScenario.Fingerprint(Run()).Should().Be(TestScenario.Fingerprint(Run()));
        }

        [Fact]
        public void Different_Seeds_Diverge()
        {
            GameState Run(int seed)
            {
                var s = TestScenario.Standard(seed: seed);
                for (int day = 0; day < 200; day++)
                {
                    if (day % 30 == 0)
                        BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                    TickSystem.AdvanceDay(s);
                }
                return s;
            }

            TestScenario.Fingerprint(Run(1)).Should().NotBe(TestScenario.Fingerprint(Run(2)));
        }

        [Fact]
        public void Ten_Consecutive_Runs_Are_All_Identical()
        {
            string Run()
            {
                var s = TestScenario.Standard(seed: 4242);
                for (int day = 0; day < 120; day++)
                {
                    if (day % 21 == 0)
                        BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                    TickSystem.AdvanceDay(s);
                }
                return TestScenario.Fingerprint(s);
            }

            var expected = Run();
            for (int i = 0; i < 9; i++) Run().Should().Be(expected);
        }

        [Fact]
        public void A_Thousand_Ticks_Preserve_The_Ledger_Invariant()
        {
            var s = TestScenario.Standard(seed: 777, openingCapitalWhole: 100_000);

            for (int day = 0; day < 1000; day++)
            {
                if (day % 25 == 0)
                    BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                TickSystem.AdvanceDay(s);
            }

            s.Ledger.Balance.Should().Be(s.Ledger.SumOfAllEntries(),
                "every balance change must be explained by a ledger entry");
        }

        [Fact]
        public void A_Thousand_Ticks_Do_Not_Throw_Or_Corrupt_State()
        {
            var s = TestScenario.Standard(seed: 31337, openingCapitalWhole: 100_000);

            Action act = () =>
            {
                for (int day = 0; day < 1000; day++)
                {
                    if (day % 25 == 0)
                        BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                    TickSystem.AdvanceDay(s);
                }
            };

            act.Should().NotThrow();

            var node = s.World.Get(new NodeId("burton"));
            node.Batches.Should().NotContain(b => b.VolumeLitres < 0,
                "no batch should ever hold a negative volume");
            node.Vessels.Should().OnlyContain(v => v.DaysRemaining >= 0,
                "no vessel should ever show negative days remaining");
            s.Date.TotalDays.Should().Be(GameDate.FromYearMonthDay(1750, 1, 1).TotalDays + 1000);
        }

        [Fact]
        public void Time_Advances_Exactly_One_Day_Per_Tick()
        {
            var s = TestScenario.Standard();
            var start = s.Date;

            TickSystem.AdvanceDays(s, 100);

            s.Date.TotalDays.Should().Be(start.TotalDays + 100);
        }
    }

    public class SaveRoundTripTests
    {
        [Fact]
        public void Save_Then_Load_Preserves_The_World_Exactly()
        {
            var original = TestScenario.Standard(seed: 555);
            for (int day = 0; day < 60; day++)
            {
                if (day % 20 == 0)
                    BrewingSystem.TryStartBrew(original, new NodeId("burton"), new RecipeId("pale-ale"));
                TickSystem.AdvanceDay(original);
            }

            var json = SaveSystem.Save(original);
            var loaded = SaveSystem.Load(json);

            TestScenario.Fingerprint(loaded).Should().Be(TestScenario.Fingerprint(original));
        }

        [Fact]
        public void A_Loaded_Game_Continues_Identically()
        {
            var original = TestScenario.Standard(seed: 909);
            for (int day = 0; day < 40; day++)
            {
                if (day % 15 == 0)
                    BrewingSystem.TryStartBrew(original, new NodeId("burton"), new RecipeId("pale-ale"));
                TickSystem.AdvanceDay(original);
            }

            var loaded = SaveSystem.Load(SaveSystem.Save(original));

            // Advance both the same way; they must stay in lockstep.
            for (int day = 0; day < 60; day++)
            {
                if (day % 15 == 0)
                {
                    BrewingSystem.TryStartBrew(original, new NodeId("burton"), new RecipeId("pale-ale"));
                    BrewingSystem.TryStartBrew(loaded, new NodeId("burton"), new RecipeId("pale-ale"));
                }
                TickSystem.AdvanceDay(original);
                TickSystem.AdvanceDay(loaded);
            }

            TestScenario.Fingerprint(loaded).Should().Be(TestScenario.Fingerprint(original));
        }

        [Fact]
        public void Save_Is_Stable_Across_Repeated_Round_Trips()
        {
            var s = TestScenario.Standard(seed: 111);
            TickSystem.AdvanceDays(s, 30);

            var first = SaveSystem.Save(s);
            var second = SaveSystem.Save(SaveSystem.Load(first));
            var third = SaveSystem.Save(SaveSystem.Load(second));

            second.Should().Be(first);
            third.Should().Be(second);
        }

        [Fact]
        public void Money_Survives_Round_Trip_To_The_Cent()
        {
            var s = TestScenario.Standard(seed: 222);
            TickSystem.AdvanceDays(s, 90);

            var loaded = SaveSystem.Load(SaveSystem.Save(s));

            loaded.Ledger.Balance.Cents.Should().Be(s.Ledger.Balance.Cents);
            loaded.Ledger.Entries.Count.Should().Be(s.Ledger.Entries.Count);
        }

        [Fact]
        public void Save_Records_Its_Version()
        {
            var s = TestScenario.Standard();
            SaveSystem.Save(s).Should().Contain("\"SaveVersion\":4");
        }

        [Fact]
        public void A_Future_Save_Version_Is_Refused_Not_Silently_Mangled()
        {
            var s = TestScenario.Standard();
            var json = SaveSystem.Save(s).Replace("\"SaveVersion\":4", "\"SaveVersion\":99");

            Action act = () => SaveSystem.Load(json);
            act.Should().Throw<InvalidOperationException>().WithMessage("*newer than this build*");
        }

        [Fact]
        public void A_Version_1_Save_Loads_With_Phase_2_Defaults()
        {
            // A hand-written v1 save: the exact shape Phase 1 wrote — no
            // Markets, no Shipments, no batch-chemistry fields. The loader
            // must default the missing collections/fields rather than throw.
            var v1Json = @"
            {
              ""SaveVersion"": 1,
              ""Seed"": 12345,
              ""DateTotalDays"": 0,
              ""ReputationBasisPoints"": 5000,
              ""IsBankrupt"": false,
              ""NextEntityNumber"": 1,
              ""LedgerBalanceCents"": 200000,
              ""LedgerEntries"": [ { ""DateTotalDays"": 0, ""Category"": 3, ""AmountCents"": 200000, ""Description"": ""Opening capital"" } ],
              ""Nodes"": [],
              ""Staff"": [],
              ""Recipes"": [],
              ""Rivals"": []
            }";

            var loaded = SaveSystem.Load(v1Json);

            loaded.SaveVersion.Should().Be(1);
            loaded.Markets.Should().BeEmpty();
            loaded.Shipments.Should().BeEmpty();
            loaded.World.NodeCount.Should().Be(0);
            loaded.Ledger.Balance.Cents.Should().Be(200000);
        }

        [Fact]
        public void Empty_Save_Data_Is_Rejected()
        {
            Action act = () => SaveSystem.Load("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Staff_And_Inventory_Survive_Round_Trip()
        {
            var s = TestScenario.Standard(seed: 333);
            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));

            var loaded = SaveSystem.Load(SaveSystem.Save(s));
            var node = loaded.World.Get(new NodeId("burton"));

            loaded.Staff.All.Should().HaveCount(s.Staff.All.Count);
            loaded.Staff.All.Single().AssignedNodeId.Should().Be("burton");
            node.Inventory.TotalGramsOf("pale-malt")
                .Should().Be(s.World.Get(new NodeId("burton")).Inventory.TotalGramsOf("pale-malt"));
            node.Batches.Should().HaveCount(1);
        }

        [Fact]
        public void Recipes_Survive_Round_Trip()
        {
            var s = TestScenario.Standard();
            var loaded = SaveSystem.Load(SaveSystem.Save(s));

            loaded.Recipes.Should().ContainKey("pale-ale");
            var r = loaded.Recipes["pale-ale"];
            r.TotalGristGrams.Should().Be(220_000);
            r.Hops.Should().HaveCount(2);
            r.Mash.Steps.Should().NotBeEmpty();
        }
    }

    public class EconomicSanityTests
    {
        /// <summary>
        /// THE BALANCE CANARY. A brewery that does nothing must go broke:
        /// upkeep and wages continue whether or not you brew. If this ever
        /// stops failing, the economy has become free money.
        /// </summary>
        [Fact]
        public void Doing_Nothing_Leads_To_Ruin_Within_A_Few_Years()
        {
            var s = TestScenario.Standard(seed: 1, openingCapitalWhole: 2000);

            for (int day = 0; day < 365 * 3 && !s.IsBankrupt; day++)
                TickSystem.AdvanceDay(s);

            s.IsBankrupt.Should().BeTrue("idle breweries still pay wages and upkeep");
        }

        [Fact]
        public void Brewing_And_Selling_Beats_Doing_Nothing()
        {
            long BalanceAfter(bool brew)
            {
                var s = TestScenario.Standard(seed: 42, openingCapitalWhole: 5000);

                for (int day = 0; day < 365; day++)
                {
                    if (brew && day % 25 == 0)
                        BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
                    TickSystem.AdvanceDay(s);
                }

                return s.Ledger.Balance.Cents;
            }

            BalanceAfter(brew: true).Should().BeGreaterThan(BalanceAfter(brew: false),
                "running the brewery must beat sitting idle");
        }

        [Fact]
        public void Wages_Are_Charged_Monthly_Not_Daily()
        {
            var s = TestScenario.Standard(seed: 5, openingCapitalWhole: 10_000);
            TickSystem.AdvanceDays(s, 365);

            int wagePostings = s.Ledger.Entries.Count(e => e.Category == LedgerCategory.Wages);

            // Twelve payrolls a year (history compaction may fold older ones).
            wagePostings.Should().BeLessOrEqualTo(12);
            wagePostings.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Excise_Duty_Is_Charged_On_Sales()
        {
            var s = TestScenario.Standard(seed: 8, openingCapitalWhole: 10_000);

            BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            TickSystem.AdvanceDays(s, 40);

            s.Ledger.Entries.Should().Contain(e => e.Category == LedgerCategory.ExciseDuty);
        }

        [Fact]
        public void Unpaid_Staff_Eventually_Resign()
        {
            var s = TestScenario.Standard(seed: 9, openingCapitalWhole: 0);

            // No money, so payroll fails every month.
            for (int day = 0; day < 365 && s.Staff.Count > 0; day++)
                TickSystem.AdvanceDay(s);

            s.Staff.Count.Should().Be(0, "nobody works unpaid forever");
        }
    }
}
