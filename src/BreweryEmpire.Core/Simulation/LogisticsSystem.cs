using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Sites;
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
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var node = state.World.Get(from);
            var batch = node.Batches.FirstOrDefault(b => b.Id == batchId);
            if (batch == null || batch.VolumeLitres < litres) return null;

            var spec = TransportSpec.For(TransportMode.HorseCart);
            var route = new Route
            {
                From = from, To = to, DistanceKm = distanceKm, Mode = TransportMode.HorseCart
            };

            var shipmentId = new ShipmentId(state.MintId("shipment"));
            var shipment = new Shipment(shipmentId, batchId, from, to,
                                        route.TransitDays, packaging, litres)
            {
                TransportCost = Money.FromCents((long)route.CostPerLitre.Cents * litres)
            };

            batch.Remove(litres);
            state.Ledger.ForceDebit(state.Date, LedgerCategory.TransportCost,
                                    shipment.TransportCost,
                                    "Shipment " + shipmentId + " " + from + " -> " + to,
                                    from.Value);

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
