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

            // The clock always advances by exactly one day. Bankruptcy is a
            // terminal *flag* the UI checks to stop ticking; it must never
            // freeze the calendar, or replays and the "one tick = one day"
            // invariant both break.
            state.Date = state.Date.AddDays(1);

            // 2. Fermentation: vessels tick, temperature applies, batches ready.
            foreach (var node in state.World.Nodes)
                FermentationSystem.ProcessNode(state, node);

            // 3. Infection rolls against today's temperature.
            foreach (var node in state.World.Nodes)
                SpoilageSystem.ProcessNode(state, node);

            // 4. Sell finished beer.
            foreach (var node in state.World.Nodes)
                SalesSystem.ProcessNode(state, node);

            // 6. Fixed costs, then wages on the first of the month.
            EconomySystem.ProcessDailyUpkeep(state);
            EconomySystem.ProcessPayrollIfDue(state);

            // 7. Housekeeping. Bankruptcy is detected here, not used to skip
            //    the systems above — a ruined brewery still pays its staff and
            //    they still walk out.
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
