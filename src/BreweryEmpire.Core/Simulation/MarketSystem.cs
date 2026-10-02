using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Markets;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Sells beer at markets, realising revenue and excise duty.
    ///
    /// Price = base × quality × market fit × reputation × salesman. Excise duty
    /// is charged on volume regardless of price, which is why weak "small beer"
    /// was historically attractive — the duty hit the same but the base price
    /// was far lower.
    /// </summary>
    public static class MarketSystem
    {
        /// <summary>Price per litre in basis-point-scaled cents, for a batch at a market.</summary>
        public static int PricePerLitreBasisPoints(GameState state, MarketNode market, Batch batch)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (market == null) throw new ArgumentNullException(nameof(market));
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            long price = market.BasePricePerLitre.Cents * 100;   // x100 = basis-point space

            // Quality swings price between 50% and 150%.
            price = price * (5000 + batch.QualityBasisPoints) / 10000;

            // Market fit: a beer the market wants sells at full price; a mismatch
            // is discounted toward half.
            int fit = batch.Flavor.MarketFitBasisPoints(market.PreferredProfile);
            price = price * (5000 + fit / 2) / 10000;

            // Reputation swings price.
            price = price * market.ReputationFactorBasisPoints / 10000;

            // Salesmen realise better prices.
            int salesBonus = state.Staff.AggregateBonus(market.AdjacentNodeId, TraitEffect.PriceRealization);
            if (salesBonus > 0)
                price = price * (10000 + salesBonus) / 10000;

            return (int)price;
        }

        /// <summary>Sell beer at every market, in deterministic id order.</summary>
        public static void ProcessMarkets(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            foreach (var market in state.Markets.OrderBy(m => m.Id, StringComparer.Ordinal))
                ProcessMarket(state, market);
        }

        private static void ProcessMarket(GameState state, MarketNode market)
        {
            var node = state.World.TryGet(new NodeId(market.AdjacentNodeId), out var n) ? n : null;
            if (node == null) return;

            // Oldest sellable beer first, so stock rotates.
            var sellable = node.Batches
                .Where(b => b.IsSellable)
                .OrderBy(b => b.ReadyOn.TotalDays)
                .ThenBy(b => b.Id.Value, StringComparer.Ordinal)
                .ToList();

            // Spoiled beer that reached a market damages reputation once.
            foreach (var spoiled in node.Batches.Where(b => b.State == BatchState.Spoiled).ToList())
            {
                market.AdjustReputation(-1000);
                node.RemoveBatch(spoiled.Id);
            }

            foreach (var batch in sellable)
            {
                int demand = market.EffectiveDemandLitres(batch.Style, state.Date);
                if (demand <= 0) continue;

                int litres = Math.Min(demand, batch.VolumeLitres);
                if (litres <= 0) continue;

                long unitPrice = PricePerLitreBasisPoints(state, market, batch);
                var revenue = Money.FromCents(unitPrice / 100 * litres);
                var duty = state.Prices.ExciseDutyPerLitre(state.Date.Era) * litres;

                batch.Remove(litres);

                state.Ledger.Credit(state.Date, LedgerCategory.BeerSales, revenue,
                                    "Sold " + litres + "L of " + batch.Id + " at " + market.Name,
                                    market.AdjacentNodeId);

                state.Ledger.ForceDebit(state.Date, LedgerCategory.ExciseDuty, duty,
                                        "Excise duty on " + litres + "L", market.AdjacentNodeId);
            }

            // Drop fully-sold batches so the list does not grow forever.
            foreach (var empty in node.Batches.Where(b => b.VolumeLitres <= 0).ToList())
                node.RemoveBatch(empty.Id);
        }
    }
}
