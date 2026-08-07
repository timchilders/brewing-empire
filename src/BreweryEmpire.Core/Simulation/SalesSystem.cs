using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Converts finished beer into money.
    ///
    /// Price is base price x quality x reputation x salesman, and every sale
    /// pays excise duty. Duty is charged on volume sold regardless of price,
    /// which is exactly why weak "small beer" was historically attractive.
    /// </summary>
    public static class SalesSystem
    {
        /// <summary>Beer a node can shift per day before saturating its market.</summary>
        public const int BaseDailyDemandLitres = 200;

        internal static Money PricePerLitre(GameState state, BreweryNode node, Batch batch)
        {
            var basePrice = Money.FromCents(60);

            // Quality swings price between 50% and 150%.
            int qualityFactor = 5000 + batch.QualityBasisPoints;
            var price = basePrice.PercentBasisPoints(qualityFactor);

            // Reputation swings it a further +/-25%.
            int reputationFactor = 7500 + state.ReputationBasisPoints / 2;
            price = price.PercentBasisPoints(reputationFactor);

            int salesBonus = state.Staff.AggregateBonus(node.Id.Value, TraitEffect.PriceRealization);
            if (salesBonus > 0) price = price.PercentBasisPoints(10000 + salesBonus);

            return price;
        }

        /// <summary>Sell what the local market will absorb today.</summary>
        public static void ProcessNode(GameState state, BreweryNode node)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));

            int remainingDemand = BaseDailyDemandLitres;

            // Oldest sellable beer first, so stock actually rotates.
            var sellable = node.Batches
                .Where(b => b.IsSellable)
                .OrderBy(b => b.ReadyOn.TotalDays)
                .ThenBy(b => b.Id.Value, StringComparer.Ordinal)
                .ToList();

            foreach (var batch in sellable)
            {
                if (remainingDemand <= 0) break;

                int litres = Math.Min(remainingDemand, batch.VolumeLitres);
                if (litres <= 0) continue;

                var unitPrice = PricePerLitre(state, node, batch);
                var revenue = unitPrice * litres;
                var duty = state.Prices.ExciseDutyPerLitre(state.Date.Era) * litres;

                batch.Remove(litres);
                remainingDemand -= litres;

                state.Ledger.Credit(state.Date, LedgerCategory.BeerSales, revenue,
                                    "Sold " + litres + "L of " + batch.Id, node.Id.Value);

                state.Ledger.ForceDebit(state.Date, LedgerCategory.ExciseDuty, duty,
                                        "Excise duty on " + litres + "L", node.Id.Value);
            }

            // Drop fully-sold batches so the list does not grow forever.
            foreach (var empty in node.Batches.Where(b => b.VolumeLitres <= 0).ToList())
                node.RemoveBatch(empty.Id);
        }
    }

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
