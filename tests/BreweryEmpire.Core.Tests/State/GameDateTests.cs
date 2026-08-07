using System;
using System.Linq;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.State
{
    public class GameDateTests
    {
        [Fact]
        public void Epoch_Is_1750_01_01()
        {
            var d = GameDate.FromTotalDays(0);
            d.Year.Should().Be(1750);
            d.Month.Should().Be(1);
            d.Day.Should().Be(1);
        }

        [Theory]
        [InlineData(0, Season.Winter)]    // 1 Jan
        [InlineData(180, Season.Summer)]  // ~30 Jun
        [InlineData(90, Season.Spring)]   // ~1 Apr
        [InlineData(280, Season.Autumn)]  // ~8 Oct
        public void Season_Derives_From_DayOfYear(int days, Season expected)
        {
            GameDate.FromTotalDays(days).Season.Should().Be(expected);
        }

        [Fact]
        public void AddDays_Is_Pure()
        {
            var d = GameDate.FromTotalDays(10);
            d.AddDays(5).TotalDays.Should().Be(15);
            d.TotalDays.Should().Be(10);
        }

        [Fact]
        public void AddDays_Accumulates_Across_Year_Boundary()
        {
            var newYearsEve = GameDate.FromYearMonthDay(1750, 12, 31);
            var next = newYearsEve.AddDays(1);
            next.Year.Should().Be(1751);
            next.Month.Should().Be(1);
            next.Day.Should().Be(1);
        }

        [Theory]
        [InlineData(1750, 1, 1)]
        [InlineData(1750, 12, 31)]
        [InlineData(1842, 6, 15)]
        [InlineData(1883, 11, 30)]
        [InlineData(1920, 2, 28)]
        public void FromYearMonthDay_Round_Trips(int year, int month, int day)
        {
            var d = GameDate.FromYearMonthDay(year, month, day);
            d.Year.Should().Be(year);
            d.Month.Should().Be(month);
            d.Day.Should().Be(day);
        }

        [Fact]
        public void Every_Day_Of_A_Year_Has_Valid_Month_And_Day()
        {
            for (int i = 0; i < GameDate.DaysPerYear; i++)
            {
                var d = GameDate.FromTotalDays(i);
                d.Year.Should().Be(1750);
                d.Month.Should().BeInRange(1, 12);
                d.Day.Should().BeInRange(1, 31);
            }
        }

        [Fact]
        public void DayOfYear_Spans_One_To_365()
        {
            GameDate.FromTotalDays(0).DayOfYear.Should().Be(1);
            GameDate.FromTotalDays(364).DayOfYear.Should().Be(365);
            GameDate.FromTotalDays(365).DayOfYear.Should().Be(1);
        }

        [Theory]
        [InlineData(1750, Era.PreIndustrial)]
        [InlineData(1829, Era.PreIndustrial)]
        [InlineData(1830, Era.Industrial)]
        [InlineData(1879, Era.Industrial)]
        [InlineData(1880, Era.Scientific)]
        [InlineData(1919, Era.Scientific)]
        [InlineData(1920, Era.Modern)]
        [InlineData(2000, Era.Modern)]
        public void Era_Derives_From_Year(int year, Era expected)
        {
            GameDate.FromYearMonthDay(year, 6, 1).Era.Should().Be(expected);
        }

        [Fact]
        public void IsFirstOfMonth_Fires_Twelve_Times_Per_Year()
        {
            int count = Enumerable.Range(0, GameDate.DaysPerYear)
                .Count(i => GameDate.FromTotalDays(i).IsFirstOfMonth);
            count.Should().Be(12);
        }

        [Fact]
        public void Comparison_Operators_Order_Correctly()
        {
            var early = GameDate.FromTotalDays(10);
            var late = GameDate.FromTotalDays(20);

            (early < late).Should().BeTrue();
            (late > early).Should().BeTrue();
            (early <= GameDate.FromTotalDays(10)).Should().BeTrue();
            (early >= GameDate.FromTotalDays(10)).Should().BeTrue();
            (early == GameDate.FromTotalDays(10)).Should().BeTrue();
            (early != late).Should().BeTrue();
        }

        [Fact]
        public void Equality_And_HashCode_Are_Value_Based()
        {
            var a = GameDate.FromTotalDays(500);
            var b = GameDate.FromTotalDays(500);
            a.Equals(b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void DaysUntil_Computes_Signed_Difference()
        {
            var a = GameDate.FromTotalDays(10);
            var b = GameDate.FromTotalDays(25);
            a.DaysUntil(b).Should().Be(15);
            b.DaysUntil(a).Should().Be(-15);
        }

        [Fact]
        public void ToString_Is_Iso_Like()
        {
            GameDate.FromYearMonthDay(1842, 6, 5).ToString().Should().Be("1842-06-05");
        }

        [Fact]
        public void AddYears_Advances_By_Whole_Years()
        {
            var d = GameDate.FromYearMonthDay(1750, 3, 14);
            var later = d.AddYears(10);
            later.Year.Should().Be(1760);
            later.Month.Should().Be(3);
            later.Day.Should().Be(14);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        public void FromYearMonthDay_Rejects_Invalid_Month(int month)
        {
            Action act = () => GameDate.FromYearMonthDay(1750, month, 1);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void FromYearMonthDay_Rejects_Invalid_Day()
        {
            Action act = () => GameDate.FromYearMonthDay(1750, 2, 30);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
