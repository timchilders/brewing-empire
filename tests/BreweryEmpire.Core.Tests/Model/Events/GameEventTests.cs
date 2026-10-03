using System.Linq;
using BreweryEmpire.Core.Model.Events;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Events
{
    public class GameEventTests
    {
        [Fact]
        public void Event_Holds_Choices_In_Order()
        {
            var e = new GameEvent
            {
                Id = "e1",
                Choices = new[]
                {
                    new EventChoice { Id = "a", Label = "A" },
                    new EventChoice { Id = "b", Label = "B" }
                }
            };
            e.Choices.Select(c => c.Id).Should().Equal("a", "b");
        }

        [Fact]
        public void Effect_Kind_Ordinals_Are_Stable()
        {
            ((int)EventEffectKind.MoneyCents).Should().Be(0);
            ((int)EventEffectKind.Prestige).Should().Be(1);
            ((int)EventEffectKind.UnlockTech).Should().Be(5);
        }
    }
}
