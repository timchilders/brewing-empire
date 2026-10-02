using System;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Sites;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Integer brewing chemistry: mash efficiency, gravity and ABV.
    ///
    /// Everything here is pure and integer so a given recipe + equipment + site
    /// always yields the same numbers on every platform. The tuning will be
    /// wrong until a balance pass, but the relationships are the real product:
    /// stainless beats wood, enzymes beat kilned grain, soft mash beats hard.
    /// </summary>
    public static class MashChemistry
    {
        /// <summary>Lintner at which a grist converts fully. Below this, efficiency scales down.</summary>
        public const int FullConversionLintner = 120;

        /// <summary>Base extract efficiency of each equipment tier (basis points).</summary>
        public static int TierEfficiencyBasisPoints(EquipmentTier tier)
        {
            switch (tier)
            {
                case EquipmentTier.Wooden: return 7000;
                case EquipmentTier.Copper: return 8000;
                case EquipmentTier.Iron: return 8500;
                case EquipmentTier.Stainless: return 9200;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>
        /// How close the grist's enzymes come to full conversion.
        /// 10000 = full, scales down with low diastatic power.
        /// </summary>
        public static int DiastaticSufficiencyBasisPoints(int weightedDiastaticPowerLintner)
        {
            if (weightedDiastaticPowerLintner >= FullConversionLintner) return 10000;
            if (weightedDiastaticPowerLintner <= 0) return 0;
            return weightedDiastaticPowerLintner * 10000 / FullConversionLintner;
        }

        /// <summary>
        /// Mash efficiency in basis points. The product of every place a brewery
        /// can win or lose extract: equipment, vessel condition, enzymes, water
        /// chemistry and the maltster's skill.
        /// </summary>
        public static int MashEfficiencyBasisPoints(
            EquipmentTier tier,
            int conditionBasisPoints,
            int weightedDiastaticPowerLintner,
            int waterFitBasisPoints,
            int maltsterBonusBasisPoints)
        {
            if (conditionBasisPoints < 0 || conditionBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(conditionBasisPoints));

            long efficiency = TierEfficiencyBasisPoints(tier);

            efficiency = efficiency * conditionBasisPoints / 10000;
            efficiency = efficiency * DiastaticSufficiencyBasisPoints(weightedDiastaticPowerLintner) / 10000;
            efficiency = efficiency * Math.Max(0, waterFitBasisPoints) / 10000;
            efficiency = efficiency * Math.Max(0, 10000 + maltsterBonusBasisPoints) / 10000;

            return (int)Math.Clamp(efficiency, 0, 10000);
        }

        /// <summary>
        /// Apparent attenuation from mash temperature. A cool beta-rest mash
        /// (63C) is dry and ferments almost fully; a hot alpha-rest mash (72C)
        /// leaves dextrins and ferments less.
        /// </summary>
        public static int AttenuationBasisPoints(int mashFermentabilityBasisPoints)
        {
            // Map fermentability (5500 sweet .. 9000 dry) to attenuation (5500 .. 8000).
            int f = Math.Clamp(mashFermentabilityBasisPoints, 5500, 9000);
            return 5500 + (f - 5500) * 2500 / 3500;
        }

        /// <summary>Original gravity points (50 = 1.050) from target and efficiency.</summary>
        public static int OriginalGravityPoints(int targetGravityPoints, int efficiencyBasisPoints)
        {
            if (targetGravityPoints < 0) throw new ArgumentOutOfRangeException(nameof(targetGravityPoints));
            return targetGravityPoints * Math.Max(0, efficiencyBasisPoints) / 10000;
        }

        /// <summary>Final gravity points (10 = 1.010) left behind by unfermented sugar.</summary>
        public static int FinalGravityPoints(int originalGravityPoints, int attenuationBasisPoints)
        {
            if (originalGravityPoints < 0) throw new ArgumentOutOfRangeException(nameof(originalGravityPoints));
            int a = Math.Clamp(attenuationBasisPoints, 0, 10000);
            return originalGravityPoints * (10000 - a) / 10000;
        }

        /// <summary>
        /// ABV in basis points from the gravity drop. Standard brewer's rule:
        /// ABV% ≈ (OG − FG) × 131.25, with gravity in points (50 = 1.050).
        /// Basis points are percent × 100, hence the divide by 10.
        /// </summary>
        public static int AbvBasisPoints(int originalGravityPoints, int finalGravityPoints)
        {
            int drop = originalGravityPoints - finalGravityPoints;
            if (drop <= 0) return 0;
            return drop * 131 / 10;
        }

        /// <summary>Compute the full gravity picture for a recipe on a site.</summary>
        public static (int originalGravity, int finalGravity, int attenuation, int abv) ComputeGravity(
            Recipe recipe, IngredientCatalog catalog, EquipmentTier tier,
            int conditionBasisPoints, int waterFitBasisPoints, int maltsterBonusBasisPoints)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            int dp = recipe.WeightedDiastaticPower(catalog);
            int efficiency = MashEfficiencyBasisPoints(
                tier, conditionBasisPoints, dp, waterFitBasisPoints, maltsterBonusBasisPoints);

            int og = OriginalGravityPoints(recipe.TargetOriginalGravityPoints, efficiency);
            int attenuation = AttenuationBasisPoints(recipe.Mash.FermentabilityBasisPoints);
            int fg = FinalGravityPoints(og, attenuation);
            int abv = AbvBasisPoints(og, fg);

            return (og, fg, attenuation, abv);
        }
    }
}
