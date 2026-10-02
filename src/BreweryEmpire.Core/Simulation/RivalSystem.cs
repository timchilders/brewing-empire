using System;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Markets;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Lightweight competitors. A rival with a presence at a market captures a
    /// share of demand (lowering what the player can sell there). Kept simple
    /// on purpose: no rival logistics or loans, just a demand-share lever that
    /// is deterministic for a seed.
    /// </summary>
    public static class RivalSystem
    {
        /// <summary>Share of a market's demand the player retains, basis points.</summary>
        public static int PlayerDemandShareBasisPoints(MarketNode market, BeerStyle style,
                                                       System.Collections.Generic.IReadOnlyList<RivalBrewer> rivals)
        {
            if (market == null) throw new ArgumentNullException(nameof(market));

            long rivalShare = 0;
            foreach (var rival in rivals)
            {
                if (!string.Equals(rival.HomeRegionId, market.AdjacentNodeId, StringComparison.Ordinal))
                    continue;

                // A rival's strength caps out at a 40% demand share.
                rivalShare += Math.Min(4000, rival.StrengthBasisPoints);
            }

            return (int)Math.Max(0, 10000 - rivalShare);
        }

        /// <summary>Effective demand the player can sell into at a market.</summary>
        public static int EffectivePlayerDemand(MarketNode market, BeerStyle style, GameDate date,
                                                System.Collections.Generic.IReadOnlyList<RivalBrewer> rivals)
        {
            int demand = market.EffectiveDemandLitres(style, date);
            return demand * PlayerDemandShareBasisPoints(market, style, rivals) / 10000;
        }

        /// <summary>Advance rival state (placeholder loop; demand share is passive).</summary>
        public static void ProcessRivals(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Rivals are passive in Phase 2: their strength is fixed and their
            // only effect is the demand share computed above. No state changes.
        }
    }
}
