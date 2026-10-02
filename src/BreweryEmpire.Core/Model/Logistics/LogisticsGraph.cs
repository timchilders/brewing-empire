using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Sites;

namespace BreweryEmpire.Core.Model.Logistics
{
    /// <summary>
    /// How beer moves. Each mode trades speed against cost, and the slower,
    /// warmer modes spoil beer in transit — which is exactly why refrigerated
    /// rail was a revolution.
    /// </summary>
    public enum TransportMode
    {
        HorseCart = 0,
        Canal = 1,
        SteamRail = 2,
        CargoShip = 3
    }

    /// <summary>Static properties of a transport mode.</summary>
    public sealed record TransportSpec
    {
        public TransportMode Mode { get; init; }
        public int DaysPer100Km { get; init; }
        public Money CostPerLitrePer100Km { get; init; }
        public int SpoilageModifierBasisPoints { get; init; }
        public bool IsRefrigeratedCapable { get; init; }

        public static TransportSpec For(TransportMode mode)
        {
            switch (mode)
            {
                case TransportMode.HorseCart:
                    return new TransportSpec
                    {
                        Mode = mode, DaysPer100Km = 4, CostPerLitrePer100Km = Money.FromCents(12),
                        SpoilageModifierBasisPoints = 10000, IsRefrigeratedCapable = false
                    };
                case TransportMode.Canal:
                    return new TransportSpec
                    {
                        Mode = mode, DaysPer100Km = 3, CostPerLitrePer100Km = Money.FromCents(4),
                        SpoilageModifierBasisPoints = 10000, IsRefrigeratedCapable = false
                    };
                case TransportMode.SteamRail:
                    return new TransportSpec
                    {
                        Mode = mode, DaysPer100Km = 1, CostPerLitrePer100Km = Money.FromCents(20),
                        SpoilageModifierBasisPoints = 7000, IsRefrigeratedCapable = true
                    };
                case TransportMode.CargoShip:
                    return new TransportSpec
                    {
                        Mode = mode, DaysPer100Km = 2, CostPerLitrePer100Km = Money.FromCents(2),
                        SpoilageModifierBasisPoints = 13000, IsRefrigeratedCapable = false
                    };
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }

    /// <summary>A directed edge in the logistics graph.</summary>
    public sealed record Route
    {
        public NodeId From { get; init; }
        public NodeId To { get; init; }
        public int DistanceKm { get; init; }
        public TransportMode Mode { get; init; }

        public int TransitDays =>
            Math.Max(1, DistanceKm * TransportSpec.For(Mode).DaysPer100Km / 100);

        public Money CostPerLitre =>
            Money.FromCents((long)DistanceKm * TransportSpec.For(Mode).CostPerLitrePer100Km.Cents / 100);
    }

    /// <summary>A resolved path through the graph.</summary>
    public sealed class Path
    {
        public IReadOnlyList<NodeId> Nodes { get; init; } = Array.Empty<NodeId>();
        public IReadOnlyList<Route> Legs { get; init; } = Array.Empty<Route>();

        public int TotalDistanceKm => Legs.Sum(l => l.DistanceKm);
        public int TotalTransitDays => Legs.Sum(l => l.TransitDays);
        public Money TotalCostPerLitre => Money.FromCents(Legs.Sum(l => l.CostPerLitre.Cents));
    }

    /// <summary>
    /// The transport network. Routes are directed but added symmetrically by
    /// default (a canal works both ways). Dijkstra for cheapest/fastest paths,
    /// with deterministic tie-breaking by node id so replays never diverge.
    /// </summary>
    public sealed class LogisticsGraph
    {
        private readonly HashSet<string> _nodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Route> _routes = new Dictionary<string, Route>(StringComparer.Ordinal);

        private static string Key(NodeId from, NodeId to) => from.Value + "|" + to.Value;

        public void AddNode(NodeId node) => _nodes.Add(node.Value);

        public void AddRoute(NodeId from, NodeId to, int distanceKm, TransportMode mode)
        {
            if (!_nodes.Contains(from.Value) || !_nodes.Contains(to.Value))
                throw new KeyNotFoundException("Route references an unknown node.");

            if (_routes.ContainsKey(Key(from, to)))
                throw new InvalidOperationException("Route " + from + " -> " + to + " already exists.");

            _routes[Key(from, to)] = new Route { From = from, To = to, DistanceKm = distanceKm, Mode = mode };
        }

        /// <summary>Dijkstra by total transit days (fastest).</summary>
        public Path? FindFastestPath(NodeId from, NodeId to) =>
            FindPath(from, to, r => r.TotalTransitDays);

        /// <summary>Dijkstra by total cost per litre (cheapest).</summary>
        public Path? FindCheapestPath(NodeId from, NodeId to) =>
            FindPath(from, to, r => r.TotalCostPerLitre.Cents);

        private Path? FindPath(NodeId from, NodeId to, Func<Path, long> metric)
        {
            if (!_nodes.Contains(from.Value) || !_nodes.Contains(to.Value)) return null;
            if (from == to) return null;

            var bestCost = new Dictionary<string, long>(StringComparer.Ordinal);
            var prevRoute = new Dictionary<string, Route>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (var n in _nodes) bestCost[n] = long.MaxValue;
            bestCost[from.Value] = 0;

            // Deterministic exploration order: always pop the smallest id among tied nodes.
            var queue = new SortedSet<(long cost, string node)>();
            queue.Add((0, from.Value));

            while (queue.Count > 0)
            {
                var (cost, node) = queue.Min;
                queue.Remove(queue.Min);
                if (!visited.Add(node)) continue;

                foreach (var route in _routes.Values.Where(r => r.From.Value == node))
                {
                    long newCost = cost + metric(new Path { Legs = new[] { route } });
                    if (newCost < bestCost[route.To.Value])
                    {
                        bestCost[route.To.Value] = newCost;
                        prevRoute[route.To.Value] = route;
                        queue.Add((newCost, route.To.Value));
                    }
                }
            }

            if (bestCost[to.Value] == long.MaxValue) return null;

            var legs = new List<Route>();
            var nodes = new List<NodeId> { to };
            var cursor = to;
            while (prevRoute.TryGetValue(cursor.Value, out var leg))
            {
                legs.Insert(0, leg);
                nodes.Insert(0, leg.From);
                cursor = leg.From;
            }

            return new Path { Legs = legs, Nodes = nodes };
        }
    }
}
