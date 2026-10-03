using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Research
{
    /// <summary>
    /// The research tree. Eras and prerequisites come from the design doc's
    /// "Technology & Historical Science Tree" (section 3). Effects are NOT here —
    /// ResearchSystem.ApplyEffect owns them.
    /// </summary>
    public static class TechCatalog
    {
        private static readonly List<TechNode> _all = new List<TechNode>
        {
            new TechNode
            {
                Id = "malting-kilns", DisplayName = "Malting Kilns", MinEra = Era.PreIndustrial,
                ResearchCostPoints = 120,
                Description = "Roast barley into dark malt.",
                LoreText = "Kilning malt unlocks porter and stout — dark, robust beers that keep."
            },
            new TechNode
            {
                Id = "saccharometer", DisplayName = "Saccharometer & Hydrometer", MinEra = Era.Industrial,
                ResearchCostPoints = 180, PrerequisiteIds = new[] { "malting-kilns" },
                Description = "Measure sugar density to hit gravity targets.",
                LoreText = "Measuring rather than guessing raises extract efficiency."
            },
            new TechNode
            {
                Id = "drum-roaster", DisplayName = "Patent Drum Roaster", MinEra = Era.Industrial,
                ResearchCostPoints = 220, PrerequisiteIds = new[] { "malting-kilns" },
                Description = "Super-roasted unmalted barley for light-bodied dark stouts.",
                LoreText = "Guinness-era roasting. (Effect deferred — no shelf-life model yet.)"
            },
            new TechNode
            {
                Id = "pale-revolution", DisplayName = "The Bohemian Pale Revolution", MinEra = Era.Industrial,
                ResearchCostPoints = 300, PrerequisiteIds = new[] { "saccharometer" },
                Description = "Soft water + pale malt + bottom-fermenting yeast = golden lager.",
                LoreText = "Pilsner Urquell, 1842: shifts world demand from dark ales to golden lager."
            },
            new TechNode
            {
                Id = "ice-house", DisplayName = "Ice House Logistics", MinEra = Era.Industrial,
                ResearchCostPoints = 150,
                Description = "Store winter lake ice in insulated cellars for summer brewing.",
                LoreText = "Bavaria banned summer brewing in 1553; stored ice beat the heat."
            },
            new TechNode
            {
                Id = "pure-yeast", DisplayName = "Pure Yeast Isolation", MinEra = Era.Scientific,
                ResearchCostPoints = 400, PrerequisiteIds = new[] { "saccharometer" },
                Description = "Isolate a single yeast strain; eliminates wild-yeast contamination.",
                LoreText = "Carlsberg, 1883: Saccharomyces carlsbergensis — the first pure culture."
            },
            new TechNode
            {
                Id = "refrigeration", DisplayName = "Mechanical Refrigeration", MinEra = Era.Scientific,
                ResearchCostPoints = 450, PrerequisiteIds = new[] { "ice-house" },
                Description = "Ammonia compressor removes seasonal brewing limits.",
                LoreText = "Linde, 1873: year-round cold fermentation without an ice harvest."
            },
            new TechNode
            {
                Id = "pasteurization", DisplayName = "Pasteurization", MinEra = Era.Scientific,
                ResearchCostPoints = 350, PrerequisiteIds = new[] { "pure-yeast" },
                Description = "Heat-treat finished beer to near-immunity to spoilage.",
                LoreText = "Gentle heating kills spoilage organisms before shipping."
            },
            new TechNode
            {
                Id = "water-chemistry", DisplayName = "Water Chemistry & pH", MinEra = Era.Scientific,
                ResearchCostPoints = 380, PrerequisiteIds = new[] { "saccharometer" },
                Description = "Adjust water hardness and acidity to suit a style.",
                LoreText = "Carlsberg Lab, 1909: matching water to style raises extract."
            },
            new TechNode
            {
                Id = "stainless-steel", DisplayName = "Stainless Steel Vessels", MinEra = Era.Modern,
                ResearchCostPoints = 500, PrerequisiteIds = new[] { "refrigeration" },
                Description = "Maximum hygiene and consistency. (Effect deferred — no build system yet.)",
                LoreText = "Inert steel displaces wood and copper."
            },
            new TechNode
            {
                Id = "bottling-line", DisplayName = "Automated Bottling & Canning", MinEra = Era.Modern,
                ResearchCostPoints = 450, PrerequisiteIds = new[] { "pasteurization" },
                Description = "High-throughput packaging for global logistics.",
                LoreText = "Sealed, long-lived containers open distant markets."
            }
        };

        public static IReadOnlyList<TechNode> All => _all;

        public static TechNode? Get(string id) => _all.FirstOrDefault(t => t.Id == id);

        public static IEnumerable<TechNode> AvailableIn(GameDate date) =>
            _all.Where(t => date.Era >= t.MinEra);

        /// <summary>The tech that must be unlocked to brew a style; null = available from Era 1.</summary>
        public static string? RequiredTechFor(BeerStyle style) => style switch
        {
            BeerStyle.Porter or BeerStyle.Stout or BeerStyle.Mild => "malting-kilns",
            BeerStyle.Pilsner or BeerStyle.Lager or BeerStyle.ViennaLager or BeerStyle.Bock => "pale-revolution",
            _ => null   // PaleAle, BerlinerWeisse, Lambic are the era-1 baseline
        };

        /// <summary>The water profile a style prefers (used by the water-chemistry effect).</summary>
        public static WaterProfile TargetWaterFor(BeerStyle style) => style switch
        {
            BeerStyle.PaleAle => WaterProfile.Burton,
            BeerStyle.Pilsner or BeerStyle.Lager or BeerStyle.ViennaLager or BeerStyle.Bock => WaterProfile.Pilsen,
            BeerStyle.Porter or BeerStyle.Mild => WaterProfile.London,
            BeerStyle.Stout => WaterProfile.Dublin,
            _ => WaterProfile.Burton
        };
    }
}
