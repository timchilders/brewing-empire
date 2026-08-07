using System;
using BreweryEmpire.Core.Economy;

namespace BreweryEmpire.Core.Model.Packaging
{
    public enum PackagingType
    {
        WoodenCask = 0,
        Bottle = 1,
        MetalKeg = 2,
        Can = 3
    }

    /// <summary>
    /// Physical properties of a container format.
    ///
    /// WHY OXYGEN AND ULLAGE: casks breathe and leak, which is precisely why
    /// draught beer had to be drunk locally and why bottling opened distant
    /// markets. Modelling ingress and per-leg loss makes the transition from
    /// cask to bottle a strategic unlock rather than a cosmetic one.
    /// </summary>
    public sealed record PackagingSpec
    {
        public PackagingType Type { get; init; }
        public int CapacityLitres { get; init; }
        public Money UnitCost { get; init; }
        public bool IsReturnable { get; init; }
        public int ShelfLifeDays { get; init; }
        public int OxygenIngressBasisPoints { get; init; }
        public int UllageLossBasisPointsPerLeg { get; init; }

        public static PackagingSpec WoodenCask => new PackagingSpec
        {
            Type = PackagingType.WoodenCask,
            CapacityLitres = 163,                  // a hogshead
            UnitCost = Money.FromWhole(3),
            IsReturnable = true,
            ShelfLifeDays = 30,
            OxygenIngressBasisPoints = 300,
            UllageLossBasisPointsPerLeg = 150
        };

        public static PackagingSpec Bottle => new PackagingSpec
        {
            Type = PackagingType.Bottle,
            CapacityLitres = 1,
            UnitCost = Money.FromCents(8),
            IsReturnable = false,
            ShelfLifeDays = 365,
            OxygenIngressBasisPoints = 20,
            UllageLossBasisPointsPerLeg = 0        // sealed: nothing sloshes out
        };

        public static PackagingSpec MetalKeg => new PackagingSpec
        {
            Type = PackagingType.MetalKeg,
            CapacityLitres = 50,
            UnitCost = Money.FromWhole(12),
            IsReturnable = true,
            ShelfLifeDays = 180,
            OxygenIngressBasisPoints = 30,
            UllageLossBasisPointsPerLeg = 20
        };

        public static PackagingSpec Can => new PackagingSpec
        {
            Type = PackagingType.Can,
            CapacityLitres = 1,
            UnitCost = Money.FromCents(5),
            IsReturnable = false,
            ShelfLifeDays = 365,
            OxygenIngressBasisPoints = 10,
            UllageLossBasisPointsPerLeg = 0
        };

        public static PackagingSpec For(PackagingType type)
        {
            switch (type)
            {
                case PackagingType.WoodenCask: return WoodenCask;
                case PackagingType.Bottle: return Bottle;
                case PackagingType.MetalKeg: return MetalKeg;
                case PackagingType.Can: return Can;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
