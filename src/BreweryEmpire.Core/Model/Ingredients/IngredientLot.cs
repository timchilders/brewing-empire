using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Ingredients
{
    /// <summary>
    /// A physical quantity of one ingredient from one purchase/harvest.
    ///
    /// WHY LOTS: a single fungible pile cannot carry a bad harvest into a
    /// specific batch. Lots make traceability, harvest-year disasters and
    /// honest COGS accounting possible for the price of one class.
    /// </summary>
    public sealed class IngredientLot
    {
        public LotId Id { get; set; }
        public string IngredientId { get; set; } = string.Empty;
        public int QuantityGrams { get; private set; }
        public int HarvestYear { get; set; }
        public int QualityBasisPoints { get; set; } = 10000;
        public GameDate AcquiredOn { get; set; }
        public Money UnitCostPaid { get; set; }

        public IngredientLot() { }

        public IngredientLot(LotId id, string ingredientId, int quantityGrams,
                             int harvestYear, int qualityBasisPoints,
                             GameDate acquiredOn, Money unitCostPaid)
        {
            if (quantityGrams < 0)
                throw new ArgumentOutOfRangeException(nameof(quantityGrams),
                    quantityGrams, "Lot quantity must not be negative.");

            Id = id;
            IngredientId = ingredientId;
            QuantityGrams = quantityGrams;
            HarvestYear = harvestYear;
            QualityBasisPoints = qualityBasisPoints;
            AcquiredOn = acquiredOn;
            UnitCostPaid = unitCostPaid;
        }

        public bool IsEmpty => QuantityGrams == 0;

        /// <summary>Remove grams. Throws and leaves the lot untouched if short.</summary>
        public void Remove(int grams)
        {
            if (grams < 0)
                throw new ArgumentOutOfRangeException(nameof(grams), grams, "Cannot remove negative grams.");
            if (grams > QuantityGrams)
                throw new InvalidOperationException(
                    "Cannot remove " + grams + "g from a lot holding " + QuantityGrams + "g.");

            QuantityGrams -= grams;
        }

        public void Add(int grams)
        {
            if (grams < 0)
                throw new ArgumentOutOfRangeException(nameof(grams), grams, "Cannot add negative grams.");
            QuantityGrams += grams;
        }

        /// <summary>
        /// Hop alpha acid degrades in storage — a real historical problem and a
        /// reason to keep hop inventory turning over. Loses ~20% of remaining
        /// alpha per year, integer math, floored at zero and monotonic.
        /// </summary>
        public int EffectiveAlphaAcidBasisPoints(Ingredient ingredient, GameDate now)
        {
            if (ingredient == null) throw new ArgumentNullException(nameof(ingredient));
            if (ingredient.AlphaAcidBasisPoints <= 0) return 0;

            int daysHeld = AcquiredOn.DaysUntil(now);
            if (daysHeld <= 0) return ingredient.AlphaAcidBasisPoints;

            int yearsHeld = daysHeld / GameDate.DaysPerYear;
            int value = ingredient.AlphaAcidBasisPoints;

            for (int y = 0; y < yearsHeld && value > 0; y++)
                value = value * 80 / 100;

            return value < 0 ? 0 : value;
        }
    }

    /// <summary>One consumption draw against a specific lot, for COGS.</summary>
    public readonly struct LotDraw
    {
        public LotId LotId { get; }
        public int Grams { get; }
        public Money UnitCost { get; }

        public LotDraw(LotId lotId, int grams, Money unitCost)
        {
            LotId = lotId;
            Grams = grams;
            UnitCost = unitCost;
        }

        /// <summary>UnitCost is per kilogram; grams are converted here.</summary>
        public Money TotalCost => Money.FromCents(UnitCost.Cents * Grams / 1000);
    }

    public sealed class Inventory
    {
        private readonly List<IngredientLot> _lots = new List<IngredientLot>();

        public IReadOnlyList<IngredientLot> Lots => _lots;

        public void AddLot(IngredientLot lot)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            _lots.Add(lot);
        }

        public int TotalGramsOf(string ingredientId)
        {
            int total = 0;
            foreach (var l in _lots)
                if (string.Equals(l.IngredientId, ingredientId, StringComparison.Ordinal))
                    total += l.QuantityGrams;
            return total;
        }

        public bool HasAtLeast(string ingredientId, int grams) => TotalGramsOf(ingredientId) >= grams;

        /// <summary>
        /// FIFO draw, oldest lot first. Ties on AcquiredOn break by LotId ordinal
        /// so the order is fully deterministic — unordered iteration here would
        /// leak into batch COGS and break the determinism tests.
        /// Throws without mutating anything if stock is short.
        /// </summary>
        public IReadOnlyList<LotDraw> Consume(string ingredientId, int grams)
        {
            if (grams < 0) throw new ArgumentOutOfRangeException(nameof(grams));
            if (grams == 0) return Array.Empty<LotDraw>();

            if (!HasAtLeast(ingredientId, grams))
                throw new InvalidOperationException(
                    "Insufficient " + ingredientId + ": need " + grams +
                    "g, have " + TotalGramsOf(ingredientId) + "g.");

            var ordered = _lots
                .Where(l => string.Equals(l.IngredientId, ingredientId, StringComparison.Ordinal)
                            && l.QuantityGrams > 0)
                .OrderBy(l => l.AcquiredOn.TotalDays)
                .ThenBy(l => l.Id.Value, StringComparer.Ordinal)
                .ToList();

            var draws = new List<LotDraw>();
            int remaining = grams;

            foreach (var lot in ordered)
            {
                if (remaining == 0) break;
                int take = Math.Min(remaining, lot.QuantityGrams);
                lot.Remove(take);
                draws.Add(new LotDraw(lot.Id, take, lot.UnitCostPaid));
                remaining -= take;
            }

            _lots.RemoveAll(l => l.IsEmpty);
            return draws;
        }

        public static Money CostOfConsumption(IReadOnlyList<LotDraw> draws)
        {
            if (draws == null) throw new ArgumentNullException(nameof(draws));
            long cents = 0;
            foreach (var d in draws) cents += d.TotalCost.Cents;
            return Money.FromCents(cents);
        }
    }
}
