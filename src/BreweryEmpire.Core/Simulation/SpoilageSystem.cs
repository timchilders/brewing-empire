using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Infection and spoilage.
    ///
    /// THE CENTRAL HISTORICAL TENSION: before germ theory, brewers lost beer
    /// constantly and could not explain why. Risk here is driven by vessel
    /// hygiene and ambient temperature, so the pre-refrigeration summer is
    /// genuinely dangerous and hiring Hansen or Pasteur visibly fixes it.
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

        private static readonly OffFlavor[] InfectionCharacters =
        {
            OffFlavor.Sour, OffFlavor.Phenolic, OffFlavor.Diacetyl, OffFlavor.Acetaldehyde
        };

        /// <summary>Roll infection for every fermenting batch at a node.</summary>
        public static void ProcessNode(GameState state, BreweryNode node)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (node == null) throw new ArgumentNullException(nameof(node));

            foreach (var batch in node.Batches.ToList())
            {
                if (batch.State == BatchState.Spoiled) continue;

                var vessel = node.Vessels.FirstOrDefault(v => v.Id == batch.VesselId);
                int risk = InfectionRiskFor(state, node, vessel);

                if (!state.Random.Chance(risk)) continue;

                var character = InfectionCharacters[state.Random.NextInt(0, InfectionCharacters.Length)];
                int severity = state.Random.NextInt(500, 7000);

                batch.AddInfection(new Infection
                {
                    Character = character,
                    SeverityBasisPoints = severity,
                    Cause = "Ambient " + node.Climate.AmbientTempOn(state.Date) + "C, hygiene " +
                            (vessel?.EffectiveHygieneBasisPoints ?? node.AverageHygieneBasisPoints) + "bp"
                });

                if (batch.State == BatchState.Spoiled)
                {
                    state.Ledger.ForceDebit(state.Date, LedgerCategory.SpoilageWriteOff,
                                            batch.CostOfGoods,
                                            "Batch " + batch.Id + " spoiled (" + character + ")",
                                            node.Id.Value);
                }
            }
        }
    }
}
