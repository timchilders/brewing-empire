using System;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Advances fermentation: ticks occupied vessels, applies the ambient
    /// temperature band to the yeast strain, and frees vessels / marks batches
    /// ready exactly on schedule.
    ///
    /// WHY THIS IS ITS OWN SYSTEM: before refrigeration, ambient temperature
    /// dictated what could be brewed and when. Ale yeast ferments clean at
    /// cellar temperature; lager yeast demands cold. A hot summer brew of a
    /// lager, or a warm-weather ale, genuinely suffers — and the player's fix
    /// (cold storage now, Linde refrigeration in Phase 3) lands here.
    /// </summary>
    public static class FermentationSystem
    {
        /// <summary>
        /// Yeast strain's ideal fermentation band (Celsius), inclusive.
        /// Ale tolerates cellar warmth; lager needs cold.
        /// </summary>
        public static (int LowC, int HighC) IdealBandFor(string yeastIngredientId)
        {
            if (string.Equals(yeastIngredientId, "lager-yeast", StringComparison.Ordinal))
                return (7, 13);
            return (15, 24);   // ale and unknown strains
        }

        /// <summary>
        /// Quality penalty (basis points per degree outside the band, applied
        /// daily) when ambient temperature is wrong for the strain. Zero in band.
        /// </summary>
        public static int TemperatureQualityPenaltyBasisPoints(int ambientCelsius, string yeastIngredientId)
        {
            var (low, high) = IdealBandFor(yeastIngredientId);

            int outBy;
            if (ambientCelsius < low) outBy = low - ambientCelsius;
            else if (ambientCelsius > high) outBy = ambientCelsius - high;
            else outBy = 0;

            return outBy * 200;
        }

        /// <summary>Advance every fermenting batch and occupied vessel at a node.</summary>
        public static void ProcessNode(GameState state, BreweryNode node)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));

            int ambient = node.Climate.AmbientTempOn(state.Date);
            bool cold = node.HasColdStorage;

            // 1. Temperature hits fermenting batches daily (cold storage negates it).
            if (!cold)
            {
                foreach (var batch in node.Batches.ToList())
                {
                    if (batch.State != BatchState.Fermenting) continue;
                    int penalty = TemperatureQualityPenaltyBasisPoints(ambient, batch.YeastIngredientId);
                    if (penalty > 0) batch.AdjustQuality(-penalty);
                }
            }

            // 2. Advance vessels; they free themselves at zero days remaining.
            foreach (var vessel in node.Vessels.ToList())
                vessel.AdvanceDay();

            // 3. Batches whose vessel has freed are ready (never the spoiled ones).
            foreach (var batch in node.Batches.ToList())
            {
                if (batch.State != BatchState.Fermenting) continue;
                var vessel = node.Vessels.FirstOrDefault(v => v.Id == batch.VesselId);
                if (vessel != null && vessel.IsAvailable && state.Date >= batch.ReadyOn)
                    batch.MarkReady();
            }
        }
    }
}
