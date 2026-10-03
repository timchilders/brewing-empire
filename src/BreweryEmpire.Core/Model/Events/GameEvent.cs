using System;
using System.Collections.Generic;

namespace BreweryEmpire.Core.Model.Events
{
    /// <summary>A historical or random event offered to the player. Pure data; the
    /// trigger logic lives in EventSystem, so this serializes cleanly.</summary>
    public sealed record GameEvent
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int MinYear { get; init; }
        public IReadOnlyList<EventChoice> Choices { get; init; } = Array.Empty<EventChoice>();
    }

    /// <summary>One selectable response with its effects.</summary>
    public sealed record EventChoice
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public IReadOnlyList<EventEffect> Effects { get; init; } = Array.Empty<EventEffect>();
    }

    /// <summary>A typed, integer state modifier.</summary>
    public sealed record EventEffect
    {
        public EventEffectKind Kind { get; init; }
        public int Amount { get; init; }                      // cents for money, bp/tonnes otherwise
        public string Target { get; init; } = string.Empty;   // tech id for UnlockTech
        public string Description { get; init; } = string.Empty; // ledger text for money effects
    }

    public enum EventEffectKind
    {
        MoneyCents = 0,          // signed: positive credit, negative debit (EventCost)
        Prestige = 1,            // added to PrestigeBasisPoints, clamped >= 0
        Reputation = 2,          // added to global ReputationBasisPoints, clamped [0,10000]
        ExciseDutyMultiplier = 3,// sets PriceBook.ExciseDutyModifierBasisPoints
        IceStock = 4,            // tonnes added to every ice-house node
        UnlockTech = 5           // immediately unlock Target
    }
}
