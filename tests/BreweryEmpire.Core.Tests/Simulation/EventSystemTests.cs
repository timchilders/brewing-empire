using System.Linq;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class EventSystemTests
    {
        [Fact]
        public void Due_Event_Is_Enqueued_Once()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);

            EventSystem.ProcessDay(s);
            EventSystem.ProcessDay(s);

            s.PendingEvents.Should().ContainSingle(e => e.Id == "temperance");
        }

        [Fact]
        public void Ice_Shortage_Requires_An_Ice_House()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1850, 1, 1);
            EventSystem.ProcessDay(s);
            s.PendingEvents.Should().NotContain(e => e.Id == "ice-shortage");

            ResearchSystem.Unlock(s, "ice-house");
            EventSystem.ProcessDay(s);
            s.PendingEvents.Should().Contain(e => e.Id == "ice-shortage");
        }

        [Fact]
        public void Resolve_Applies_Money_And_Records()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            EventSystem.ProcessDay(s);
            var e = s.PendingEvents.Single(x => x.Id == "temperance");
            int lobbyIndex = e.Choices.ToList().FindIndex(c => c.Id == "lobby");
            long balanceBefore = s.Ledger.Balance.Cents;

            EventSystem.Resolve(s, "temperance", lobbyIndex).Should().BeTrue();

            s.Ledger.Balance.Cents.Should().BeLessThan(balanceBefore);
            s.PendingEvents.Should().NotContain(x => x.Id == "temperance");
            s.ResolvedEventIds.Should().Contain("temperance");
        }

        [Fact]
        public void Resolve_Invalid_Choice_Returns_False()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            EventSystem.ProcessDay(s);
            EventSystem.Resolve(s, "temperance", 99).Should().BeFalse();
            EventSystem.Resolve(s, "no-such-event", 0).Should().BeFalse();
        }

        [Fact]
        public void Tick_Enqueues_Due_Events()
        {
            var s = TestScenario.Standard();
            s.Date = GameDate.FromYearMonthDay(1890, 6, 1);
            TickSystem.AdvanceDay(s);
            s.PendingEvents.Should().Contain(e => e.Id == "temperance");
        }
    }
}
