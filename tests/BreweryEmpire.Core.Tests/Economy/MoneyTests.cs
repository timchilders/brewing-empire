using System;
using BreweryEmpire.Core.Economy;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Economy
{
    public class MoneyTests
    {
        [Fact]
        public void FromCents_And_FromWhole_Agree()
        {
            Money.FromWhole(5).Should().Be(Money.FromCents(500));
        }

        [Fact]
        public void Addition_And_Subtraction_Are_Exact()
        {
            (Money.FromCents(150) + Money.FromCents(275)).Cents.Should().Be(425);
            (Money.FromCents(150) - Money.FromCents(275)).Cents.Should().Be(-125);
        }

        [Fact]
        public void Multiplication_Scales_By_Quantity()
        {
            (Money.FromCents(250) * 4).Cents.Should().Be(1000);
            (3 * Money.FromCents(100)).Cents.Should().Be(300);
        }

        [Fact]
        public void PercentBasisPoints_Scales_Without_Truncation_Error()
        {
            Money.FromCents(100).PercentBasisPoints(2500).Cents.Should().Be(25);
            Money.FromCents(100).PercentBasisPoints(10000).Cents.Should().Be(100);
            Money.FromCents(100).PercentBasisPoints(0).Cents.Should().Be(0);
        }

        [Fact]
        public void PercentBasisPoints_Rounds_Half_Away_From_Zero()
        {
            // 1 cent * 50% = 0.5 -> 1
            Money.FromCents(1).PercentBasisPoints(5000).Cents.Should().Be(1);
            Money.FromCents(-1).PercentBasisPoints(5000).Cents.Should().Be(-1);
        }

        [Fact]
        public void PercentBasisPoints_Handles_Large_Values()
        {
            // A global empire must not overflow or lose precision.
            var large = Money.FromCents(1_000_000_000_00L);
            large.PercentBasisPoints(500).Cents.Should().Be(5_000_000_00L * 10);
        }

        [Fact]
        public void Sign_Helpers_Are_Correct()
        {
            Money.FromCents(-5).IsNegative.Should().BeTrue();
            Money.FromCents(5).IsPositive.Should().BeTrue();
            Money.Zero.IsZero.Should().BeTrue();
            Money.FromCents(-5).Abs().Cents.Should().Be(5);
            Money.FromCents(5).Negated().Cents.Should().Be(-5);
        }

        [Fact]
        public void Comparisons_Order_Correctly()
        {
            (Money.FromCents(10) > Money.FromCents(5)).Should().BeTrue();
            (Money.FromCents(5) < Money.FromCents(10)).Should().BeTrue();
            (Money.FromCents(5) >= Money.FromCents(5)).Should().BeTrue();
            (Money.FromCents(5) <= Money.FromCents(5)).Should().BeTrue();
            (Money.FromCents(5) == Money.FromCents(5)).Should().BeTrue();
            (Money.FromCents(5) != Money.FromCents(6)).Should().BeTrue();
        }

        [Fact]
        public void Division_By_Zero_Throws()
        {
            Action act = () => { var _ = Money.FromCents(100) / 0; };
            act.Should().Throw<DivideByZeroException>();
        }

        [Theory]
        [InlineData(0, "0.00")]
        [InlineData(5, "0.05")]
        [InlineData(150, "1.50")]
        [InlineData(-150, "-1.50")]
        [InlineData(-5, "-0.05")]
        public void ToString_Formats_As_Decimal(long cents, string expected)
        {
            Money.FromCents(cents).ToString().Should().Be(expected);
        }

        [Fact]
        public void Equality_Is_Value_Based()
        {
            Money.FromCents(42).Equals(Money.FromCents(42)).Should().BeTrue();
            Money.FromCents(42).GetHashCode().Should().Be(Money.FromCents(42).GetHashCode());
        }
    }
}
