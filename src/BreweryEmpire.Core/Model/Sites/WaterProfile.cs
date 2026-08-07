using System;

namespace BreweryEmpire.Core.Model.Sites
{
    /// <summary>
    /// Brewing water ion profile in parts per million.
    ///
    /// WHY THIS MATTERS: water chemistry is the single best historical
    /// explanation for why styles are tied to places. Burton's gypsum-rich
    /// wells made pale ale possible; Pilsen's near-distilled water made the
    /// world's first pale lager; Dublin's carbonate water suits roasted stout.
    /// Modelling six ions gives the player a real reason to care about
    /// geography rather than treating all sites as interchangeable.
    /// </summary>
    public sealed record WaterProfile
    {
        public int CalciumPpm { get; init; }
        public int SulfatePpm { get; init; }
        public int ChloridePpm { get; init; }
        public int CarbonatePpm { get; init; }
        public int MagnesiumPpm { get; init; }
        public int SodiumPpm { get; init; }

        /// <summary>Cap used when chloride is zero, to avoid divide-by-zero.</summary>
        public const int MaxRatioBasisPoints = 1_000_000;

        /// <summary>
        /// Sulfate:chloride ratio in basis points (10000 = 1:1).
        /// High favours crisp, bitter pale ales; low favours malty, round beers.
        /// </summary>
        public int SulfateChlorideRatioBasisPoints
        {
            get
            {
                if (ChloridePpm <= 0)
                    return SulfatePpm <= 0 ? 10000 : MaxRatioBasisPoints;

                long ratio = (long)SulfatePpm * 10000 / ChloridePpm;
                return ratio > MaxRatioBasisPoints ? MaxRatioBasisPoints : (int)ratio;
            }
        }

        /// <summary>Soft water (low carbonate) is required for delicate pale lagers.</summary>
        public bool IsSoft => CarbonatePpm < 50;

        /// <summary>
        /// How well this water suits a target profile, 10000 = perfect.
        /// Integer L1 distance across ions, scaled and clamped. Deliberately
        /// simple in Phase 1 — Phase 2 can weight ions by style.
        /// </summary>
        public int FitScoreBasisPoints(WaterProfile target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            long distance =
                Math.Abs(CalciumPpm - target.CalciumPpm) +
                Math.Abs(SulfatePpm - target.SulfatePpm) +
                Math.Abs(ChloridePpm - target.ChloridePpm) +
                Math.Abs(CarbonatePpm - target.CarbonatePpm) +
                Math.Abs(MagnesiumPpm - target.MagnesiumPpm) +
                Math.Abs(SodiumPpm - target.SodiumPpm);

            // ~1500ppm total deviation drives the score to zero.
            long penalty = distance * 10000 / 1500;
            long score = 10000 - penalty;

            if (score < 0) return 0;
            if (score > 10000) return 10000;
            return (int)score;
        }

        /// <summary>Burton-on-Trent: gypsum-heavy, the home of pale ale.</summary>
        public static WaterProfile Burton => new WaterProfile
        {
            CalciumPpm = 275, SulfatePpm = 610, ChloridePpm = 35,
            CarbonatePpm = 270, MagnesiumPpm = 40, SodiumPpm = 25
        };

        /// <summary>Pilsen: extremely soft, enabled the first pale lager.</summary>
        public static WaterProfile Pilsen => new WaterProfile
        {
            CalciumPpm = 7, SulfatePpm = 5, ChloridePpm = 5,
            CarbonatePpm = 15, MagnesiumPpm = 2, SodiumPpm = 2
        };

        /// <summary>Munich: carbonate-rich, suits darker malty lagers.</summary>
        public static WaterProfile Munich => new WaterProfile
        {
            CalciumPpm = 75, SulfatePpm = 10, ChloridePpm = 2,
            CarbonatePpm = 200, MagnesiumPpm = 18, SodiumPpm = 10
        };

        /// <summary>Dublin: high carbonate, balances roasted stout acidity.</summary>
        public static WaterProfile Dublin => new WaterProfile
        {
            CalciumPpm = 120, SulfatePpm = 55, ChloridePpm = 19,
            CarbonatePpm = 300, MagnesiumPpm = 4, SodiumPpm = 12
        };

        /// <summary>London: moderate carbonate, historic porter water.</summary>
        public static WaterProfile London => new WaterProfile
        {
            CalciumPpm = 52, SulfatePpm = 32, ChloridePpm = 34,
            CarbonatePpm = 104, MagnesiumPpm = 16, SodiumPpm = 86
        };
    }
}
