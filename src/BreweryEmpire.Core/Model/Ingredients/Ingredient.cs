using System;
using System.Collections.Generic;
using BreweryEmpire.Core.Economy;

namespace BreweryEmpire.Core.Model.Ingredients
{
    public enum IngredientType
    {
        BaseMalt = 0,
        SpecialtyMalt = 1,
        RoastedGrain = 2,
        Hops = 3,
        Yeast = 4,
        Adjunct = 5,
        Finings = 6
    }

    /// <summary>
    /// A purchasable brewing ingredient definition (not a physical quantity —
    /// see IngredientLot for that).
    ///
    /// KEY DOMAIN RULE: DiastaticPowerLintner is the enzymatic capacity to
    /// convert starch into fermentable sugar. Base malts carry it; specialty
    /// and roasted grains have ZERO because kilning destroys the enzymes.
    /// This is what stops a player brewing 100% roasted-barley stout — the
    /// mash simply would not convert. It is a real, teachable constraint that
    /// costs us exactly one integer.
    /// </summary>
    public sealed record Ingredient
    {
        public string Id { get; init; } = string.Empty;
        public IngredientType Type { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public Money BasePrice { get; init; }
        public int DiastaticPowerLintner { get; init; }
        public int ColorLovibond { get; init; }
        public int YieldBasisPoints { get; init; }
        public int AlphaAcidBasisPoints { get; init; }
        public int ShelfLifeDays { get; init; }
        public string? RegionId { get; init; }

        /// <summary>True for grain types that can contribute enzymes to a mash.</summary>
        public bool CanSelfConvert => DiastaticPowerLintner > 0;

        /// <summary>
        /// Enforces the enzyme rule. Called by the catalog so bad data fails
        /// loudly at construction rather than producing silently wrong beer.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(Id))
                throw new ArgumentException("Ingredient Id must not be empty.");

            if ((Type == IngredientType.RoastedGrain || Type == IngredientType.SpecialtyMalt)
                && DiastaticPowerLintner != 0)
            {
                throw new ArgumentException(
                    "Roasted and specialty malts must have zero diastatic power: " +
                    "kilning destroys the enzymes. Offending ingredient: " + Id);
            }

            if (DiastaticPowerLintner < 0)
                throw new ArgumentException("Diastatic power must not be negative: " + Id);

            if (Type != IngredientType.Hops && AlphaAcidBasisPoints != 0)
                throw new ArgumentException("Only hops carry alpha acid: " + Id);
        }
    }

    /// <summary>Lookup of all known ingredient definitions.</summary>
    public sealed class IngredientCatalog
    {
        private readonly Dictionary<string, Ingredient> _byId =
            new Dictionary<string, Ingredient>(StringComparer.Ordinal);

        public void Add(Ingredient ingredient)
        {
            if (ingredient == null) throw new ArgumentNullException(nameof(ingredient));
            ingredient.Validate();
            _byId[ingredient.Id] = ingredient;
        }

        public Ingredient Get(string id) =>
            _byId.TryGetValue(id, out var i)
                ? i
                : throw new KeyNotFoundException("Unknown ingredient: " + id);

        public bool TryGet(string id, out Ingredient ingredient) => _byId.TryGetValue(id, out ingredient!);

        public bool Contains(string id) => _byId.ContainsKey(id);

        public int Count => _byId.Count;

        public IEnumerable<Ingredient> All => _byId.Values;

        /// <summary>
        /// Historically-grounded starter catalogue. Colour figures are Lovibond,
        /// diastatic power in degrees Lintner, alpha acid in basis points.
        /// </summary>
        public static IngredientCatalog CreateDefault()
        {
            var c = new IngredientCatalog();

            c.Add(new Ingredient
            {
                Id = "pale-malt", Type = IngredientType.BaseMalt, DisplayName = "Pale Malt",
                BasePrice = Money.FromCents(45), DiastaticPowerLintner = 140,
                ColorLovibond = 2, YieldBasisPoints = 8000, ShelfLifeDays = 540
            });
            c.Add(new Ingredient
            {
                Id = "munich-malt", Type = IngredientType.BaseMalt, DisplayName = "Munich Malt",
                BasePrice = Money.FromCents(52), DiastaticPowerLintner = 70,
                ColorLovibond = 10, YieldBasisPoints = 7800, ShelfLifeDays = 540
            });
            c.Add(new Ingredient
            {
                Id = "crystal-malt", Type = IngredientType.SpecialtyMalt, DisplayName = "Crystal Malt",
                BasePrice = Money.FromCents(65), DiastaticPowerLintner = 0,
                ColorLovibond = 60, YieldBasisPoints = 7200, ShelfLifeDays = 540
            });
            c.Add(new Ingredient
            {
                Id = "chocolate-malt", Type = IngredientType.RoastedGrain, DisplayName = "Chocolate Malt",
                BasePrice = Money.FromCents(70), DiastaticPowerLintner = 0,
                ColorLovibond = 350, YieldBasisPoints = 6500, ShelfLifeDays = 540
            });
            c.Add(new Ingredient
            {
                Id = "roasted-barley", Type = IngredientType.RoastedGrain, DisplayName = "Roasted Barley",
                BasePrice = Money.FromCents(68), DiastaticPowerLintner = 0,
                ColorLovibond = 500, YieldBasisPoints = 6300, ShelfLifeDays = 540
            });
            c.Add(new Ingredient
            {
                Id = "black-patent", Type = IngredientType.RoastedGrain, DisplayName = "Black Patent Malt",
                BasePrice = Money.FromCents(72), DiastaticPowerLintner = 0,
                ColorLovibond = 500, YieldBasisPoints = 6200, ShelfLifeDays = 540
            });

            c.Add(new Ingredient
            {
                Id = "goldings-hops", Type = IngredientType.Hops, DisplayName = "East Kent Goldings",
                BasePrice = Money.FromCents(320), AlphaAcidBasisPoints = 500,
                ShelfLifeDays = 365, RegionId = "kent"
            });
            c.Add(new Ingredient
            {
                Id = "fuggles-hops", Type = IngredientType.Hops, DisplayName = "Fuggles",
                BasePrice = Money.FromCents(300), AlphaAcidBasisPoints = 450,
                ShelfLifeDays = 365, RegionId = "worcestershire"
            });
            c.Add(new Ingredient
            {
                Id = "saaz-hops", Type = IngredientType.Hops, DisplayName = "Saaz",
                BasePrice = Money.FromCents(380), AlphaAcidBasisPoints = 350,
                ShelfLifeDays = 365, RegionId = "bohemia"
            });

            c.Add(new Ingredient
            {
                Id = "ale-yeast", Type = IngredientType.Yeast, DisplayName = "Ale Yeast",
                BasePrice = Money.FromCents(150), ShelfLifeDays = 90
            });
            c.Add(new Ingredient
            {
                Id = "lager-yeast", Type = IngredientType.Yeast, DisplayName = "Lager Yeast",
                BasePrice = Money.FromCents(180), ShelfLifeDays = 90
            });

            return c;
        }
    }
}
