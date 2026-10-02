using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Packaging;

namespace BreweryEmpire.Core.Model.Sites
{
    public enum NodeType
    {
        Brewery = 0,
        Maltings = 1,
        Warehouse = 2,
        Pub = 3,
        Farm = 4
    }

    /// <summary>
    /// A physical site the player owns.
    ///
    /// Water and climate live here rather than on the recipe because location
    /// is the strategic choice: the same recipe brewed in Burton and Pilsen
    /// should not produce the same beer.
    /// </summary>
    public sealed class BreweryNode
    {
        private readonly List<Vessel> _vessels = new List<Vessel>();
        private readonly List<Batch> _batches = new List<Batch>();
        private readonly Dictionary<PackagingType, ContainerPool> _containers =
            new Dictionary<PackagingType, ContainerPool>();

        public NodeId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public RegionId RegionId { get; set; }
        public WaterProfile Water { get; set; } = WaterProfile.London;
        public RegionClimate Climate { get; set; } = RegionClimate.BurtonEngland;
        public Inventory Inventory { get; } = new Inventory();
        public Money DailyOverhead { get; set; }

        /// <summary>
        /// Whether this site can ferment cold (ice house / refrigerated warehouse).
        /// Negates the ambient temperature penalty in FermentationSystem.
        /// </summary>
        public bool HasColdStorage { get; set; }

        public IReadOnlyList<Vessel> Vessels => _vessels;
        public IReadOnlyList<Batch> Batches => _batches;
        public IReadOnlyDictionary<PackagingType, ContainerPool> Containers => _containers;

        public BreweryNode() { }

        public BreweryNode(NodeId id, string name, NodeType type, RegionId regionId,
                           WaterProfile water, RegionClimate climate)
        {
            Id = id;
            Name = name;
            Type = type;
            RegionId = regionId;
            Water = water;
            Climate = climate;
        }

        public void AddVessel(Vessel vessel)
        {
            if (vessel == null) throw new ArgumentNullException(nameof(vessel));
            if (_vessels.Any(v => v.Id == vessel.Id))
                throw new InvalidOperationException("Vessel " + vessel.Id + " is already at this node.");
            _vessels.Add(vessel);
        }

        public void AddBatch(Batch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            _batches.Add(batch);
        }

        public bool RemoveBatch(BatchId id)
        {
            var b = _batches.FirstOrDefault(x => x.Id == id);
            return b != null && _batches.Remove(b);
        }

        public ContainerPool GetOrCreateContainerPool(PackagingType type, Money replacementCost)
        {
            if (!_containers.TryGetValue(type, out var pool))
            {
                pool = new ContainerPool(type, 0, replacementCost);
                _containers[type] = pool;
            }
            return pool;
        }

        /// <summary>
        /// Smallest vessel that fits the volume, so a 10L test brew does not
        /// tie up the 5000L copper. Deterministic tie-break by vessel id.
        /// </summary>
        public Vessel? FindAvailableVessel(VesselType type, int requiredLitres) =>
            _vessels
                .Where(v => v.Type == type && v.IsAvailable && v.CapacityLitres >= requiredLitres)
                .OrderBy(v => v.CapacityLitres)
                .ThenBy(v => v.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();

        public int TotalCapacityLitres => _vessels.Sum(v => v.CapacityLitres);

        public Money TotalDailyUpkeep
        {
            get
            {
                long cents = DailyOverhead.Cents;
                foreach (var v in _vessels) cents += v.DailyUpkeep.Cents;
                return Money.FromCents(cents);
            }
        }

        /// <summary>Average vessel hygiene — drives infection risk at this site.</summary>
        public int AverageHygieneBasisPoints
        {
            get
            {
                if (_vessels.Count == 0) return 0;
                long sum = _vessels.Sum(v => (long)v.EffectiveHygieneBasisPoints);
                return (int)(sum / _vessels.Count);
            }
        }

        public void AdvanceVesselsOneDay()
        {
            foreach (var v in _vessels) v.AdvanceDay();
        }
    }

    /// <summary>
    /// The owned world: nodes plus the routes between them.
    ///
    /// Distance drives transport cost and transit spoilage, which is what
    /// makes geography a real constraint rather than a label.
    /// </summary>
    public sealed class WorldMap
    {
        private readonly Dictionary<string, BreweryNode> _nodes =
            new Dictionary<string, BreweryNode>(StringComparer.Ordinal);

        private readonly Dictionary<string, int> _routeDistancesKm =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public IEnumerable<BreweryNode> Nodes => _nodes.Values.OrderBy(n => n.Id.Value, StringComparer.Ordinal);

        public int NodeCount => _nodes.Count;

        public void AddNode(BreweryNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodes.ContainsKey(node.Id.Value))
                throw new InvalidOperationException("Node " + node.Id + " already exists.");
            _nodes[node.Id.Value] = node;
        }

        public BreweryNode Get(NodeId id) =>
            _nodes.TryGetValue(id.Value, out var n)
                ? n
                : throw new KeyNotFoundException("Unknown node: " + id);

        public bool TryGet(NodeId id, out BreweryNode node) => _nodes.TryGetValue(id.Value, out node!);

        private static string RouteKey(NodeId a, NodeId b) =>
            string.CompareOrdinal(a.Value, b.Value) <= 0
                ? a.Value + "|" + b.Value
                : b.Value + "|" + a.Value;

        /// <summary>Routes are symmetric: distance from A to B equals B to A.</summary>
        public void SetRoute(NodeId a, NodeId b, int distanceKm)
        {
            if (distanceKm < 0) throw new ArgumentOutOfRangeException(nameof(distanceKm));
            if (a == b) throw new ArgumentException("A node cannot have a route to itself.");
            _routeDistancesKm[RouteKey(a, b)] = distanceKm;
        }

        public bool HasRoute(NodeId a, NodeId b) => _routeDistancesKm.ContainsKey(RouteKey(a, b));

        public int DistanceKm(NodeId a, NodeId b) =>
            _routeDistancesKm.TryGetValue(RouteKey(a, b), out var d)
                ? d
                : throw new KeyNotFoundException("No route between " + a + " and " + b + ".");
    }
}
