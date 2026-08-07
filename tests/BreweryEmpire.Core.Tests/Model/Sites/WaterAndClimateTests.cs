using System;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Sites
{
    public class WaterProfileTests
    {
        [Fact]
        public void Burton_Is_Strongly_Sulfate_Dominant()
        {
            // Burton's gypsum water is why pale ale happened there.
            WaterProfile.Burton.SulfateChlorideRatioBasisPoints.Should().BeGreaterThan(20000);
        }

        [Fact]
        public void Pilsen_Is_Soft_And_Others_Are_Not()
        {
            WaterProfile.Pilsen.IsSoft.Should().BeTrue();
            WaterProfile.Munich.IsSoft.Should().BeFalse();
            WaterProfile.Dublin.IsSoft.Should().BeFalse();
            WaterProfile.Burton.IsSoft.Should().BeFalse();
        }

        [Fact]
        public void Zero_Chloride_Does_Not_Divide_By_Zero()
        {
            var w = new WaterProfile { SulfatePpm = 100, ChloridePpm = 0 };
            w.SulfateChlorideRatioBasisPoints.Should().Be(WaterProfile.MaxRatioBasisPoints);
        }

        [Fact]
        public void Zero_Sulfate_And_Chloride_Is_Neutral_Ratio()
        {
            var w = new WaterProfile { SulfatePpm = 0, ChloridePpm = 0 };
            w.SulfateChlorideRatioBasisPoints.Should().Be(10000);
        }

        [Fact]
        public void Fit_Score_Is_Perfect_Against_Itself()
        {
            WaterProfile.Burton.FitScoreBasisPoints(WaterProfile.Burton).Should().Be(10000);
            WaterProfile.Pilsen.FitScoreBasisPoints(WaterProfile.Pilsen).Should().Be(10000);
        }

        [Fact]
        public void Fit_Score_Falls_For_A_Different_Profile()
        {
            // Burton water is a poor match for a Pilsen target.
            int score = WaterProfile.Burton.FitScoreBasisPoints(WaterProfile.Pilsen);
            score.Should().BeLessThan(10000);
            score.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void Fit_Score_Is_Symmetric()
        {
            WaterProfile.Burton.FitScoreBasisPoints(WaterProfile.Dublin)
                .Should().Be(WaterProfile.Dublin.FitScoreBasisPoints(WaterProfile.Burton));
        }

        [Fact]
        public void Fit_Score_Clamps_At_Zero_For_Extreme_Mismatch()
        {
            var extreme = new WaterProfile { CalciumPpm = 100000 };
            extreme.FitScoreBasisPoints(WaterProfile.Pilsen).Should().Be(0);
        }

        [Fact]
        public void Fit_Score_Rejects_Null_Target()
        {
            Action act = () => WaterProfile.Burton.FitScoreBasisPoints(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Nearer_Profiles_Score_Higher_Than_Distant_Ones()
        {
            // London is far closer to Dublin than Burton is to Pilsen.
            int near = WaterProfile.London.FitScoreBasisPoints(WaterProfile.Dublin);
            int far = WaterProfile.Burton.FitScoreBasisPoints(WaterProfile.Pilsen);
            near.Should().BeGreaterThan(far);
        }
    }

    public class RegionClimateTests
    {
        [Fact]
        public void Wrong_Length_Temperature_Array_Throws()
        {
            Action act = () => new RegionClimate
            {
                RegionId = "bad",
                MonthlyAvgTempCelsius = new[] { 1, 2, 3 }
            };
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Ambient_Temp_Is_Deterministic_Across_Year_Boundary()
        {
            var climate = RegionClimate.BurtonEngland;

            // Day 364 is 31 Dec 1750; day 365 is 1 Jan 1751. Neither may throw.
            var dec = GameDate.FromTotalDays(364);
            var jan = GameDate.FromTotalDays(365);

            climate.AmbientTempOn(dec).Should().Be(climate.MonthlyAvgTempCelsius[11]);
            climate.AmbientTempOn(jan).Should().Be(climate.MonthlyAvgTempCelsius[0]);
        }

        [Fact]
        public void Ambient_Temp_Never_Throws_Across_A_Full_Year()
        {
            var climate = RegionClimate.Pilsen;
            for (int day = 0; day < GameDate.DaysPerYear * 3; day++)
            {
                Action act = () => climate.AmbientTempOn(GameDate.FromTotalDays(day));
                act.Should().NotThrow();
            }
        }

        [Fact]
        public void Ambient_Temp_Is_Repeatable_For_The_Same_Date()
        {
            var climate = RegionClimate.Munich;
            var date = GameDate.FromYearMonthDay(1842, 7, 15);
            int first = climate.AmbientTempOn(date);

            for (int i = 0; i < 10; i++)
                climate.AmbientTempOn(date).Should().Be(first);
        }

        [Fact]
        public void Cold_Regions_Support_Ice_Harvest()
        {
            RegionClimate.Pilsen.SupportsWinterIceHarvest.Should().BeTrue();
            RegionClimate.Munich.SupportsWinterIceHarvest.Should().BeTrue();
            RegionClimate.Bavaria.SupportsWinterIceHarvest.Should().BeTrue();
        }

        [Fact]
        public void Warm_Regions_Do_Not_Support_Ice_Harvest()
        {
            RegionClimate.Mediterranean.SupportsWinterIceHarvest.Should().BeFalse();
            RegionClimate.Dublin.SupportsWinterIceHarvest.Should().BeFalse();
        }

        [Fact]
        public void Summer_Is_Warmer_Than_Winter()
        {
            var climate = RegionClimate.BurtonEngland;
            int july = climate.AmbientTempOn(GameDate.FromYearMonthDay(1750, 7, 15));
            int january = climate.AmbientTempOn(GameDate.FromYearMonthDay(1750, 1, 15));
            july.Should().BeGreaterThan(january);
        }

        [Fact]
        public void Warmest_And_Coldest_Are_Consistent()
        {
            var climate = RegionClimate.Bavaria;
            climate.WarmestMonthTemp.Should().BeGreaterThan(climate.ColdestMonthTemp);
            climate.ColdestMonthTemp.Should().BeLessOrEqualTo(0);
        }

        [Fact]
        public void Temperature_Array_Is_Defensively_Copied()
        {
            var source = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var climate = new RegionClimate { RegionId = "test", MonthlyAvgTempCelsius = source };

            source[0] = 999;

            climate.AmbientTempOn(GameDate.FromYearMonthDay(1750, 1, 1)).Should().Be(1);
        }
    }
}
