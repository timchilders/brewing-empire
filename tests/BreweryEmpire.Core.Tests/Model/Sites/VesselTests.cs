using System;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Sites;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Sites
{
    public class VesselTests
    {
        private static Vessel NewVessel(EquipmentTier tier = EquipmentTier.Copper, int capacity = 1000)
            => Vessel.Create("v1", VesselType.OpenFermenter, tier, capacity);

        [Fact]
        public void New_Vessel_Is_Available_And_In_Perfect_Condition()
        {
            var v = NewVessel();
            v.IsAvailable.Should().BeTrue();
            v.OccupiedBy.Should().BeNull();
            v.ConditionBasisPoints.Should().Be(10000);
        }

        [Fact]
        public void Occupying_A_Busy_Vessel_Throws()
        {
            var v = NewVessel();
            v.Occupy(new BatchId("b1"), 5);

            Action act = () => v.Occupy(new BatchId("b2"), 3);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Occupied_Vessel_Reports_Its_Batch()
        {
            var v = NewVessel();
            v.Occupy(new BatchId("b1"), 5);

            v.IsAvailable.Should().BeFalse();
            v.OccupiedBy.Should().Be(new BatchId("b1"));
        }

        [Fact]
        public void Vessel_Is_Unavailable_For_Exactly_N_Days_Then_Frees_Itself()
        {
            var v = NewVessel();
            v.Occupy(new BatchId("b1"), 3);

            v.AdvanceDay();
            v.IsAvailable.Should().BeFalse();
            v.AdvanceDay();
            v.IsAvailable.Should().BeFalse();
            v.AdvanceDay();
            v.IsAvailable.Should().BeTrue("the third day completes the occupancy");
        }

        [Fact]
        public void Advancing_Past_Release_Is_Safe_And_Idempotent()
        {
            var v = NewVessel();
            v.Occupy(new BatchId("b1"), 1);
            v.AdvanceDay();
            v.IsAvailable.Should().BeTrue();

            Action act = () =>
            {
                for (int i = 0; i < 10; i++) v.AdvanceDay();
            };

            act.Should().NotThrow();
            v.IsAvailable.Should().BeTrue();
            v.DaysRemaining.Should().Be(0);
        }

        [Fact]
        public void Freed_Vessel_Can_Be_Reoccupied()
        {
            var v = NewVessel();
            v.Occupy(new BatchId("b1"), 1);
            v.AdvanceDay();

            Action act = () => v.Occupy(new BatchId("b2"), 4);
            act.Should().NotThrow();
            v.OccupiedBy.Should().Be(new BatchId("b2"));
        }

        [Fact]
        public void Release_Frees_A_Busy_Vessel_Immediately()
        {
            var v = NewVessel();
            v.Occupy(new BatchId("b1"), 20);
            v.Release();

            v.IsAvailable.Should().BeTrue();
            v.DaysRemaining.Should().Be(0);
        }

        [Fact]
        public void Hygiene_Increases_Strictly_With_Tier()
        {
            int wooden = Vessel.BaseHygieneFor(EquipmentTier.Wooden);
            int copper = Vessel.BaseHygieneFor(EquipmentTier.Copper);
            int iron = Vessel.BaseHygieneFor(EquipmentTier.Iron);
            int stainless = Vessel.BaseHygieneFor(EquipmentTier.Stainless);

            copper.Should().BeGreaterThan(wooden);
            iron.Should().BeGreaterThan(copper);
            stainless.Should().BeGreaterThan(iron);
        }

        [Fact]
        public void Stainless_Vessel_Is_More_Hygienic_Than_Wooden()
        {
            var wood = NewVessel(EquipmentTier.Wooden);
            var steel = NewVessel(EquipmentTier.Stainless);

            steel.HygieneBasisPoints.Should().BeGreaterThan(wood.HygieneBasisPoints);
        }

        [Fact]
        public void Condition_Degrades_And_Clamps_At_Zero()
        {
            var v = NewVessel();
            v.DegradeCondition(3000);
            v.ConditionBasisPoints.Should().Be(7000);

            v.DegradeCondition(99999);
            v.ConditionBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Maintain_Restores_Condition_And_Clamps_At_Full()
        {
            var v = NewVessel();
            v.DegradeCondition(5000);
            v.Maintain(2000);
            v.ConditionBasisPoints.Should().Be(7000);

            v.Maintain(99999);
            v.ConditionBasisPoints.Should().Be(10000);
        }

        [Fact]
        public void Effective_Hygiene_Falls_As_Condition_Falls()
        {
            var v = NewVessel(EquipmentTier.Stainless);
            int full = v.EffectiveHygieneBasisPoints;

            v.DegradeCondition(5000);
            v.EffectiveHygieneBasisPoints.Should().BeLessThan(full);

            v.DegradeCondition(5000);
            v.EffectiveHygieneBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Negative_Condition_Adjustments_Throw()
        {
            var v = NewVessel();
            Action degrade = () => v.DegradeCondition(-1);
            Action maintain = () => v.Maintain(-1);

            degrade.Should().Throw<ArgumentOutOfRangeException>();
            maintain.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Non_Positive_Capacity_Throws()
        {
            Action act = () => new Vessel(new VesselId("x"), VesselType.MashTun,
                                          EquipmentTier.Copper, 0,
                                          BreweryEmpire.Core.Economy.Money.Zero,
                                          BreweryEmpire.Core.Economy.Money.Zero);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Factory_Sets_Cost_And_Upkeep()
        {
            var v = Vessel.Create("v9", VesselType.ConditioningTank, EquipmentTier.Iron, 800);
            v.PurchaseCost.IsPositive.Should().BeTrue();
            v.DailyUpkeep.IsPositive.Should().BeTrue();
            v.CapacityLitres.Should().Be(800);
            v.Tier.Should().Be(EquipmentTier.Iron);
        }
    }
}
