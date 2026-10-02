using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Model.Ingredients;

namespace BreweryEmpire.Core.Model.Recipes
{
    /// <summary>One grain in the grist, measured in grams per batch.</summary>
    public sealed record GristItem
    {
        public string IngredientId { get; init; } = string.Empty;
        public int Grams { get; init; }
    }

    /// <summary>
    /// A single mash rest: hold the mash at a temperature for a duration.
    ///
    /// WHY TEMPERATURE MATTERS: beta-amylase (around 63C) makes highly
    /// fermentable sugar giving a dry, thin beer; alpha-amylase (around 70C)
    /// makes unfermentable dextrins giving a sweet, full body. This one number
    /// is the most expressive lever a brewer has, so it drives FlavorProfile
    /// directly rather than being folded into a generic "quality" roll.
    /// </summary>
    public sealed record MashStep
    {
        public int TemperatureCelsius { get; init; }
        public int DurationMinutes { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class MashSchedule
    {
        private readonly List<MashStep> _steps = new List<MashStep>();

        public IReadOnlyList<MashStep> Steps => _steps;

        public void AddStep(MashStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (step.TemperatureCelsius < 0 || step.TemperatureCelsius > 100)
                throw new ArgumentOutOfRangeException(nameof(step),
                    "Mash temperature must be between 0 and 100 C.");
            if (step.DurationMinutes <= 0)
                throw new ArgumentOutOfRangeException(nameof(step),
                    "Mash step duration must be positive.");
            _steps.Add(step);
        }

        public int TotalDurationMinutes => _steps.Sum(s => s.DurationMinutes);

        /// <summary>
        /// Duration-weighted mean temperature. Used as the single summary value
        /// for fermentability so a multi-step decoction and a single infusion
        /// can be compared on the same axis.
        /// </summary>
        public int WeightedAverageTemperature
        {
            get
            {
                if (_steps.Count == 0) return 0;
                long weighted = _steps.Sum(s => (long)s.TemperatureCelsius * s.DurationMinutes);
                return (int)(weighted / TotalDurationMinutes);
            }
        }

        /// <summary>
        /// Fermentability in basis points from mash temperature. 63C and below
        /// is very fermentable (dry); 72C and above is dextrinous (sweet).
        /// Linear in between, integer math.
        /// </summary>
        public int FermentabilityBasisPoints
        {
            get
            {
                int t = WeightedAverageTemperature;
                if (t <= 63) return 9000;
                if (t >= 72) return 5500;
                return 9000 - (t - 63) * (9000 - 5500) / 9;
            }
        }

        public static MashSchedule SingleInfusion(int temperatureCelsius, int minutes = 60)
        {
            var s = new MashSchedule();
            s.AddStep(new MashStep
            {
                Name = "Saccharification",
                TemperatureCelsius = temperatureCelsius,
                DurationMinutes = minutes
            });
            return s;
        }

        /// <summary>Traditional continental step mash.</summary>
        public static MashSchedule StepMash()
        {
            var s = new MashSchedule();
            s.AddStep(new MashStep { Name = "Protein rest", TemperatureCelsius = 52, DurationMinutes = 20 });
            s.AddStep(new MashStep { Name = "Beta rest", TemperatureCelsius = 63, DurationMinutes = 40 });
            s.AddStep(new MashStep { Name = "Alpha rest", TemperatureCelsius = 72, DurationMinutes = 30 });
            s.AddStep(new MashStep { Name = "Mash out", TemperatureCelsius = 78, DurationMinutes = 10 });
            return s;
        }
    }

    /// <summary>
    /// A hop charge at a point in the boil.
    ///
    /// Boil time trades bitterness against aroma: long boils isomerise alpha
    /// acid into bitterness and drive off volatile oils, short boils do the
    /// reverse. Modelling the timing is what lets a player design a beer
    /// rather than just pick a hop.
    /// </summary>
    public sealed record HopAddition
    {
        public string IngredientId { get; init; } = string.Empty;
        public int Grams { get; init; }
        public int BoilMinutesRemaining { get; init; }

        /// <summary>Utilisation rises with boil time and plateaus near 60 minutes.</summary>
        public int UtilizationBasisPoints
        {
            get
            {
                int m = BoilMinutesRemaining;
                if (m <= 0) return 0;
                if (m >= 60) return 3000;
                return m * 3000 / 60;
            }
        }

        /// <summary>Aroma survives only late additions — the inverse curve.</summary>
        public int AromaRetentionBasisPoints
        {
            get
            {
                int m = BoilMinutesRemaining;
                if (m <= 0) return 10000;
                if (m >= 30) return 0;
                return 10000 - m * 10000 / 30;
            }
        }
    }

    public sealed class Recipe
    {
        private readonly List<GristItem> _grist = new List<GristItem>();
        private readonly List<HopAddition> _hops = new List<HopAddition>();

        public RecipeId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string YeastIngredientId { get; set; } = "ale-yeast";
        public int TargetVolumeLitres { get; set; }
        public int FermentationDays { get; set; } = 7;
        public int ConditioningDays { get; set; } = 14;
        public MashSchedule Mash { get; set; } = MashSchedule.SingleInfusion(66);

        /// <summary>Target original gravity in gravity points (50 = 1.050).</summary>
        public int TargetOriginalGravityPoints { get; set; } = 50;

        public IReadOnlyList<GristItem> Grist => _grist;
        public IReadOnlyList<HopAddition> Hops => _hops;

        public void AddGrain(string ingredientId, int grams)
        {
            if (grams <= 0) throw new ArgumentOutOfRangeException(nameof(grams));
            _grist.Add(new GristItem { IngredientId = ingredientId, Grams = grams });
        }

        public void AddHop(string ingredientId, int grams, int boilMinutesRemaining)
        {
            if (grams <= 0) throw new ArgumentOutOfRangeException(nameof(grams));
            if (boilMinutesRemaining < 0) throw new ArgumentOutOfRangeException(nameof(boilMinutesRemaining));
            _hops.Add(new HopAddition
            {
                IngredientId = ingredientId,
                Grams = grams,
                BoilMinutesRemaining = boilMinutesRemaining
            });
        }

        public int TotalGristGrams => _grist.Sum(g => g.Grams);

        /// <summary>
        /// THE ENZYME CHECK. A grist needs enough diastatic power from base
        /// malt to convert its own starch. All-roasted-grain "recipes" must
        /// fail here rather than silently producing beer.
        /// </summary>
        public bool HasSufficientDiastaticPower(IngredientCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (_grist.Count == 0) return false;

            long weighted = 0;
            foreach (var g in _grist)
            {
                var ing = catalog.Get(g.IngredientId);
                weighted += (long)ing.DiastaticPowerLintner * g.Grams;
            }

            // ~35 Lintner over the whole grist is the practical floor for conversion.
            return weighted / TotalGristGrams >= 35;
        }

        public int WeightedDiastaticPower(IngredientCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (_grist.Count == 0) return 0;

            long weighted = 0;
            foreach (var g in _grist)
                weighted += (long)catalog.Get(g.IngredientId).DiastaticPowerLintner * g.Grams;

            return (int)(weighted / TotalGristGrams);
        }

        /// <summary>Grist colour contribution, a rough Lovibond weighted mean.</summary>
        public int EstimatedColorLovibond(IngredientCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (_grist.Count == 0) return 0;

            long weighted = 0;
            foreach (var g in _grist)
                weighted += (long)catalog.Get(g.IngredientId).ColorLovibond * g.Grams;

            return (int)(weighted / TotalGristGrams);
        }

        public int TotalDaysToReady => FermentationDays + ConditioningDays;
    }
}
