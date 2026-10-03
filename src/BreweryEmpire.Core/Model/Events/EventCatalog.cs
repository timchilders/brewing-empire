using System.Collections.Generic;

namespace BreweryEmpire.Core.Model.Events
{
    /// <summary>
    /// The four Victoria-style events from the design doc (section 3.5). Dollar
    /// figures are placeholders pending a balance pass; the plumbing is the product.
    /// </summary>
    public static class EventCatalog
    {
        private static EventEffect Money(int cents, string desc) => new EventEffect
            { Kind = EventEffectKind.MoneyCents, Amount = cents, Description = desc };

        private static EventEffect Prestige(int bp) => new EventEffect
            { Kind = EventEffectKind.Prestige, Amount = bp, Description = "Prestige" };

        private static EventEffect Reputation(int bp) => new EventEffect
            { Kind = EventEffectKind.Reputation, Amount = bp, Description = "Reputation" };

        private static EventEffect Duty(int bp) => new EventEffect
            { Kind = EventEffectKind.ExciseDutyMultiplier, Amount = bp, Description = "Excise duty" };

        private static EventEffect Ice(int tonnes) => new EventEffect
            { Kind = EventEffectKind.IceStock, Amount = tonnes, Description = "Ice stock" };

        public static IReadOnlyList<GameEvent> All { get; } = new List<GameEvent>
        {
            new GameEvent
            {
                Id = "pure-yeast-dilemma", MinYear = 1883,
                Title = "The Open-Source Pure Yeast Dilemma",
                Description = "Hansen's pure yeast can be shared with the world, or patented.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "share", Label = "Share it freely",
                        Effects = new List<EventEffect> { Prestige(5000), Reputation(2000) }
                    },
                    new EventChoice
                    {
                        Id = "patent", Label = "Patent it",
                        Effects = new List<EventEffect> { Money(50_000, "Pure yeast patent royalties"), Prestige(-1500) }
                    }
                }
            },
            new GameEvent
            {
                Id = "ice-shortage", MinYear = 1830,
                Title = "The Great Ice Shortage",
                Description = "A warm winter has starved the ice houses.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "buy", Label = "Buy Scandinavian lake ice",
                        Effects = new List<EventEffect> { Money(-20_000, "Imported Scandinavian ice"), Ice(20) }
                    },
                    new EventChoice
                    {
                        Id = "halt", Label = "Halt lager for the summer",
                        Effects = new List<EventEffect> { Reputation(-1000) }
                    }
                }
            },
            new GameEvent
            {
                Id = "hop-blight", MinYear = 1760,
                Title = "The Hop Blight",
                Description = "A blight has spiked hop prices across the country.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "premium", Label = "Pay premium for true hops",
                        Effects = new List<EventEffect> { Money(-30_000, "Premium hop purchase") }
                    },
                    new EventChoice
                    {
                        Id = "substitute", Label = "Substitute herbal blends",
                        Effects = new List<EventEffect> { Money(-5_000, "Herbal substitute"), Prestige(-2000), Reputation(-1500) }
                    }
                }
            },
            new GameEvent
            {
                Id = "temperance", MinYear = 1880,
                Title = "Temperance Movement Rises",
                Description = "Local prohibition threatens your markets.",
                Choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        Id = "lobby", Label = "Lobby the authorities",
                        Effects = new List<EventEffect> { Money(-40_000, "Lobbying expenditure"), Reputation(500) }
                    },
                    new EventChoice
                    {
                        Id = "session", Label = "Brew low-alcohol session beers",
                        Effects = new List<EventEffect> { Prestige(-1500), Reputation(1000) }
                    }
                }
            }
        };
    }
}
