using System;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Integer hop utilisation and IBU.
    ///
    /// IBU is load-bearing beyond flavour: iso-alpha-acids inhibit Gram-positive
    /// spoilage bacteria, so a bitter export beer survives voyages a mild beer
    /// cannot. It must be a real, deterministic number for the spoilage system
    /// to lean on.
    /// </summary>
    public static class HopChemistry
    {
        /// <summary>
        /// Isomerisation (utilisation) of alpha acid by boil time, basis points.
        /// ~60 minutes = 24%, falling to ~2% at flame-out. Integer-linear between.
        /// </summary>
        public static int UtilizationBasisPoints(int boilMinutesRemaining)
        {
            int m = boilMinutesRemaining;
            if (m <= 0) return 200;
            if (m >= 60) return 2400;
            return 200 + (2400 - 200) * m / 60;
        }

        /// <summary>
        /// Total IBU in tenths (350 = 35.0 IBU) for a recipe.
        /// mg alpha acid = grams × alpha-acid%, isomerised by utilisation,
        /// divided by batch volume. Integer throughout.
        /// </summary>
        public static int IbuTenths(Recipe recipe, IngredientCatalog catalog, int volumeLitres)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (volumeLitres <= 0) throw new ArgumentOutOfRangeException(nameof(volumeLitres));

            long total = 0;
            foreach (var h in recipe.Hops)
            {
                var ing = catalog.Get(h.IngredientId);
                if (ing.AlphaAcidBasisPoints <= 0) continue;

                // mg alpha acid available.
                long mgAlpha = (long)h.Grams * ing.AlphaAcidBasisPoints / 10000 * 1000;
                // isomerised by boil time.
                long isomerised = mgAlpha * UtilizationBasisPoints(h.BoilMinutesRemaining) / 10000;

                total += isomerised;
            }

            // IBU = mg isomerised alpha acid per litre (times 10 for tenths).
            return (int)(total * 10 / volumeLitres);
        }
    }
}
