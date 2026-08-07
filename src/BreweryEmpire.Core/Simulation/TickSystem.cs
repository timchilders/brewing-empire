using System;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// The heartbeat. One call advances the world exactly one day.
    ///
    /// ORDER IS PART OF THE CONTRACT. Systems run in a fixed sequence and
    /// nodes are always visited in sorted id order — any set/dictionary
    /// iteration here would make replays diverge between runs. The order below
    /// is chosen so a batch cannot be brewed, spoiled and sold on the same day
    /// in a way that depends on which node happened to go first.
    /// </summary>
    public static class TickSystem
    {
        public static void AdvanceDay(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsBankrupt) return;

            // 1. Calendar first: every later system reads the new date.
            state.Date = state.Date.AddDays(1);

            // 2. Vessels tick, freeing any whose occupancy expired.
            foreach (var node in state.World.Nodes)
                node.AdvanceVesselsOneDay();

            // 3. Infection rolls against today's temperature.
            foreach (var node in state.World.Nodes)
                SpoilageSystem.ProcessNode(state, node);

            // 4. Batches that reached their ready date become sellable.
            foreach (var node in state.World.Nodes)
            {
                foreach (var batch in node.Batches.ToList())
                {
                    if (batch.State != BatchState.Fermenting) continue;
                    if (state.Date < batch.ReadyOn) continue;
                    batch.MarkReady();
                }
            }

            // 5. Sell finished beer.
            foreach (var node in state.World.Nodes)
                SalesSystem.ProcessNode(state, node);

            // 6. Fixed costs, then wages on the first of the month.
            EconomySystem.ProcessDailyUpkeep(state);
            EconomySystem.ProcessPayrollIfDue(state);

            // 7. Housekeeping.
            EconomySystem.CheckBankruptcy(state);
            state.Ledger.CompactHistory(state.Date);
            state.SyncRandomState();
        }

        public static void AdvanceDays(GameState state, int days)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
            for (int i = 0; i < days; i++) AdvanceDay(state);
        }
    }
}
