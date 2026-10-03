using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Events;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Choice-based historical events. Triggers are deterministic (date + state
    /// conditions, no RNG); a due event is enqueued once and waits for the player
    /// to resolve a choice. Resolving applies typed, integer effects.
    /// </summary>
    public static class EventSystem
    {
        /// <summary>Enqueue any event whose trigger is now true and not yet resolved/pending.</summary>
        public static void ProcessDay(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            foreach (var e in EventCatalog.All)
            {
                if (state.ResolvedEventIds.Contains(e.Id)) continue;
                if (state.PendingEvents.Any(p => p.Id == e.Id)) continue;
                if (!ShouldTrigger(e.Id, state)) continue;
                state.PendingEvents.Add(e);
            }
        }

        private static bool ShouldTrigger(string id, GameState state) => id switch
        {
            "pure-yeast-dilemma" => state.Date.Year >= 1883 && ResearchSystem.HasTech(state, "pure-yeast"),
            "ice-shortage" => state.Date.Year >= 1830 && state.World.Nodes.Any(n => n.HasIceHouse),
            "hop-blight" => state.Date.Year >= 1760,
            "temperance" => state.Date.Year >= 1880,
            _ => false
        };

        /// <summary>Apply a chosen response. Returns false if the event is not pending or the
        /// index is out of range (no partial application).</summary>
        public static bool Resolve(GameState state, string eventId, int choiceIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var e = state.PendingEvents.FirstOrDefault(p => p.Id == eventId);
            if (e == null) return false;
            if (choiceIndex < 0 || choiceIndex >= e.Choices.Count) return false;

            foreach (var fx in e.Choices[choiceIndex].Effects)
                ApplyEffect(state, fx);

            state.PendingEvents.Remove(e);
            state.ResolvedEventIds.Add(e.Id);
            return true;
        }

        private static void ApplyEffect(GameState state, EventEffect fx)
        {
            switch (fx.Kind)
            {
                case EventEffectKind.MoneyCents:
                    if (fx.Amount >= 0)
                        state.Ledger.Credit(state.Date, LedgerCategory.EventCost,
                                            Money.FromCents(fx.Amount), fx.Description);
                    else
                        state.Ledger.ForceDebit(state.Date, LedgerCategory.EventCost,
                                                Money.FromCents(-fx.Amount), fx.Description);
                    break;
                case EventEffectKind.Prestige:
                    state.PrestigeBasisPoints = Math.Max(0, state.PrestigeBasisPoints + fx.Amount);
                    break;
                case EventEffectKind.Reputation:
                    state.ReputationBasisPoints = Math.Clamp(state.ReputationBasisPoints + fx.Amount, 0, 10000);
                    break;
                case EventEffectKind.ExciseDutyMultiplier:
                    state.Prices.ExciseDutyModifierBasisPoints = fx.Amount;
                    break;
                case EventEffectKind.IceStock:
                    foreach (var n in state.World.Nodes)
                        if (n.HasIceHouse) n.RestoreIceStock(n.IceStockTonnes + fx.Amount);
                    break;
                case EventEffectKind.UnlockTech:
                    ResearchSystem.Unlock(state, fx.Target);
                    break;
            }
        }
    }
}
