using System;
using System.Linq;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.State
{
    public class DeterministicRandomTests
    {
        [Fact]
        public void Same_Seed_Yields_Same_Sequence()
        {
            var a = new DeterministicRandom(12345);
            var b = new DeterministicRandom(12345);

            var seqA = Enumerable.Range(0, 100).Select(_ => a.NextInt(0, 1000)).ToArray();
            var seqB = Enumerable.Range(0, 100).Select(_ => b.NextInt(0, 1000)).ToArray();

            seqA.Should().Equal(seqB);
        }

        [Fact]
        public void Different_Seeds_Yield_Different_Sequences()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);

            var seqA = Enumerable.Range(0, 50).Select(_ => a.NextInt(0, 1000)).ToArray();
            var seqB = Enumerable.Range(0, 50).Select(_ => b.NextInt(0, 1000)).ToArray();

            seqA.Should().NotEqual(seqB);
        }

        [Fact]
        public void Sequential_Seeds_Are_Well_Separated()
        {
            // SplitMix64 seeding must stop seeds 1/2/3 producing correlated streams.
            var first = Enumerable.Range(1, 5)
                .Select(s => new DeterministicRandom(s).NextInt(0, 1_000_000))
                .ToArray();

            first.Distinct().Should().HaveCount(5);
        }

        [Fact]
        public void State_Round_Trips()
        {
            var a = new DeterministicRandom(7);
            a.NextInt(0, 100);
            a.NextInt(0, 100);

            var expected = a.Clone().NextInt(0, 100);
            var restored = DeterministicRandom.FromState(a.State);

            restored.NextInt(0, 100).Should().Be(expected);
        }

        [Fact]
        public void State_Round_Trip_Preserves_Long_Sequence()
        {
            var a = new DeterministicRandom(99);
            for (int i = 0; i < 500; i++) a.NextULong();

            var restored = DeterministicRandom.FromState(a.State);
            var expected = a.Clone();

            for (int i = 0; i < 100; i++)
                restored.NextULong().Should().Be(expected.NextULong());
        }

        [Fact]
        public void Clone_Is_Independent()
        {
            var a = new DeterministicRandom(42);
            var clone = a.Clone();

            a.NextInt(0, 100);   // advance original only

            clone.State.Should().NotEqual(a.State);
        }

        [Fact]
        public void FromState_Rejects_Malformed_State()
        {
            Action wrongLength = () => DeterministicRandom.FromState(new ulong[] { 1 });
            wrongLength.Should().Throw<ArgumentException>();

            Action allZero = () => DeterministicRandom.FromState(new ulong[] { 0, 0 });
            allZero.Should().Throw<ArgumentException>();

            Action nullState = () => DeterministicRandom.FromState(null!);
            nullState.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void NextInt_Respects_Bounds()
        {
            var rng = new DeterministicRandom(2024);
            for (int i = 0; i < 10_000; i++)
            {
                rng.NextInt(0, 10).Should().BeInRange(0, 9);
            }
        }

        [Fact]
        public void NextInt_Handles_Negative_Range()
        {
            var rng = new DeterministicRandom(5);
            for (int i = 0; i < 1000; i++)
            {
                rng.NextInt(-50, 50).Should().BeInRange(-50, 49);
            }
        }

        [Fact]
        public void NextInt_Single_Value_Range_Is_Constant()
        {
            var rng = new DeterministicRandom(1);
            for (int i = 0; i < 10; i++)
                rng.NextInt(5, 6).Should().Be(5);
        }

        [Fact]
        public void NextInt_Rejects_Empty_Or_Inverted_Range()
        {
            var rng = new DeterministicRandom(1);
            Action empty = () => rng.NextInt(5, 5);
            empty.Should().Throw<ArgumentOutOfRangeException>();

            Action inverted = () => rng.NextInt(10, 5);
            inverted.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void NextInt_Is_Roughly_Uniform()
        {
            var rng = new DeterministicRandom(31337);
            var buckets = new int[10];
            const int draws = 100_000;

            for (int i = 0; i < draws; i++) buckets[rng.NextInt(0, 10)]++;

            // Each bucket should hold ~10% of draws; allow generous tolerance.
            foreach (var b in buckets)
                b.Should().BeInRange((int)(draws * 0.09), (int)(draws * 0.11));
        }

        [Fact]
        public void Chance_BasisPoints_Is_Unbiased()
        {
            var rng = new DeterministicRandom(777);
            int hits = 0;
            const int trials = 100_000;

            for (int i = 0; i < trials; i++)
                if (rng.Chance(5000)) hits++;

            // 50% +/- 1%
            hits.Should().BeInRange((int)(trials * 0.49), (int)(trials * 0.51));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void Chance_Never_Fires_At_Or_Below_Zero(int bp)
        {
            var rng = new DeterministicRandom(1);
            for (int i = 0; i < 100; i++)
                rng.Chance(bp).Should().BeFalse();
        }

        [Theory]
        [InlineData(10000)]
        [InlineData(20000)]
        public void Chance_Always_Fires_At_Or_Above_Full(int bp)
        {
            var rng = new DeterministicRandom(1);
            for (int i = 0; i < 100; i++)
                rng.Chance(bp).Should().BeTrue();
        }

        [Fact]
        public void Chance_Low_Probability_Is_Approximately_Correct()
        {
            var rng = new DeterministicRandom(4242);
            int hits = 0;
            const int trials = 100_000;

            for (int i = 0; i < trials; i++)
                if (rng.Chance(100)) hits++;   // 1%

            hits.Should().BeInRange((int)(trials * 0.008), (int)(trials * 0.012));
        }

        [Fact]
        public void Variance_Stays_Within_Spread()
        {
            var rng = new DeterministicRandom(88);
            for (int i = 0; i < 5000; i++)
                rng.Variance(5000, 500).Should().BeInRange(4500, 5500);
        }

        [Fact]
        public void Variance_With_Zero_Spread_Is_Exact()
        {
            var rng = new DeterministicRandom(88);
            rng.Variance(1234, 0).Should().Be(1234);
        }

        [Fact]
        public void Variance_Rejects_Negative_Spread()
        {
            var rng = new DeterministicRandom(1);
            Action act = () => rng.Variance(100, -1);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Never_Degenerates_To_Zero_State()
        {
            // A zero state would lock xorshift128+ at zero forever.
            var rng = new DeterministicRandom(0);
            var draws = Enumerable.Range(0, 100).Select(_ => rng.NextULong()).ToArray();
            draws.Should().Contain(d => d != 0);
        }
    }
}
