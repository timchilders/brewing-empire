using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Capital expenditure. Buying a vessel is all-or-nothing: the debit and the
    /// add happen together, so a broke player never ends up with a half-built tank.
    /// </summary>
    public static class BuildSystem
    {
        public static Vessel? TryPurchaseVessel(GameState state, NodeId nodeId,
                                                VesselType type, EquipmentTier tier,
                                                int capacityLitres)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (capacityLitres <= 0) throw new ArgumentOutOfRangeException(nameof(capacityLitres));

            if (!state.World.TryGet(nodeId, out var node)) return null;
            if (node.Type != NodeType.Brewery) return null;
            if (!ResearchSystem.CanBuildTier(state, tier)) return null;

            var vessel = Vessel.Create(state.MintId("vessel"), type, tier, capacityLitres);

            if (!state.Ledger.TryDebit(state.Date, LedgerCategory.CapitalExpenditure,
                                       vessel.PurchaseCost,
                                       "Purchase " + tier + " " + type + " at " + nodeId.Value,
                                       nodeId.Value))
                return null;

            node.AddVessel(vessel);
            return vessel;
        }
    }
}
