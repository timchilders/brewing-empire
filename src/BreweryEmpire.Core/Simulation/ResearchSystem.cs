using System;
using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Research;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// The research loop. Chemists accrue points each day; when the active node's
    /// cost is met it unlocks and its effect is applied. All arithmetic is integer;
    /// nothing here touches the RNG, so research is fully deterministic.
    /// </summary>
    public static class ResearchSystem
    {
        public const int BaseDailyPoints = 1;

        /// <summary>Points accrued per day: a base point plus each chemist's contribution.</summary>
        public static int DailyResearchPoints(GameState state)
        {
            int points = BaseDailyPoints;
            foreach (var s in state.Staff.All)
            {
                if (s.Role != StaffRole.Chemist) continue;
                points += (s.SkillBasisPoints + s.BonusFor(TraitEffect.ResearchRate)) / 1000;
            }
            return points;
        }

        public static bool HasTech(GameState state, string techId) =>
            state.Research.UnlockedTechIds.Contains(techId);

        public static bool CanStart(GameState state, string techId)
        {
            if (state.Research.ActiveTechId != null) return false;
            var tech = TechCatalog.Get(techId);
            if (tech == null) return false;
            if (HasTech(state, techId)) return false;
            if (state.Date.Era < tech.MinEra) return false;
            if (!tech.PrerequisiteIds.All(HasTechState(state))) return false;
            return true;
        }

        private static Func<string, bool> HasTechState(GameState state) =>
            id => state.Research.UnlockedTechIds.Contains(id);

        /// <summary>Begin researching. Returns false (no state change) if invalid.</summary>
        public static bool Start(GameState state, string techId)
        {
            if (!CanStart(state, techId)) return false;
            state.Research.ActiveTechId = techId;
            state.Research.ActiveProgressPoints = 0;
            return true;
        }

        /// <summary>Advance research one day; complete and unlock when the cost is met.</summary>
        public static void ProcessDay(GameState state)
        {
            if (state.Research.ActiveTechId == null) return;
            state.Research.ActiveProgressPoints += DailyResearchPoints(state);

            var tech = TechCatalog.Get(state.Research.ActiveTechId);
            if (tech != null && state.Research.ActiveProgressPoints >= tech.ResearchCostPoints)
                Unlock(state, tech.Id);
        }

        /// <summary>Force-unlock and apply an effect. Used by events and tests; does not
        /// re-check era/prereqs — Start/CanStart are the gated entry points.</summary>
        public static void Unlock(GameState state, string techId)
        {
            if (HasTech(state, techId)) return;
            state.Research.UnlockedTechIds.Add(techId);
            if (state.Research.ActiveTechId == techId) state.Research.ActiveTechId = null;
            ApplyEffect(state, techId);
        }

        public static bool IsStyleUnlocked(GameState state, BeerStyle style)
        {
            var required = TechCatalog.RequiredTechFor(style);
            return required == null || HasTech(state, required);
        }

        public static bool IsPackagingUnlocked(GameState state, PackagingType type) =>
            type == PackagingType.WoodenCask || HasTech(state, "bottling-line");

        /// <summary>Whether a shipment on this mode can ride refrigerated: the mode must be
        /// refrigerated-capable and mechanical refrigeration must be researched.</summary>
        public static bool CanRefrigerateShipment(GameState state, TransportMode mode) =>
            TransportSpec.For(mode).IsRefrigeratedCapable && HasTech(state, "refrigeration");

        /// <summary>Retroactive effects. Flag-only techs (saccharometer, pure-yeast,
        /// pasteurization, water-chemistry, style gates) are consumed directly by the
        /// systems that read HasTech(...); nothing to set here.</summary>
        private static void ApplyEffect(GameState state, string techId)
        {
            switch (techId)
            {
                case "ice-house":
                    foreach (var n in state.World.Nodes) n.HasIceHouse = true;
                    break;
                case "refrigeration":
                    foreach (var n in state.World.Nodes) n.IsRefrigerated = true;
                    break;
                // malting-kilns, saccharometer, drum-roaster, pale-revolution, pure-yeast,
                // pasteurization, water-chemistry, stainless-steel, bottling-line: flag-only.
            }
        }
    }
}
