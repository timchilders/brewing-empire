using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Sites;

namespace BreweryEmpire.Core.Model.Logistics
{
    /// <summary>Strongly-typed shipment identifier.</summary>
    public readonly struct ShipmentId : IEquatable<ShipmentId>, IComparable<ShipmentId>
    {
        public string Value { get; }
        public ShipmentId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(ShipmentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ShipmentId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(ShipmentId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ShipmentId a, ShipmentId b) => a.Equals(b);
        public static bool operator !=(ShipmentId a, ShipmentId b) => !a.Equals(b);
    }

    /// <summary>
    /// Beer in transit. Advances one day at a time; on arrival it is ready to
    /// deliver. Carries its own ullage loss so a cask shipment arrives lighter
    /// than it left, while bottles arrive intact.
    /// </summary>
    public sealed class Shipment
    {
        public ShipmentId Id { get; set; }
        public BatchId BatchId { get; set; }
        public NodeId From { get; set; }
        public NodeId To { get; set; }
        public PackagingType Packaging { get; set; }
        public int VolumeLitres { get; private set; }
        public int DaysRemaining { get; private set; }
        public Money TransportCost { get; set; }

        /// <summary>How this shipment travels (speed/cost/spoilage trade-off).</summary>
        public TransportMode Mode { get; set; } = TransportMode.HorseCart;

        /// <summary>True if the cargo rides in a refrigerated hold (rail, post-refrigeration).</summary>
        public bool IsRefrigerated { get; set; }

        /// <summary>The beer in transit, as a batch snapshot preserving quality/flavour/style.</summary>
        public Batch? Cargo { get; set; }

        public Shipment() { }

        public Shipment(ShipmentId id, BatchId batchId, NodeId from, NodeId to,
                        int transitDays, PackagingType packaging, int litres)
        {
            if (litres <= 0) throw new ArgumentOutOfRangeException(nameof(litres));
            if (transitDays < 1) throw new ArgumentOutOfRangeException(nameof(transitDays));

            Id = id;
            BatchId = batchId;
            From = from;
            To = to;
            Packaging = packaging;
            VolumeLitres = litres;
            DaysRemaining = transitDays;
        }

        public bool IsArrived => DaysRemaining <= 0;

        /// <summary>Advance one day. Arrives when the counter reaches zero.</summary>
        public void AdvanceDay()
        {
            if (DaysRemaining > 0) DaysRemaining--;
        }

        /// <summary>Apply ullage loss per leg; sealed bottles lose nothing.</summary>
        public void ApplyUllageLoss(PackagingSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.UllageLossBasisPointsPerLeg <= 0) return;

            int loss = VolumeLitres * spec.UllageLossBasisPointsPerLeg / 10000;
            VolumeLitres = Math.Max(0, VolumeLitres - loss);
        }
    }
}
