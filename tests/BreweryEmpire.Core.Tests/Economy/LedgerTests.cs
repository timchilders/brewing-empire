using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Economy
{
    public class LedgerTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);

        [Fact]
        public void Post_Credit_Then_Debit_Yields_Correct_Balance()
        {
            var ledger = new Ledger();
            ledger.Credit(D(0), LedgerCategory.BeerSales, Money.FromWhole(100), "sale");
            ledger.ForceDebit(D(1), LedgerCategory.Wages, Money.FromWhole(30), "wages");

            ledger.Balance.Should().Be(Money.FromWhole(70));
        }

        [Fact]
        public void Opening_Balance_Constructor_Posts_Capital()
        {
            var ledger = new Ledger(Money.FromWhole(500), D(0));
            ledger.Balance.Should().Be(Money.FromWhole(500));
            ledger.Entries.Should().ContainSingle();
        }

        [Fact]
        public void TryDebit_Beyond_Balance_Returns_False_And_Posts_Nothing()
        {
            var ledger = new Ledger(Money.FromWhole(10), D(0));
            int entriesBefore = ledger.Entries.Count;

            bool ok = ledger.TryDebit(D(1), LedgerCategory.IngredientPurchase,
                                      Money.FromWhole(50), "too much");

            ok.Should().BeFalse();
            ledger.Balance.Should().Be(Money.FromWhole(10));
            ledger.Entries.Count.Should().Be(entriesBefore);
        }

        [Fact]
        public void TryDebit_Within_Balance_Succeeds()
        {
            var ledger = new Ledger(Money.FromWhole(100), D(0));
            bool ok = ledger.TryDebit(D(1), LedgerCategory.IngredientPurchase,
                                      Money.FromWhole(40), "malt");

            ok.Should().BeTrue();
            ledger.Balance.Should().Be(Money.FromWhole(60));
        }

        [Fact]
        public void TryDebit_Exact_Balance_Succeeds()
        {
            var ledger = new Ledger(Money.FromWhole(50), D(0));
            ledger.TryDebit(D(1), LedgerCategory.Upkeep, Money.FromWhole(50), "exact")
                  .Should().BeTrue();
            ledger.Balance.Should().Be(Money.Zero);
        }

        [Fact]
        public void ForceDebit_Allows_Negative_Balance()
        {
            var ledger = new Ledger(Money.FromWhole(10), D(0));
            ledger.ForceDebit(D(1), LedgerCategory.LoanInterest, Money.FromWhole(25), "interest");

            ledger.Balance.IsNegative.Should().BeTrue();
            ledger.Balance.Should().Be(Money.FromWhole(-15));
        }

        [Fact]
        public void Debit_And_Credit_Reject_Negative_Amounts()
        {
            var ledger = new Ledger();

            Action badDebit = () => ledger.TryDebit(D(0), LedgerCategory.Wages,
                                                    Money.FromCents(-1), "bad");
            badDebit.Should().Throw<ArgumentOutOfRangeException>();

            Action badCredit = () => ledger.Credit(D(0), LedgerCategory.BeerSales,
                                                   Money.FromCents(-1), "bad");
            badCredit.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Zero_Amount_Posts_No_Entry()
        {
            var ledger = new Ledger();
            ledger.Post(D(0), LedgerCategory.Wages, Money.Zero, "nothing");
            ledger.Entries.Should().BeEmpty();
        }

        [Fact]
        public void TotalFor_Filters_By_Category_And_Inclusive_Date_Range()
        {
            var ledger = new Ledger();
            ledger.Credit(D(1), LedgerCategory.BeerSales, Money.FromWhole(10), "s1");
            ledger.Credit(D(5), LedgerCategory.BeerSales, Money.FromWhole(20), "s2");
            ledger.Credit(D(9), LedgerCategory.BeerSales, Money.FromWhole(40), "s3");
            ledger.ForceDebit(D(5), LedgerCategory.Wages, Money.FromWhole(7), "w1");

            ledger.TotalFor(LedgerCategory.BeerSales, D(1), D(5))
                  .Should().Be(Money.FromWhole(30));

            ledger.TotalFor(LedgerCategory.Wages, D(1), D(9))
                  .Should().Be(Money.FromWhole(-7));
        }

        [Fact]
        public void EntriesForPeriod_Returns_Only_In_Range()
        {
            var ledger = new Ledger();
            ledger.Credit(D(1), LedgerCategory.BeerSales, Money.FromWhole(1), "a");
            ledger.Credit(D(10), LedgerCategory.BeerSales, Money.FromWhole(1), "b");
            ledger.Credit(D(20), LedgerCategory.BeerSales, Money.FromWhole(1), "c");

            ledger.EntriesForPeriod(D(5), D(15)).Should().ContainSingle()
                  .Which.Description.Should().Be("b");
        }

        [Fact]
        public void Entries_Carry_Node_Attribution()
        {
            var ledger = new Ledger();
            ledger.ForceDebit(D(0), LedgerCategory.Upkeep, Money.FromWhole(5),
                              "upkeep", "brewery-burton");

            ledger.Entries.Single().NodeId.Should().Be("brewery-burton");
        }

        /// <summary>
        /// THE CORE INVARIANT. If this ever fails, money is being created or
        /// destroyed outside the ledger and the economy is unauditable.
        /// </summary>
        [Fact]
        public void Balance_Always_Equals_Sum_Of_Entries()
        {
            var ledger = new Ledger(Money.FromWhole(1000), D(0));
            var rng = new DeterministicRandom(4242);

            for (int day = 1; day <= 1000; day++)
            {
                var date = D(day);
                ledger.Credit(date, LedgerCategory.BeerSales,
                              Money.FromCents(rng.NextInt(1, 5000)), "sale");
                ledger.ForceDebit(date, LedgerCategory.Upkeep,
                                  Money.FromCents(rng.NextInt(1, 3000)), "upkeep");

                if (rng.Chance(2000))
                {
                    ledger.TryDebit(date, LedgerCategory.IngredientPurchase,
                                    Money.FromCents(rng.NextInt(1, 8000)), "malt");
                }
            }

            ledger.Balance.Should().Be(ledger.SumOfAllEntries());
        }

        [Fact]
        public void CompactHistory_Preserves_Balance_Exactly()
        {
            var ledger = new Ledger(Money.FromWhole(1000), D(0));
            var rng = new DeterministicRandom(99);

            for (int day = 1; day <= 1200; day++)
            {
                ledger.Credit(D(day), LedgerCategory.BeerSales,
                              Money.FromCents(rng.NextInt(1, 500)), "sale");
                ledger.ForceDebit(D(day), LedgerCategory.Wages,
                                  Money.FromCents(rng.NextInt(1, 300)), "wages");
            }

            var before = ledger.Balance;
            int entriesBefore = ledger.Entries.Count;

            ledger.CompactHistory(D(1200));

            ledger.Balance.Should().Be(before);
            ledger.SumOfAllEntries().Should().Be(before);
            ledger.Entries.Count.Should().BeLessThan(entriesBefore);
        }

        [Fact]
        public void CompactHistory_Is_Idempotent()
        {
            var ledger = new Ledger(Money.FromWhole(100), D(0));
            for (int day = 1; day <= 1000; day++)
                ledger.Credit(D(day), LedgerCategory.BeerSales, Money.FromCents(10), "s");

            ledger.CompactHistory(D(1000));
            int afterFirst = ledger.Entries.Count;
            var balanceAfterFirst = ledger.Balance;

            ledger.CompactHistory(D(1000));

            ledger.Entries.Count.Should().Be(afterFirst);
            ledger.Balance.Should().Be(balanceAfterFirst);
        }

        [Fact]
        public void CompactHistory_Keeps_Recent_Detail()
        {
            var ledger = new Ledger();
            ledger.RetentionDays = 100;

            for (int day = 0; day <= 300; day++)
                ledger.Credit(D(day), LedgerCategory.BeerSales, Money.FromCents(1), "sale " + day);

            ledger.CompactHistory(D(300));

            // Entries inside the retention window keep their original description.
            ledger.Entries.Should().Contain(e => e.Description == "sale 300");
            ledger.Entries.Should().NotContain(e => e.Description == "sale 10");
        }
    }
}
