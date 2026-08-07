using System;

namespace BreweryEmpire.Core.Model
{
    /// <summary>Strongly-typed node identifier. Prevents id mix-ups as the graph grows.</summary>
    public readonly struct NodeId : IEquatable<NodeId>, IComparable<NodeId>
    {
        public string Value { get; }
        public NodeId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(NodeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is NodeId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(NodeId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(NodeId a, NodeId b) => a.Equals(b);
        public static bool operator !=(NodeId a, NodeId b) => !a.Equals(b);
    }

    public readonly struct RegionId : IEquatable<RegionId>, IComparable<RegionId>
    {
        public string Value { get; }
        public RegionId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(RegionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RegionId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(RegionId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(RegionId a, RegionId b) => a.Equals(b);
        public static bool operator !=(RegionId a, RegionId b) => !a.Equals(b);
    }

    public readonly struct LotId : IEquatable<LotId>, IComparable<LotId>
    {
        public string Value { get; }
        public LotId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(LotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is LotId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(LotId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(LotId a, LotId b) => a.Equals(b);
        public static bool operator !=(LotId a, LotId b) => !a.Equals(b);
    }

    public readonly struct BatchId : IEquatable<BatchId>, IComparable<BatchId>
    {
        public string Value { get; }
        public BatchId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(BatchId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is BatchId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(BatchId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(BatchId a, BatchId b) => a.Equals(b);
        public static bool operator !=(BatchId a, BatchId b) => !a.Equals(b);
    }

    public readonly struct VesselId : IEquatable<VesselId>, IComparable<VesselId>
    {
        public string Value { get; }
        public VesselId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(VesselId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is VesselId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(VesselId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VesselId a, VesselId b) => a.Equals(b);
        public static bool operator !=(VesselId a, VesselId b) => !a.Equals(b);
    }

    public readonly struct StaffId : IEquatable<StaffId>, IComparable<StaffId>
    {
        public string Value { get; }
        public StaffId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(StaffId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StaffId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(StaffId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(StaffId a, StaffId b) => a.Equals(b);
        public static bool operator !=(StaffId a, StaffId b) => !a.Equals(b);
    }

    public readonly struct RecipeId : IEquatable<RecipeId>, IComparable<RecipeId>
    {
        public string Value { get; }
        public RecipeId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(RecipeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RecipeId o && Equals(o);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public int CompareTo(RecipeId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(RecipeId a, RecipeId b) => a.Equals(b);
        public static bool operator !=(RecipeId a, RecipeId b) => !a.Equals(b);
    }
}
