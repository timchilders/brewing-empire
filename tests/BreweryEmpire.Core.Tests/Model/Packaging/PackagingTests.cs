using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Packaging
{
    public class PackagingSpecTests
    {
        [Fact]
        public void Bottles_Have_No_Ullage_Loss()
        {
            PackagingSpec.Bottle.UllageLossBasisPointsPerLeg.Should().Be(0);
            PackagingSpec.Can.UllageLossBasisPointsPerLeg.Should().Be(0);
        }

        [Fact]
        public void Casks_Lose_Beer_In_Transit()
        {
            PackagingSpec.WoodenCask.UllageLossBasisPointsPerLeg.Should().BePositive();
        }

        [Fact]
        public void Casks_Are_Returnable_And_Bottles_Are_Not()
        {
            PackagingSpec.WoodenCask.IsReturnable.Should().BeTrue();
            PackagingSpec.MetalKeg.IsReturnable.Should().BeTrue();
            PackagingSpec.Bottle.IsReturnable.Should().BeFalse();
            PackagingSpec.Can.IsReturnable.Should().BeFalse();
        }

        [Fact]
        public void Sealed_Formats_Keep_Beer_Longer_Than_Casks()
        {
            PackagingSpec.Bottle.ShelfLifeDays
                .Should().BeGreaterThan(PackagingSpec.WoodenCask.ShelfLifeDays);
        }

        [Fact]
        public void Casks_Admit_More_Oxygen_Than_Sealed_Formats()
        {
            PackagingSpec.WoodenCask.OxygenIngressBasisPoints
                .Should().BeGreaterThan(PackagingSpec.Bottle.OxygenIngressBasisPoints);
        }

        [Theory]
        [InlineData(PackagingType.WoodenCask)]
        [InlineData(PackagingType.Bottle)]
        [InlineData(PackagingType.MetalKeg)]
        [InlineData(PackagingType.Can)]
        public void For_Returns_Matching_Spec(PackagingType type)
        {
            PackagingSpec.For(type).Type.Should().Be(type);
        }
    }

    public class ContainerPoolTests
    {
        private static ContainerPool NewPool(int owned = 100) =>
            new ContainerPool(PackagingType.WoodenCask, owned, Money.FromWhole(3));

        [Fact]
        public void Reserving_Moves_Containers_Into_Use()
        {
            var pool = NewPool(100);
            pool.TryReserve(30).Should().BeTrue();

            pool.InUse.Should().Be(30);
            pool.Available.Should().Be(70);
        }

        [Fact]
        public void Reserving_Beyond_Available_Returns_False_And_Mutates_Nothing()
        {
            var pool = NewPool(10);
            pool.TryReserve(4).Should().BeTrue();

            int owned = pool.OwnedTotal, inUse = pool.InUse;
            int atCustomers = pool.AtCustomerSites, lost = pool.Lost;

            pool.TryReserve(50).Should().BeFalse();

            pool.OwnedTotal.Should().Be(owned);
            pool.InUse.Should().Be(inUse);
            pool.AtCustomerSites.Should().Be(atCustomers);
            pool.Lost.Should().Be(lost);
        }

        [Fact]
        public void Delivering_Moves_Containers_To_Customers()
        {
            var pool = NewPool(100);
            pool.TryReserve(20);
            pool.MarkDelivered(20);

            pool.InUse.Should().Be(0);
            pool.AtCustomerSites.Should().Be(20);
            pool.Available.Should().Be(80);
        }

        [Fact]
        public void Delivering_More_Than_Reserved_Throws()
        {
            var pool = NewPool(100);
            pool.TryReserve(5);

            Action act = () => pool.MarkDelivered(10);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Returns_Restore_Availability_Minus_Losses()
        {
            var pool = NewPool(100);
            pool.TryReserve(50);
            pool.MarkDelivered(50);

            var rng = new DeterministicRandom(1234);
            int recovered = pool.ProcessReturns(50, rng, lossBasisPoints: 1000);   // ~10% loss

            recovered.Should().BeLessThan(50);
            recovered.Should().BeGreaterThan(30);
            pool.Lost.Should().Be(50 - recovered);
            pool.AtCustomerSites.Should().Be(0);
            pool.Available.Should().Be(100 - pool.Lost);
        }

        [Fact]
        public void Returns_Are_Deterministic_For_A_Given_Seed()
        {
            int RunOnce()
            {
                var pool = NewPool(200);
                pool.TryReserve(100);
                pool.MarkDelivered(100);
                return pool.ProcessReturns(100, new DeterministicRandom(999), 1500);
            }

            int first = RunOnce();
            for (int i = 0; i < 5; i++) RunOnce().Should().Be(first);
        }

        [Fact]
        public void Zero_Loss_Rate_Recovers_Everything()
        {
            var pool = NewPool(50);
            pool.TryReserve(50);
            pool.MarkDelivered(50);

            int recovered = pool.ProcessReturns(50, new DeterministicRandom(7), 0);

            recovered.Should().Be(50);
            pool.Lost.Should().Be(0);
            pool.Available.Should().Be(50);
        }

        [Fact]
        public void Total_Loss_Rate_Recovers_Nothing()
        {
            var pool = NewPool(50);
            pool.TryReserve(50);
            pool.MarkDelivered(50);

            int recovered = pool.ProcessReturns(50, new DeterministicRandom(7), 10000);

            recovered.Should().Be(0);
            pool.Lost.Should().Be(50);
            pool.Available.Should().Be(0);
        }

        [Fact]
        public void Returning_More_Than_Are_Out_Throws()
        {
            var pool = NewPool(50);
            Action act = () => pool.ProcessReturns(10, new DeterministicRandom(1), 500);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Purchase_Increases_Owned_And_Available()
        {
            var pool = NewPool(10);
            pool.Purchase(15);

            pool.OwnedTotal.Should().Be(25);
            pool.Available.Should().Be(25);
        }

        [Fact]
        public void Replacing_Lost_Containers_Costs_Money_And_Clears_Losses()
        {
            var pool = NewPool(20);
            pool.TryReserve(20);
            pool.MarkDelivered(20);
            pool.ProcessReturns(20, new DeterministicRandom(3), 10000);   // lose all

            pool.Lost.Should().Be(20);
            var cost = pool.ReplaceLost(20);

            cost.Should().Be(Money.FromWhole(60));
            pool.Lost.Should().Be(0);
            pool.InvariantHolds().Should().BeTrue();
        }

        /// <summary>
        /// THE CONSERVATION INVARIANT under a long randomised workload.
        /// Containers must never be created or destroyed by accident.
        /// </summary>
        [Fact]
        public void Available_Never_Negative_And_Invariant_Holds_Over_Long_Sequence()
        {
            var pool = NewPool(500);
            var rng = new DeterministicRandom(20260806);

            for (int step = 0; step < 1000; step++)
            {
                int action = rng.NextInt(0, 4);

                if (action == 0)
                {
                    pool.TryReserve(rng.NextInt(1, 60));
                }
                else if (action == 1 && pool.InUse > 0)
                {
                    pool.MarkDelivered(rng.NextInt(1, pool.InUse + 1));
                }
                else if (action == 2 && pool.AtCustomerSites > 0)
                {
                    pool.ProcessReturns(rng.NextInt(1, pool.AtCustomerSites + 1), rng, 800);
                }
                else if (action == 3 && pool.Lost > 0)
                {
                    pool.ReplaceLost(rng.NextInt(1, pool.Lost + 1));
                }

                pool.Available.Should().BeGreaterOrEqualTo(0, "at step " + step);
                pool.InvariantHolds().Should().BeTrue("at step " + step);
            }
        }
    }
}
