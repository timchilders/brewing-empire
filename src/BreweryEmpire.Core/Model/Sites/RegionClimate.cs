using System;
using System.Linq;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Sites
{
    /// <summary>
    /// Monthly temperature curve for a region.
    ///
    /// WHY IT MATTERS: before mechanical refrigeration (Linde, 1873) ambient
    /// temperature dictated what could be brewed and when. Warm summers spoil
    /// open-fermented beer; cold winters enable lagering and ice harvest.
    /// This is the mechanism that makes the pre-industrial calendar tense and
    /// makes refrigeration feel like a genuine breakthrough when unlocked.
    /// </summary>
    public sealed record RegionClimate
    {
        private readonly int[] _monthlyAvgTempCelsius = new int[12];

        public string RegionId { get; init; } = string.Empty;

        public int[] MonthlyAvgTempCelsius
        {
            get => _monthlyAvgTempCelsius;
            init
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length != 12)
                    throw new ArgumentException(
                        "MonthlyAvgTempCelsius must have exactly 12 entries, got " + value.Length + ".",
                        nameof(value));
                _monthlyAvgTempCelsius = (int[])value.Clone();
            }
        }

        /// <summary>
        /// Ambient temperature on a date. Uses the month's average — deterministic,
        /// total, and safe across the December/January wrap.
        /// </summary>
        public int AmbientTempOn(GameDate date) => _monthlyAvgTempCelsius[date.Month - 1];

        /// <summary>Ice harvesting needs at least one month at or below freezing.</summary>
        public bool SupportsWinterIceHarvest => _monthlyAvgTempCelsius.Any(t => t <= 0);

        public int WarmestMonthTemp => _monthlyAvgTempCelsius.Max();
        public int ColdestMonthTemp => _monthlyAvgTempCelsius.Min();

        private static RegionClimate Make(string id, params int[] temps) =>
            new RegionClimate { RegionId = id, MonthlyAvgTempCelsius = temps };

        //                                      J   F   M   A   M   J   J   A   S   O   N   D
        public static RegionClimate BurtonEngland =>
            Make("burton", 4, 4, 6, 9, 12, 15, 17, 17, 14, 11, 7, 5);

        public static RegionClimate Pilsen =>
            Make("pilsen", -2, -1, 3, 8, 13, 16, 19, 18, 14, 9, 3, -1);

        public static RegionClimate Munich =>
            Make("munich", -2, -1, 3, 8, 13, 16, 18, 18, 14, 9, 3, -1);

        public static RegionClimate Dublin =>
            Make("dublin", 5, 5, 7, 8, 11, 14, 15, 15, 13, 11, 7, 6);

        public static RegionClimate Bavaria =>
            Make("bavaria", -3, -2, 2, 7, 12, 15, 18, 17, 13, 8, 2, -2);

        /// <summary>Warm-climate region: no ice harvest, high spoilage risk.</summary>
        public static RegionClimate Mediterranean =>
            Make("mediterranean", 10, 11, 13, 16, 20, 25, 28, 28, 24, 19, 14, 11);
    }
}
