using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Moves beer forward and returns empties backward.
    ///
    /// Dispatch debits transport cost, creates a shipment, and draws beer off
    /// the batch. Each tick advances shipments; on arrival the beer is handed
    /// to the destination market's adjacent node. Returnable containers
    /// eventually come home minus a seeded loss rate (reduced by coopers).
    /// </summary>
    public static class LogisticsSystem
    {
        /// <summary>Dispatch a shipment. Returns null if the batch has insufficient beer.</summary>
        public static Shipment? DispatchShipment(GameState state, NodeId from, NodeId to,
                                                BatchId batchId, int litres,
                                                PackagingType packaging, int distanceKm)
            => DispatchShipment(state, from, to, batchId, litres, packaging,
                                TransportMode.HorseCart, distanceKm);

        /// <summary>Dispatch a shipment by an explicit mode. Preserves the beer's quality/flavour
        /// as cargo; charges the mode's real per-litre cost.</summary>
        public static Shipment? DispatchShipment(GameState state, NodeId from, NodeId to,
                                                BatchId batchId, int litres,
                                                PackagingType packaging, TransportMode mode,
                                                int distanceKm, bool refrigerated = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (!ResearchSystem.IsPackagingUnlocked(state, packaging)) return null;

            var node = state.World.Get(from);
            var batch = node.Batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null || batch.VolumeLitres < litres) return null;

            // Prefer the registered route's distance and mode; the arguments are a fallback.
            int distance = distanceKm;
            if (state.World.TryGetRoute(from, to, out var route))
            {
                distance = route.DistanceKm;
                if (route.Mode != null) mode = route.Mode.Value;
            }

            var spec = TransportSpec.For(mode);
            var routeForCost = new Route { From = from, To = to, DistanceKm = distance, Mode = mode };

            // Draymen speed transit.
            int draymanBonus = state.Staff.AggregateBonus(from.Value, TraitEffect.TransitSpeed);
            int transitDays = routeForCost.TransitDays;
            if (draymanBonus > 0)
                transitDays = Math.Max(1, transitDays * (10000 - draymanBonus) / 10000);

            // Refrigeration only on a capable mode, gated by the refrigeration tech.
            bool isRefrigerated = refrigerated && ResearchSystem.CanRefrigerateShipment(state, mode);

            var cargo = new Batch(new BatchId(state.MintId("batch")), batch.RecipeId,
                                  batch.NodeId, batch.VesselId, litres, state.Date, state.Date)
            {
                Style = batch.Style,
                IsPasteurized = batch.IsPasteurized,
                OriginalGravityPoints = batch.OriginalGravityPoints,
                FinalGravityPoints = batch.FinalGravityPoints,
                AttenuationBasisPoints = batch.AttenuationBasisPoints,
                AbvBasisPoints = batch.AbvBasisPoints,
                IbuTenths = batch.IbuTenths,
                SrmLovibond = batch.SrmLovibond,
                YeastIngredientId = batch.YeastIngredientId,
                YeastGeneration = batch.YeastGeneration,
                Flavor = batch.Flavor,
                CostOfGoods = batch.CostPerLitre * litres
            };
            cargo.SetQuality(batch.QualityBasisPoints);
            cargo.MarkReady();
            foreach (var inf in batch.Infections) cargo.RestoreInfection(inf);

            var shipmentId = new ShipmentId(state.MintId("shipment"));
            var shipment = new Shipment(shipmentId, batchId, from, to,
                                        transitDays, packaging, litres)
            {
                Mode = mode,
                IsRefrigerated = isRefrigerated,
                Cargo = cargo,
                TransportCost = Money.FromCents((long)routeForCost.CostPerLitre.Cents * litres)
            };

            batch.Remove(litres);
            state.Ledger.ForceDebit(state.Date, LedgerCategory.TransportCost,
                                    shipment.TransportCost,
                                    "Shipment " + shipmentId + " " + from + " -> " + to + " (" + mode + ")",
                                    from.Value);

            state.Shipments.Add(shipment);
            return shipment;
        }

        /// <summary>Advance every shipment in the game one day.</summary>
        public static void ProcessShipments(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            foreach (var shipment in state.Shipments.ToList())
            {
                shipment.AdvanceDay();

                if (shipment.IsArrived)
                {
                    // Hand the beer to the destination node's market.
                    Deliver(state, shipment);
                    state.Shipments.Remove(shipment);
                }
            }
        }

        private static void Deliver(GameState state, Shipment shipment)
        {
            // Beer reappears at the destination node as a ready batch.
            if (!state.World.TryGet(shipment.To, out var dest)) return;

            var batch = new Batch(new BatchId(state.MintId("batch")),
                                  new RecipeId("shipped"), shipment.To.Value,
                                  new VesselId("none"), shipment.VolumeLitres,
                                  state.Date, state.Date)
            {
                IsPasteurized = false
            };
            batch.MarkReady();
            dest.AddBatch(batch);
        }
    }
}
