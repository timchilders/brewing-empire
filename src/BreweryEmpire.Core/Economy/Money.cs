using System;
using System.Globalization;

namespace BreweryEmpire.Core.Economy
{
    /// <summary>
    /// Integer currency in cents.
    ///
    /// Backed by long because a global empire across a 100+ year campaign will
    /// exceed int range. Integer-only arithmetic keeps saves exact and avoids
    /// floating-point drift accumulating over hundreds of thousands of ticks.
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        public static readonly Money Zero = new Money(0);

        public long Cents { get; }

        private Money(long cents)
        {
            Cents = cents;
        }

        public static Money FromCents(long cents) => new Money(cents);

        public static Money FromWhole(long units) => new Money(units * 100);

        public bool IsZero => Cents == 0;
        public bool IsNegative => Cents < 0;
        public bool IsPositive => Cents > 0;

        public Money Abs() => new Money(Math.Abs(Cents));
        public Money Negated() => new Money(-Cents);

        /// <summary>
        /// Scale by basis points (10000 = 100%), rounding half away from zero.
        /// Integer-safe: the multiply happens before the divide.
        /// </summary>
        public Money PercentBasisPoints(int basisPoints)
        {
            long numerator = Cents * basisPoints;
            long rounded = numerator >= 0
                ? (numerator + 5000) / 10000
                : (numerator - 5000) / 10000;
            return new Money(rounded);
        }

        public static Money operator +(Money a, Money b) => new Money(a.Cents + b.Cents);
        public static Money operator -(Money a, Money b) => new Money(a.Cents - b.Cents);
        public static Money operator -(Money a) => new Money(-a.Cents);
        public static Money operator *(Money a, int qty) => new Money(a.Cents * qty);
        public static Money operator *(int qty, Money a) => new Money(a.Cents * qty);
        public static Money operator *(Money a, long qty) => new Money(a.Cents * qty);

        public static Money operator /(Money a, int divisor)
        {
            if (divisor == 0) throw new DivideByZeroException();
            return new Money(a.Cents / divisor);
        }

        public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
        public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
        public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;
        public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;
        public static bool operator ==(Money a, Money b) => a.Cents == b.Cents;
        public static bool operator !=(Money a, Money b) => a.Cents != b.Cents;

        public bool Equals(Money other) => Cents == other.Cents;
        public override bool Equals(object? obj) => obj is Money m && Equals(m);
        public override int GetHashCode() => Cents.GetHashCode();
        public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

        public override string ToString()
        {
            long whole = Cents / 100;
            long frac = Math.Abs(Cents % 100);
            string sign = Cents < 0 && whole == 0 ? "-" : string.Empty;
            return sign + whole.ToString(CultureInfo.InvariantCulture)
                        + "." + frac.ToString("D2", CultureInfo.InvariantCulture);
        }
    }
}
