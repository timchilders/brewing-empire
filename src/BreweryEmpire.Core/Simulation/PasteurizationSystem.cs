using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Heat-treat a finished batch. The spoilage payoff (pasteurized → token
    /// progression rate) is already wired in SpoilageSystem; this only flips the
    /// flag and charges the real cost.
    /// </summary>
    public static class PasteurizationSystem
    {
        public static bool TryPasteurize(GameState state, NodeId nodeId, BatchId batchId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!ResearchSystem.HasTech(state, "pasteurization")) return false;

            var node = state.World.Get(nodeId);
            var batch = node.Batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null || batch.State != BatchState.Ready) return false;

            var cost = Money.FromCents((long)batch.VolumeLitres * 2);
            state.Ledger.ForceDebit(state.Date, LedgerCategory.Upkeep, cost,
                                    "Pasteurization of " + batchId.Value, nodeId.Value);

            batch.IsPasteurized = true;
            batch.AdjustQuality(-300);   // gentle heat costs a little flavour
            return true;
        }
    }
}
