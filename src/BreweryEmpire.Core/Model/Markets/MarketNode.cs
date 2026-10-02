using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Markets
{
    /// <summary>
    /// A market where beer is sold — distinct from where it is made.
    ///
    /// A market has its own climate (seasonal demand), population, per-style
    /// base demand, a flavour preference, and a reputation. Reputation is how
    /// spoilage bites back: bad beer arriving damages your brand and depresses
    /// future prices, which is the feedback loop that makes cold-chain
    /// investment feel necessary rather than optional.
    /// </summary>
    public sealed class MarketNode
    {
        private readonly Dictionary<BeerStyle, int> _baseDemand =
            new Dictionary<BeerStyle, int>();

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>The node id this market is served from (for local adjacency).</summary>
        public string AdjacentNodeId { get; set; } = string.Empty;

        public RegionClimate Climate { get; set; } = RegionClimate.BurtonEngland;
        public int Population { get; set; }

        public int ReputationBasisPoints { get; private set; } = 5000;

        /// <summary>Baseline price per litre the market will pay, before modifiers.</summary>
        public Money BasePricePerLitre { get; set; } = Money.FromCents(60);

        /// <summary>The flavour profile this market rewards most.</summary>
        public FlavorProfile PreferredProfile { get; set; } = new FlavorProfile
        {
            BitternessBasisPoints = 4000, BodyBasisPoints = 3500,
            SweetnessBasisPoints = 3000, MaltinessBasisPoints = 3500,
            HopAromaBasisPoints = 2500, AlcoholBasisPoints = 450
        };

        public bool IsTiedHouse { get; set; }
        public string? OwnerBreweryId { get; set; }

        public static MarketNode Create(string id, string name, NodeId adjacentNode,
                                        RegionClimate climate, int population)
        {
            return new MarketNode
            {
                Id = id,
                Name = name,
                AdjacentNodeId = adjacentNode.Value,
                Climate = climate,
                Population = population
            };
        }

        public void SetBaseDemand(BeerStyle style, int litresPerTick)
        {
            if (litresPerTick < 0) throw new ArgumentOutOfRangeException(nameof(litresPerTick));
            _baseDemand[style] = litresPerTick;
        }

        public int BaseDemandLitresPerTick(BeerStyle style) =>
            _baseDemand.TryGetValue(style, out var d) ? d : 0;

        /// <summary>All base-demand entries, for save serialization.</summary>
        public IEnumerable<KeyValuePair<BeerStyle, int>> BaseDemandEntries() =>
            _baseDemand.OrderBy(kv => (int)kv.Key);

        public void AdjustReputation(int delta) =>
            ReputationBasisPoints = Math.Clamp(ReputationBasisPoints + delta, 0, 10000);

        /// <summary>Reputation as a demand factor: 5000bp (neutral) .. 15000bp.</summary>
        public int ReputationFactorBasisPoints => 5000 + ReputationBasisPoints;

        /// <summary>Population as a demand factor, 10000bp at 10k people.</summary>
        public int PopulationFactorBasisPoints
        {
            get
            {
                long f = 2000 + (long)Population * 8000 / 40000;
                return (int)Math.Clamp(f, 2000, 10000);
            }
        }

        /// <summary>
        /// Seasonal demand factor for a style. Refreshing, bitter styles sell in
        /// summer; strong, warming styles sell in winter. Integer, continuous
        /// across the year boundary (month-based, so no wrap discontinuity).
        /// </summary>
        public int SeasonalFactorBasisPoints(BeerStyle style, GameDate date)
        {
            int temp = Climate.AmbientTempOn(date);
            int annualMean = Climate.WarmestMonthTemp + Climate.ColdestMonthTemp;
            annualMean /= 2;

            bool summerStyle = style == BeerStyle.PaleAle || style == BeerStyle.Pilsner ||
                               style == BeerStyle.Lager || style == BeerStyle.BerlinerWeisse ||
                               style == BeerStyle.Lambic;

            int deviation = temp - annualMean;   // positive in summer, negative in winter

            // ±3000bp swing across the year.
            int factor = 10000 + deviation * 300;
            factor = summerStyle ? factor : -factor + 20000;

            return (int)Math.Clamp(factor, 7000, 13000);
        }

        /// <summary>Effective litres demanded of a style on a date.</summary>
        public int EffectiveDemandLitres(BeerStyle style, GameDate date)
        {
            int baseDemand = BaseDemandLitresPerTick(style);
            if (baseDemand == 0) return 0;

            long litres = baseDemand;
            litres = litres * PopulationFactorBasisPoints / 10000;
            litres = litres * SeasonalFactorBasisPoints(style, date) / 10000;
            litres = litres * ReputationFactorBasisPoints / 10000;

            return (int)litres;
        }
    }
}
