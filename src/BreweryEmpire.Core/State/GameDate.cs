using System;

namespace BreweryEmpire.Core.State
{
    /// <summary>Season derived from the day of year.</summary>
    public enum Season
    {
        Winter = 0,
        Spring = 1,
        Summer = 2,
        Autumn = 3
    }

    /// <summary>The four historical eras of the campaign.</summary>
    public enum Era
    {
        PreIndustrial = 0,  // 1750-1830
        Industrial = 1,     // 1830-1880
        Scientific = 2,     // 1880-1920
        Modern = 3          // 1920+
    }

    /// <summary>
    /// Integer-day calendar. One tick = one day.
    ///
    /// DELIBERATE SIMPLIFICATION: uses a fixed 365-day year with no leap years.
    /// This keeps date arithmetic exact and platform-independent, which matters
    /// far more for deterministic simulation than calendar fidelity. A
    /// history-minded player may eventually notice; documented in
    /// docs/architecture.md as a known tradeoff.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        /// <summary>The campaign epoch: 1 January 1750.</summary>
        public const int EpochYear = 1750;

        public const int DaysPerYear = 365;

        private static readonly int[] MonthLengths =
            { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        /// <summary>Days elapsed since the epoch. May be negative for pre-epoch dates.</summary>
        public int TotalDays { get; }

        private GameDate(int totalDays)
        {
            TotalDays = totalDays;
        }

        public static GameDate FromTotalDays(int totalDays) => new GameDate(totalDays);

        public static GameDate FromYearMonthDay(int year, int month, int day)
        {
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1-12.");
            if (day < 1 || day > MonthLengths[month - 1])
                throw new ArgumentOutOfRangeException(nameof(day), day, "Day out of range for month.");

            int days = (year - EpochYear) * DaysPerYear;
            for (int m = 0; m < month - 1; m++)
                days += MonthLengths[m];
            days += day - 1;
            return new GameDate(days);
        }

        /// <summary>Zero-based day within the year, always in [0, 364].</summary>
        private int DayOfYearZeroBased
        {
            get
            {
                int d = TotalDays % DaysPerYear;
                return d < 0 ? d + DaysPerYear : d;
            }
        }

        public int Year
        {
            get
            {
                // Floor division so pre-epoch dates behave correctly.
                int y = TotalDays / DaysPerYear;
                if (TotalDays < 0 && TotalDays % DaysPerYear != 0) y--;
                return EpochYear + y;
            }
        }

        /// <summary>One-based day of year, 1-365.</summary>
        public int DayOfYear => DayOfYearZeroBased + 1;

        public int Month
        {
            get
            {
                int remaining = DayOfYearZeroBased;
                for (int m = 0; m < 12; m++)
                {
                    if (remaining < MonthLengths[m]) return m + 1;
                    remaining -= MonthLengths[m];
                }
                return 12;
            }
        }

        public int Day
        {
            get
            {
                int remaining = DayOfYearZeroBased;
                for (int m = 0; m < 12; m++)
                {
                    if (remaining < MonthLengths[m]) return remaining + 1;
                    remaining -= MonthLengths[m];
                }
                return remaining + 1;
            }
        }

        /// <summary>True on the first day of a month — the payroll trigger.</summary>
        public bool IsFirstOfMonth => Day == 1;

        public Season Season
        {
            get
            {
                switch (Month)
                {
                    case 12:
                    case 1:
                    case 2:
                        return Season.Winter;
                    case 3:
                    case 4:
                    case 5:
                        return Season.Spring;
                    case 6:
                    case 7:
                    case 8:
                        return Season.Summer;
                    default:
                        return Season.Autumn;
                }
            }
        }

        public Era Era
        {
            get
            {
                int y = Year;
                if (y < 1830) return Era.PreIndustrial;
                if (y < 1880) return Era.Industrial;
                if (y < 1920) return Era.Scientific;
                return Era.Modern;
            }
        }

        /// <summary>Pure: returns a new date, never mutates this one.</summary>
        public GameDate AddDays(int days) => new GameDate(TotalDays + days);

        public GameDate AddYears(int years) => new GameDate(TotalDays + years * DaysPerYear);

        public int DaysUntil(GameDate other) => other.TotalDays - TotalDays;

        public bool Equals(GameDate other) => TotalDays == other.TotalDays;

        public override bool Equals(object? obj) => obj is GameDate d && Equals(d);

        public override int GetHashCode() => TotalDays;

        public int CompareTo(GameDate other) => TotalDays.CompareTo(other.TotalDays);

        public static bool operator ==(GameDate a, GameDate b) => a.TotalDays == b.TotalDays;
        public static bool operator !=(GameDate a, GameDate b) => a.TotalDays != b.TotalDays;
        public static bool operator <(GameDate a, GameDate b) => a.TotalDays < b.TotalDays;
        public static bool operator >(GameDate a, GameDate b) => a.TotalDays > b.TotalDays;
        public static bool operator <=(GameDate a, GameDate b) => a.TotalDays <= b.TotalDays;
        public static bool operator >=(GameDate a, GameDate b) => a.TotalDays >= b.TotalDays;

        public override string ToString() =>
            Year.ToString("D4") + "-" + Month.ToString("D2") + "-" + Day.ToString("D2");
    }
}
