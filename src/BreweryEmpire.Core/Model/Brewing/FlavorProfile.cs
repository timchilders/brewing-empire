using System;

namespace BreweryEmpire.Core.Model.Brewing
{
    /// <summary>
    /// What a beer TASTES like — the design axis.
    ///
    /// Deliberately separate from Quality. FlavorProfile answers "what did you
    /// set out to make?" and comes from recipe decisions (mash temperature,
    /// grist, hop timing, water). Quality answers "how well did you execute
    /// it?" and comes from skill, equipment and luck. A brilliantly executed
    /// beer nobody wants should still sell badly, and that is only expressible
    /// if these two are independent.
    /// </summary>
    public sealed record FlavorProfile
    {
        public int BitternessBasisPoints { get; init; }
        public int SweetnessBasisPoints { get; init; }
        public int MaltinessBasisPoints { get; init; }
        public int HopAromaBasisPoints { get; init; }
        public int BodyBasisPoints { get; init; }
        public int ColorLovibond { get; init; }
        public int AlcoholBasisPoints { get; init; }

        /// <summary>
        /// Similarity to a target profile, 10000 = identical.
        /// Used for market fit: a stout sold to a pale-ale market scores badly
        /// however well it was brewed.
        /// </summary>
        public int MatchScoreBasisPoints(FlavorProfile target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            long distance =
                Math.Abs(BitternessBasisPoints - target.BitternessBasisPoints) +
                Math.Abs(SweetnessBasisPoints - target.SweetnessBasisPoints) +
                Math.Abs(MaltinessBasisPoints - target.MaltinessBasisPoints) +
                Math.Abs(HopAromaBasisPoints - target.HopAromaBasisPoints) +
                Math.Abs(BodyBasisPoints - target.BodyBasisPoints) +
                Math.Abs(AlcoholBasisPoints - target.AlcoholBasisPoints);

            // Six axes, each up to 10000 apart; 30000 total deviation -> zero.
            long score = 10000 - distance * 10000 / 30000;
            if (score < 0) return 0;
            if (score > 10000) return 10000;
            return (int)score;
        }
    }

    /// <summary>A defect present in a finished beer.</summary>
    public enum OffFlavor
    {
        None = 0,
        Diacetyl = 1,        // butterscotch: rushed fermentation
        Acetaldehyde = 2,    // green apple: beer packaged too young
        Sour = 3,            // lactobacillus / pediococcus infection
        Phenolic = 4,        // medicinal: wild yeast or bad water
        Oxidized = 5,        // cardboard: oxygen ingress in packaging
        Sulfur = 6,          // struck match: stressed yeast
        Astringent = 7       // harsh: oversparging or overmilled grain
    }

    /// <summary>An infection event on a batch.</summary>
    public sealed record Infection
    {
        public OffFlavor Character { get; init; }
        public int SeverityBasisPoints { get; init; }
        public string Cause { get; init; } = string.Empty;
    }
}
