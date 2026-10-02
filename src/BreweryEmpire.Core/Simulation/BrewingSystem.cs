using System;
using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>Why a brew could not be started. Beats a bare bool.</summary>
    public enum BrewFailureReason
    {
        None = 0,
        NoAvailableVessel = 1,
        InsufficientIngredients = 2,
        InsufficientDiastaticPower = 3,
        InsufficientFunds = 4,
        UnknownRecipe = 5
    }

    public sealed class BrewResult
    {
        public bool Success { get; set; }
        public BrewFailureReason Reason { get; set; }
        public Batch? Batch { get; set; }

        public static BrewResult Fail(BrewFailureReason reason) =>
            new BrewResult { Success = false, Reason = reason };

        public static BrewResult Ok(Batch batch) =>
            new BrewResult { Success = true, Reason = BrewFailureReason.None, Batch = batch };
    }

    /// <summary>
    /// Starts brews: validates the recipe, consumes ingredients FIFO, computes
    /// quality and flavour, occupies a vessel and books the cost.
    ///
    /// ATOMICITY: every precondition is checked before anything is mutated, so
    /// a failed brew leaves inventory, vessels and the ledger untouched.
    /// </summary>
    public static class BrewingSystem
    {
        public static BrewResult TryStartBrew(GameState state, NodeId nodeId, RecipeId recipeId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (!state.Recipes.TryGetValue(recipeId.Value, out var recipe))
                return BrewResult.Fail(BrewFailureReason.UnknownRecipe);

            var node = state.World.Get(nodeId);

            if (!recipe.HasSufficientDiastaticPower(state.Catalog))
                return BrewResult.Fail(BrewFailureReason.InsufficientDiastaticPower);

            var vessel = node.FindAvailableVessel(VesselType.OpenFermenter, recipe.TargetVolumeLitres);
            if (vessel == null)
                return BrewResult.Fail(BrewFailureReason.NoAvailableVessel);

            // Check every ingredient BEFORE consuming any of them.
            foreach (var g in recipe.Grist)
                if (!node.Inventory.HasAtLeast(g.IngredientId, g.Grams))
                    return BrewResult.Fail(BrewFailureReason.InsufficientIngredients);

            foreach (var h in recipe.Hops)
                if (!node.Inventory.HasAtLeast(h.IngredientId, h.Grams))
                    return BrewResult.Fail(BrewFailureReason.InsufficientIngredients);

            // --- past this point the brew is committed ---

            long cogsCents = 0;
            foreach (var g in recipe.Grist)
                cogsCents += Inventory.CostOfConsumption(
                    node.Inventory.Consume(g.IngredientId, g.Grams)).Cents;

            foreach (var h in recipe.Hops)
                cogsCents += Inventory.CostOfConsumption(
                    node.Inventory.Consume(h.IngredientId, h.Grams)).Cents;

            var batchId = new BatchId(state.MintId("batch"));
            var readyOn = state.Date.AddDays(recipe.TotalDaysToReady);

            var batch = new Batch(batchId, recipeId, nodeId.Value, vessel.Id,
                                  recipe.TargetVolumeLitres, state.Date, readyOn)
            {
                CostOfGoods = Money.FromCents(cogsCents),
                Flavor = ComputeFlavor(state, recipe, node)
            };

            batch.SetQuality(ComputeQuality(state, recipe, node, vessel));

            // Gravity from mash chemistry. Water fit is neutral for now (the
            // recipe carries no target water yet) — Block C wires style water.
            var maltsterBonus = state.Staff.AggregateBonus(node.Id.Value, TraitEffect.MashEfficiency);
            var (og, fg, attenuation, abv) = MashChemistry.ComputeGravity(
                recipe, state.Catalog, vessel.Tier, vessel.ConditionBasisPoints,
                waterFitBasisPoints: 10000, maltsterBonusBasisPoints: maltsterBonus);

            batch.OriginalGravityPoints = og;
            batch.FinalGravityPoints = fg;
            batch.AttenuationBasisPoints = attenuation;
            batch.AbvBasisPoints = abv;
            batch.IbuTenths = HopChemistry.IbuTenths(recipe, state.Catalog, recipe.TargetVolumeLitres);
            batch.SrmLovibond = recipe.EstimatedColorLovibond(state.Catalog);

            vessel.Occupy(batchId, recipe.TotalDaysToReady);
            node.AddBatch(batch);

            return BrewResult.Ok(batch);
        }

        /// <summary>
        /// EXECUTION axis. Brewmaster skill and equipment dominate; water fit
        /// and a seeded roll supply the variance that makes consistency a
        /// genuine achievement.
        /// </summary>
        internal static int ComputeQuality(GameState state, Recipe recipe,
                                           BreweryNode node, Vessel vessel)
        {
            int baseQuality = 3000;

            var brewmaster = state.Staff.BestFor(node.Id.Value, StaffRole.Brewmaster);
            int skill = brewmaster?.SkillBasisPoints ?? 0;
            baseQuality += skill * 3000 / 10000;

            baseQuality += vessel.EffectiveHygieneBasisPoints * 2000 / 10000;

            // Water suited to the style is worth up to 1000bp.
            int waterFit = node.Water.FitScoreBasisPoints(node.Water);
            baseQuality += waterFit * 1000 / 10000;

            baseQuality += state.Staff.AggregateBonus(node.Id.Value, TraitEffect.QualityBonus);

            // Better brewmasters are more consistent, not just better.
            int spread = 1200 - skill * 800 / 10000;
            int rolled = state.Random.Variance(baseQuality, spread);

            return rolled < 0 ? 0 : (rolled > 10000 ? 10000 : rolled);
        }

        /// <summary>
        /// DESIGN axis. Derived purely from recipe decisions and site water —
        /// no randomness, because a recipe should taste like what it is.
        /// </summary>
        internal static FlavorProfile ComputeFlavor(GameState state, Recipe recipe, BreweryNode node)
        {
            int fermentability = recipe.Mash.FermentabilityBasisPoints;

            int bitterness = 0;
            int aroma = 0;
            foreach (var h in recipe.Hops)
            {
                var ing = state.Catalog.Get(h.IngredientId);
                int contribution = ing.AlphaAcidBasisPoints * h.Grams / 1000;
                bitterness += contribution * h.UtilizationBasisPoints / 10000;
                aroma += contribution * h.AromaRetentionBasisPoints / 10000;
            }

            int color = recipe.EstimatedColorLovibond(state.Catalog);

            // Sulfate accentuates bitterness (Burton), chloride accentuates body.
            int sulfateBoost = node.Water.SulfateChlorideRatioBasisPoints > 20000 ? 500 : 0;

            // A less fermentable mash leaves sweetness and body behind.
            int sweetness = 10000 - fermentability;
            int body = (10000 - fermentability) + node.Water.ChloridePpm * 10;

            return new FlavorProfile
            {
                BitternessBasisPoints = Clamp(bitterness + sulfateBoost),
                HopAromaBasisPoints = Clamp(aroma),
                SweetnessBasisPoints = Clamp(sweetness),
                MaltinessBasisPoints = Clamp(color * 20),
                BodyBasisPoints = Clamp(body),
                ColorLovibond = color,
                AlcoholBasisPoints = Clamp(recipe.TotalGristGrams / 40)
            };
        }

        private static int Clamp(int v) => v < 0 ? 0 : (v > 10000 ? 10000 : v);
    }
}
