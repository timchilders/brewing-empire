using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Infection and spoilage — the two-stage model.
    ///
    /// Stage 1 is an infection ROLL at vulnerable moments (open fermentation,
    /// warm transit, high-oxygen packaging), driven by vessel hygiene, tier,
    /// brewmaster resistance and pasteurization. Stage 2 is PROGRESSION each
    /// tick, driven by temperature, ABV, IBU and the cold chain. The defenses
    /// are all historically real and already modelled: hops inhibit Gram-positive
    /// bacteria, alcohol resists everything, cold slows, pasteurization nearly
    /// immunises.
    /// </summary>
    public static class SpoilageSystem
    {
        /// <summary>
        /// Warm beer spoils fast; cold beer keeps. Basis points per day.
        ///
        /// Calibrated to make pre-refrigeration summer brewing genuinely
        /// dangerous rather than a nuisance: Bavaria banned brewing between
        /// April and September in 1553 precisely because warm-weather batches
        /// were so unreliable. In an open wooden fermenter at 17C this works
        /// out near a 1-in-3 chance of infection across a three-week batch,
        /// which is what makes cold cellars, ice and later refrigeration feel
        /// like genuine unlocks instead of stat bumps.
        /// </summary>
        internal static int BaseInfectionRiskBasisPoints(int ambientTempCelsius)
        {
            if (ambientTempCelsius <= 5) return 10;
            if (ambientTempCelsius <= 12) return 60;
            if (ambientTempCelsius <= 18) return 250;
            if (ambientTempCelsius <= 24) return 600;
            return 1000;
        }

        /// <summary>
        /// Daily infection chance for one batch, after hygiene and staff
        /// mitigation. Never negative.
        /// </summary>
        internal static int InfectionRiskFor(GameState state, BreweryNode node, Vessel? vessel)
        {
            int ambient = node.Climate.AmbientTempOn(state.Date);
            int risk = BaseInfectionRiskBasisPoints(ambient);

            // Hygiene scales risk down: 10000bp hygiene removes 90% of it.
            int hygiene = vessel?.EffectiveHygieneBasisPoints ?? node.AverageHygieneBasisPoints;
            risk = risk * (10000 - hygiene * 9000 / 10000) / 10000;

            // Chemists (Hansen, Pasteur) cut what remains.
            int resistance = state.Staff.AggregateBonus(node.Id.Value, TraitEffect.InfectionResistance);
            risk = risk * Math.Max(0, 10000 - resistance) / 10000;

            return Math.Max(0, risk);
        }

        /// <summary>
        /// Base progression rate once infected, basis points per day. This is
        /// deliberately far faster than the daily *risk* curve: a contracted
        /// infection ruins beer in days, not weeks — which is why contaminated
        /// summer batches could not be saved.
        /// </summary>
        internal static int BaseProgressionRateBasisPoints(int ambientTempCelsius)
        {
            if (ambientTempCelsius <= 5) return 400;
            if (ambientTempCelsius <= 12) return 1000;
            if (ambientTempCelsius <= 18) return 2500;
            if (ambientTempCelsius <= 24) return 5000;
            return 8000;
        }

        /// <summary>
        /// Defence (basis points) a batch mounts against an organism.
        /// Hops inhibit Gram-positive bacteria (Lacto/Pedio); alcohol resists
        /// everything. Defence caps at 7000bp so spoilage never becomes
        /// mathematically impossible outside pasteurization.
        /// </summary>
        public static int DefenseBasisPoints(SpoilageOrganism organism, int ibuTenths, int abvBasisPoints)
        {
            int ibuDefense = 0;
            if (organism == SpoilageOrganism.Lactobacillus || organism == SpoilageOrganism.Pediococcus)
                ibuDefense = Math.Min(4000, ibuTenths * 8);

            int abvDefense = Math.Min(3000, Math.Max(0, abvBasisPoints - 300) * 6);

            return Math.Min(7000, ibuDefense + abvDefense);
        }

        /// <summary>
        /// Daily progression (basis points) of an infection, after the batch's
        /// defenses and the environment. Pasteurized beer progresses at a token
        /// 2% rate; cold storage cuts ambient rate by 60%.
        /// </summary>
        public static int ProgressionRateBasisPoints(
            SpoilageOrganism organism, int ambientC, int ibuTenths,
            int abvBasisPoints, bool cold, bool pasteurized)
        {
            if (pasteurized) return 20;   // near-immunity, but never absolute

            int baseRate = BaseProgressionRateBasisPoints(ambientC);
            int defense = DefenseBasisPoints(organism, ibuTenths, abvBasisPoints);

            int rate = baseRate * Math.Max(0, 10000 - defense) / 10000;

            if (cold) rate = rate * 4000 / 10000;

            return Math.Max(0, rate);
        }

        /// <summary>Whether a sour organism on this style is the point, not a defect.</summary>
        public static bool IsIntentionalSour(BeerStyle style, SpoilageOrganism organism) =>
            SpoilageModel.IsIntentionalSour(style, organism);

        /// <summary>Advance spoilage for every batch at a node.</summary>
        public static void ProcessNode(GameState state, BreweryNode node)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));

            int ambient = node.Climate.AmbientTempOn(state.Date);
            bool cold = node.HasColdStorage;

            foreach (var batch in node.Batches.ToList())
            {
                if (batch.State == BatchState.Spoiled) continue;

                var vessel = node.Vessels.FirstOrDefault(v => v.Id == batch.VesselId);
                bool hasActiveInfection = batch.Infections.Any(i =>
                    !i.IsIntentionalSour && i.ProgressionBasisPoints < 10000);

                if (!hasActiveInfection)
                {
                    // Stage 1: contract?
                    int risk = InfectionRiskFor(state, node, vessel);
                    if (risk > 0 && state.Random.Chance(risk))
                    {
                        var organism = (SpoilageOrganism)state.Random.NextInt(0, 5);
                        batch.ContractInfection(organism,
                            "Ambient " + ambient + "C, hygiene " +
                            (vessel?.EffectiveHygieneBasisPoints ?? node.AverageHygieneBasisPoints) + "bp");
                    }
                }
                else
                {
                    // Stage 2: progress toward ruin.
                    int rate = ProgressionRateBasisPoints(
                        ActiveOrganism(batch), ambient, batch.IbuTenths,
                        batch.AbvBasisPoints, cold, batch.IsPasteurized);

                    if (batch.AdvanceInfection(rate))
                        WriteOff(state, node, batch);
                }
            }
        }

        private static SpoilageOrganism ActiveOrganism(Batch batch) =>
            batch.Infections
                .Where(i => !i.IsIntentionalSour && i.ProgressionBasisPoints < 10000)
                .OrderByDescending(i => i.ProgressionBasisPoints)
                .Select(i => i.Organism)
                .FirstOrDefault();

        /// <summary>Write a spoiled batch's COGS off to the ledger once.</summary>
        public static void WriteOff(GameState state, BreweryNode node, Batch batch)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            var organism = batch.Infections
                .OrderByDescending(i => i.ProgressionBasisPoints)
                .FirstOrDefault()?.Organism ?? SpoilageOrganism.Lactobacillus;

            state.Ledger.ForceDebit(state.Date, LedgerCategory.SpoilageWriteOff,
                                    batch.CostOfGoods,
                                    "Batch " + batch.Id + " spoiled (" + organism + ")",
                                    node.Id.Value);
        }
    }
}
