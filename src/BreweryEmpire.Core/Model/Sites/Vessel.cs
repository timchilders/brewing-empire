using System;
using BreweryEmpire.Core.Economy;

namespace BreweryEmpire.Core.Model.Sites
{
    public enum VesselType
    {
        MashTun = 0,
        Copper = 1,
        OpenFermenter = 2,
        ClosedFermenter = 3,
        ConditioningTank = 4,
        StainlessTank = 5
    }

    /// <summary>
    /// Material technology tier. Ordering is meaningful: higher tiers are
    /// strictly more hygienic, which is the core of the pre/post-germ-theory
    /// progression.
    /// </summary>
    public enum EquipmentTier
    {
        Wooden = 0,
        Copper = 1,
        Iron = 2,
        Stainless = 3
    }

    /// <summary>
    /// A physical brewing vessel. Occupancy is tracked with a simple day
    /// counter rather than a stage timeline: the vessel only needs to know
    /// that it is busy and for how long, which keeps scheduling trivial and
    /// leaves brewing detail to the batch itself.
    /// </summary>
    public sealed class Vessel
    {
        public VesselId Id { get; set; }
        public VesselType Type { get; set; }
        public EquipmentTier Tier { get; set; }
        public int CapacityLitres { get; set; }
        public Money PurchaseCost { get; set; }
        public Money DailyUpkeep { get; set; }
        public int HygieneBasisPoints { get; set; }
        public int ConditionBasisPoints { get; private set; } = 10000;
        public BatchId? OccupiedBy { get; private set; }
        public int DaysRemaining { get; private set; }

        public Vessel() { }

        public Vessel(VesselId id, VesselType type, EquipmentTier tier, int capacityLitres,
                      Money purchaseCost, Money dailyUpkeep)
        {
            if (capacityLitres <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacityLitres),
                    capacityLitres, "Capacity must be positive.");

            Id = id;
            Type = type;
            Tier = tier;
            CapacityLitres = capacityLitres;
            PurchaseCost = purchaseCost;
            DailyUpkeep = dailyUpkeep;
            HygieneBasisPoints = BaseHygieneFor(tier);
        }

        public bool IsAvailable => OccupiedBy == null;

        /// <summary>
        /// Baseline cleanliness by material. Wood harbours bacteria in its
        /// grain and can never be truly sanitised; stainless can. This single
        /// curve is what makes equipment upgrades feel worthwhile.
        /// </summary>
        public static int BaseHygieneFor(EquipmentTier tier)
        {
            switch (tier)
            {
                case EquipmentTier.Wooden: return 4000;
                case EquipmentTier.Copper: return 6000;
                case EquipmentTier.Iron: return 7000;
                case EquipmentTier.Stainless: return 9500;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>Hygiene actually achieved, degraded by wear.</summary>
        public int EffectiveHygieneBasisPoints => HygieneBasisPoints * ConditionBasisPoints / 10000;

        public void Occupy(BatchId batch, int days)
        {
            if (days < 0)
                throw new ArgumentOutOfRangeException(nameof(days), days, "Days must not be negative.");
            if (!IsAvailable)
                throw new InvalidOperationException(
                    "Vessel " + Id + " is already occupied by batch " + OccupiedBy + ".");

            OccupiedBy = batch;
            DaysRemaining = days;
        }

        /// <summary>
        /// Tick one day. Frees the vessel exactly once when the counter runs
        /// out, and is a safe no-op when already free — the day loop should
        /// never have to special-case vessel state.
        /// </summary>
        public void AdvanceDay()
        {
            if (IsAvailable) return;

            if (DaysRemaining > 0) DaysRemaining--;

            if (DaysRemaining == 0)
            {
                OccupiedBy = null;
            }
        }

        /// <summary>Force-release, e.g. when a batch is dumped early.</summary>
        public void Release()
        {
            OccupiedBy = null;
            DaysRemaining = 0;
        }

        public void DegradeCondition(int basisPoints)
        {
            if (basisPoints < 0) throw new ArgumentOutOfRangeException(nameof(basisPoints));
            ConditionBasisPoints = Math.Max(0, ConditionBasisPoints - basisPoints);
        }

        public void Maintain(int basisPoints)
        {
            if (basisPoints < 0) throw new ArgumentOutOfRangeException(nameof(basisPoints));
            ConditionBasisPoints = Math.Min(10000, ConditionBasisPoints + basisPoints);
        }

        /// <summary>Typical vessel for a tier, with era-appropriate cost and upkeep.</summary>
        public static Vessel Create(string id, VesselType type, EquipmentTier tier, int capacityLitres)
        {
            Money cost;
            Money upkeep;

            switch (tier)
            {
                case EquipmentTier.Wooden:
                    cost = Money.FromWhole(capacityLitres / 4);
                    upkeep = Money.FromCents(capacityLitres * 2);
                    break;
                case EquipmentTier.Copper:
                    cost = Money.FromWhole(capacityLitres / 2);
                    upkeep = Money.FromCents(capacityLitres * 3);
                    break;
                case EquipmentTier.Iron:
                    cost = Money.FromWhole(capacityLitres * 3 / 4);
                    upkeep = Money.FromCents(capacityLitres * 3);
                    break;
                case EquipmentTier.Stainless:
                    cost = Money.FromWhole(capacityLitres);
                    upkeep = Money.FromCents(capacityLitres * 2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier));
            }

            return new Vessel(new VesselId(id), type, tier, capacityLitres, cost, upkeep);
        }
    }
}
