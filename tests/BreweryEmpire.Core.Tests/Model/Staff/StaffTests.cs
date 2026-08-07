using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Staff
{
    public class StaffMemberTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);

        private static StaffTrait Trait(StaffRole role, TraitEffect effect, int magnitude) =>
            new StaffTrait
            {
                Id = "t-" + effect, DisplayName = "T", AppliesTo = role,
                Effect = effect, MagnitudeBasisPoints = magnitude
            };

        [Fact]
        public void Skill_Clamps_To_Valid_Range()
        {
            var s = new StaffMember(new StaffId("a"), "A", StaffRole.Brewmaster,
                                    99999, Money.FromWhole(10), 30, D(0));
            s.SkillBasisPoints.Should().Be(10000);

            s.SkillBasisPoints = -500;
            s.SkillBasisPoints.Should().Be(0);
        }

        [Fact]
        public void BonusFor_Sums_Matching_Traits_Only()
        {
            var s = new StaffMember(new StaffId("a"), "A", StaffRole.Brewmaster,
                                    5000, Money.FromWhole(10), 30, D(0),
                                    new List<StaffTrait>
                                    {
                                        Trait(StaffRole.Brewmaster, TraitEffect.QualityBonus, 500),
                                        Trait(StaffRole.Brewmaster, TraitEffect.QualityBonus, 300),
                                        Trait(StaffRole.Brewmaster, TraitEffect.TransitSpeed, 900)
                                    });

            s.BonusFor(TraitEffect.QualityBonus).Should().Be(800);
            s.BonusFor(TraitEffect.TransitSpeed).Should().Be(900);
            s.BonusFor(TraitEffect.ResearchRate).Should().Be(0);
        }

        [Fact]
        public void Traits_For_A_Different_Role_Do_Not_Apply()
        {
            // A drayman's trait held by a brewmaster must not count.
            var s = new StaffMember(new StaffId("a"), "A", StaffRole.Brewmaster,
                                    5000, Money.FromWhole(10), 30, D(0),
                                    new List<StaffTrait>
                                    {
                                        Trait(StaffRole.Drayman, TraitEffect.TransitSpeed, 1000)
                                    });

            s.BonusFor(TraitEffect.TransitSpeed).Should().Be(0);
        }

        [Fact]
        public void Loyalty_Clamps_At_Both_Ends()
        {
            var s = new StaffMember(new StaffId("a"), "A", StaffRole.Cooper,
                                    5000, Money.FromWhole(10), 30, D(0));

            s.AdjustLoyalty(99999);
            s.LoyaltyBasisPoints.Should().Be(10000);

            s.AdjustLoyalty(-99999);
            s.LoyaltyBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Member_Resigns_When_Loyalty_Hits_Zero()
        {
            var s = new StaffMember(new StaffId("a"), "A", StaffRole.Cooper,
                                    5000, Money.FromWhole(10), 30, D(0));
            s.HasResigned.Should().BeFalse();

            s.AdjustLoyalty(-10000);
            s.HasResigned.Should().BeTrue();
        }

        [Fact]
        public void Assignment_Round_Trips_And_Can_Be_Cleared()
        {
            var s = new StaffMember(new StaffId("a"), "A", StaffRole.Cooper,
                                    5000, Money.FromWhole(10), 30, D(0));
            s.AssignedNodeId.Should().BeNull();

            s.AssignTo("brewery-1");
            s.AssignedNodeId.Should().Be("brewery-1");

            s.AssignTo(null);
            s.AssignedNodeId.Should().BeNull();
        }
    }

    public class RosterTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);
        private static GameDate Year(int y) => GameDate.FromYearMonthDay(y, 6, 1);

        private static StaffMember Worker(string id, StaffRole role, int skill,
                                          long wageWhole = 20, string? node = null,
                                          IReadOnlyList<StaffTrait>? traits = null)
        {
            var s = new StaffMember(new StaffId(id), id, role, skill,
                                    Money.FromWhole(wageWhole), 30, D(0), traits);
            s.AssignTo(node);
            return s;
        }

        private static StaffTrait Trait(StaffRole role, TraitEffect effect, int magnitude) =>
            new StaffTrait
            {
                Id = "t", DisplayName = "T", AppliesTo = role,
                Effect = effect, MagnitudeBasisPoints = magnitude
            };

        [Fact]
        public void Unassigned_Staff_Still_Accrue_Wages()
        {
            var r = new Roster();
            r.Hire(Worker("a", StaffRole.Cooper, 5000, wageWhole: 30, node: "brewery-1"), D(0));
            r.Hire(Worker("b", StaffRole.Cooper, 5000, wageWhole: 25, node: null), D(0));

            r.TotalMonthlyWages.Should().Be(Money.FromWhole(55));
        }

        [Fact]
        public void Unassigned_Staff_Contribute_No_Bonus()
        {
            var r = new Roster();
            r.Hire(Worker("idle", StaffRole.Brewmaster, 9000, node: null,
                          traits: new[] { Trait(StaffRole.Brewmaster, TraitEffect.QualityBonus, 2000) }), D(0));

            r.AggregateBonus("brewery-1", TraitEffect.QualityBonus).Should().Be(0);
        }

        [Fact]
        public void Staff_At_Another_Node_Contribute_No_Bonus()
        {
            var r = new Roster();
            r.Hire(Worker("elsewhere", StaffRole.Brewmaster, 9000, node: "brewery-2",
                          traits: new[] { Trait(StaffRole.Brewmaster, TraitEffect.QualityBonus, 2000) }), D(0));

            r.AggregateBonus("brewery-1", TraitEffect.QualityBonus).Should().Be(0);
            r.AggregateBonus("brewery-2", TraitEffect.QualityBonus).Should().Be(2000);
        }

        [Fact]
        public void AggregateBonus_Sums_Across_Staff_At_The_Node()
        {
            var r = new Roster();
            r.Hire(Worker("a", StaffRole.Brewmaster, 8000, node: "brewery-1",
                          traits: new[] { Trait(StaffRole.Brewmaster, TraitEffect.QualityBonus, 500) }), D(0));
            r.Hire(Worker("b", StaffRole.Brewmaster, 7000, node: "brewery-1",
                          traits: new[] { Trait(StaffRole.Brewmaster, TraitEffect.QualityBonus, 700) }), D(0));

            r.AggregateBonus("brewery-1", TraitEffect.QualityBonus).Should().Be(1200);
        }

        [Fact]
        public void BestFor_Is_Deterministic_Under_Equal_Skill()
        {
            for (int run = 0; run < 5; run++)
            {
                var r = new Roster();
                r.Hire(Worker("zebra", StaffRole.Brewmaster, 8000, node: "n1"), D(0));
                r.Hire(Worker("alpha", StaffRole.Brewmaster, 8000, node: "n1"), D(0));
                r.Hire(Worker("mango", StaffRole.Brewmaster, 8000, node: "n1"), D(0));

                r.BestFor("n1", StaffRole.Brewmaster)!.Id.Value.Should().Be("alpha");
            }
        }

        [Fact]
        public void BestFor_Prefers_Higher_Skill()
        {
            var r = new Roster();
            r.Hire(Worker("aaa", StaffRole.Brewmaster, 5000, node: "n1"), D(0));
            r.Hire(Worker("zzz", StaffRole.Brewmaster, 9000, node: "n1"), D(0));

            r.BestFor("n1", StaffRole.Brewmaster)!.Id.Value.Should().Be("zzz");
        }

        [Fact]
        public void BestFor_Returns_Null_When_No_Match()
        {
            var r = new Roster();
            r.Hire(Worker("a", StaffRole.Cooper, 9000, node: "n1"), D(0));

            r.BestFor("n1", StaffRole.Brewmaster).Should().BeNull();
            r.BestFor("n2", StaffRole.Cooper).Should().BeNull();
        }

        [Fact]
        public void Resignations_Are_Removed_And_Returned()
        {
            var r = new Roster();
            var quitter = Worker("quitter", StaffRole.Drayman, 5000, node: "n1");
            r.Hire(quitter, D(0));
            r.Hire(Worker("loyal", StaffRole.Drayman, 5000, node: "n1"), D(0));

            // Missed payroll grinds loyalty down.
            for (int month = 0; month < 12; month++) quitter.AdjustLoyalty(-1000);

            var left = r.ProcessResignations();

            left.Should().ContainSingle().Which.Id.Value.Should().Be("quitter");
            r.All.Should().ContainSingle().Which.Id.Value.Should().Be("loyal");
        }

        [Fact]
        public void Historical_Figure_Cannot_Be_Hired_Before_Their_Era()
        {
            var r = new Roster();
            var hansen = HistoricalFigures.Get("hansen").ToStaffMember(Year(1850));

            Action act = () => r.Hire(hansen, Year(1850));
            act.Should().Throw<InvalidOperationException>().WithMessage("*not available until 1883*");
        }

        [Fact]
        public void Historical_Figure_Can_Be_Hired_After_Their_Era_Begins()
        {
            var r = new Roster();
            var hansen = HistoricalFigures.Get("hansen").ToStaffMember(Year(1890));

            Action act = () => r.Hire(hansen, Year(1890));
            act.Should().NotThrow();
            r.Count.Should().Be(1);
        }

        [Fact]
        public void Historical_Figure_Cannot_Be_Hired_Twice()
        {
            var r = new Roster();
            r.Hire(HistoricalFigures.Get("groll").ToStaffMember(Year(1850)), Year(1850));

            Action act = () => r.Hire(HistoricalFigures.Get("groll").ToStaffMember(Year(1850)), Year(1850));
            act.Should().Throw<InvalidOperationException>().WithMessage("*already employed*");
        }

        [Fact]
        public void Ordinary_Staff_Are_Not_Exclusive()
        {
            var r = new Roster();
            r.Hire(Worker("cooper-1", StaffRole.Cooper, 4000), D(0));

            Action act = () => r.Hire(Worker("cooper-1", StaffRole.Cooper, 4000), D(0));
            act.Should().NotThrow("only historical figures are unique");
        }

        [Fact]
        public void Fire_Removes_A_Member()
        {
            var r = new Roster();
            r.Hire(Worker("a", StaffRole.Cooper, 5000), D(0));

            r.Fire(new StaffId("a")).Should().BeTrue();
            r.Count.Should().Be(0);
            r.Fire(new StaffId("ghost")).Should().BeFalse();
        }
    }

    public class HistoricalFiguresTests
    {
        private static GameDate Year(int y) => GameDate.FromYearMonthDay(y, 6, 1);

        [Fact]
        public void All_Eight_Figures_Exist_With_Expected_Roles()
        {
            var expected = new Dictionary<string, StaffRole>
            {
                { "hansen", StaffRole.Chemist },
                { "pasteur", StaffRole.Chemist },
                { "linde", StaffRole.Chemist },
                { "sedlmayr", StaffRole.Brewmaster },
                { "dreher", StaffRole.Brewmaster },
                { "groll", StaffRole.Brewmaster },
                { "guinness", StaffRole.Foreman },
                { "busch", StaffRole.Salesman }
            };

            HistoricalFigures.All.Should().HaveCount(8);
            foreach (var kv in expected)
                HistoricalFigures.Get(kv.Key).Role.Should().Be(kv.Value);
        }

        [Fact]
        public void Availability_Grows_Over_Time()
        {
            HistoricalFigures.AvailableIn(Year(1900)).Count()
                .Should().BeGreaterThan(HistoricalFigures.AvailableIn(Year(1750)).Count());
        }

        [Fact]
        public void Hansen_Appears_Only_After_1883()
        {
            HistoricalFigures.AvailableIn(Year(1850)).Should().NotContain(f => f.Id == "hansen");
            HistoricalFigures.AvailableIn(Year(1890)).Should().Contain(f => f.Id == "hansen");
        }

        [Fact]
        public void Guinness_Is_Available_In_The_Earliest_Era()
        {
            HistoricalFigures.AvailableIn(Year(1760)).Should().Contain(f => f.Id == "guinness");
        }

        [Fact]
        public void Figures_Are_Highly_Skilled_And_Expensive()
        {
            foreach (var f in HistoricalFigures.All)
            {
                f.SkillBasisPoints.Should().BeInRange(8000, 9800);
                f.MonthlyWage.Should().BeGreaterThan(Money.FromWhole(100));
                f.Traits.Should().NotBeEmpty();
            }
        }

        [Fact]
        public void Hansen_Gives_Strong_Infection_Resistance()
        {
            var hansen = HistoricalFigures.Get("hansen").ToStaffMember(Year(1890));
            hansen.BonusFor(TraitEffect.InfectionResistance).Should().BeGreaterOrEqualTo(3000);
        }

        [Fact]
        public void Converted_Figures_Are_Marked_Historical()
        {
            var busch = HistoricalFigures.Get("busch").ToStaffMember(Year(1880));
            busch.IsHistoricalFigure.Should().BeTrue();
            busch.Name.Should().Be("Adolphus Busch");
        }
    }
}
