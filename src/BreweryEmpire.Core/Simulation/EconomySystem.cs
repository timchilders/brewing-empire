using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Wages, upkeep and the consequences of not paying them.
    ///
    /// Wages fall due on the first of the month for EVERY employee, assigned
    /// or not. Missing payroll costs loyalty, and staff eventually walk.
    /// </summary>
    public static class EconomySystem
    {
        public static void ProcessDailyUpkeep(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            foreach (var node in state.World.Nodes)
            {
                var upkeep = node.TotalDailyUpkeep;
                if (upkeep.IsZero) continue;

                int reduction = state.Staff.AggregateBonus(node.Id.Value, TraitEffect.UpkeepReduction);
                if (reduction > 0)
                    upkeep = upkeep.PercentBasisPoints(Math.Max(0, 10000 - reduction));

                state.Ledger.ForceDebit(state.Date, LedgerCategory.Upkeep, upkeep,
                                        "Daily upkeep", node.Id.Value);
            }
        }

        public static void ProcessPayrollIfDue(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Date.IsFirstOfMonth) return;

            var wages = state.Staff.TotalMonthlyWages;
            if (wages.IsZero) return;

            if (state.Ledger.Balance >= wages)
            {
                state.Ledger.ForceDebit(state.Date, LedgerCategory.Wages, wages,
                                        "Monthly wages");

                foreach (var s in state.Staff.All) s.AdjustLoyalty(200);
            }
            else
            {
                // Unpaid staff lose faith fast.
                foreach (var s in state.Staff.All) s.AdjustLoyalty(-2500);
                state.Staff.ProcessResignations();
            }
        }

        /// <summary>A sustained negative balance ends the run.</summary>
        public static void CheckBankruptcy(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Ledger.Balance.Cents < -100_000) state.IsBankrupt = true;
        }
    }
}
