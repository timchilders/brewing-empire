using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Economy
{
    public enum LedgerCategory
    {
        IngredientPurchase = 0,
        Wages = 1,
        Upkeep = 2,
        CapitalExpenditure = 3,
        TransportCost = 4,
        ExciseDuty = 5,
        LoanPrincipal = 6,
        LoanInterest = 7,
        BeerSales = 8,
        ByproductSales = 9,
        SpoilageWriteOff = 10,
        EventCost = 11,
        MonthlySummary = 12
    }

    public sealed class LedgerEntry
    {
        public GameDate Date { get; set; }
        public LedgerCategory Category { get; set; }

        /// <summary>Signed: positive is a credit, negative is a debit.</summary>
        public Money Amount { get; set; }

        public string Description { get; set; } = string.Empty;
        public string? NodeId { get; set; }

        public LedgerEntry() { }

        public LedgerEntry(GameDate date, LedgerCategory category, Money amount,
                           string description, string? nodeId = null)
        {
            Date = date;
            Category = category;
            Amount = amount;
            Description = description;
            NodeId = nodeId;
        }
    }

    /// <summary>
    /// The single source of truth for money.
    ///
    /// CORE INVARIANT: Balance always equals the sum of every entry ever posted
    /// (including rolled-up summaries). Nothing may mutate Balance directly —
    /// every change goes through Post/TryDebit so the player can always be
    /// shown *why* they are broke. A test asserts this invariant after 1000 ticks.
    ///
    /// History is bounded: entries older than RetentionDays collapse into one
    /// MonthlySummary entry per category per month. Without this a 100-year
    /// campaign would grow an unbounded save file.
    /// </summary>
    public sealed class Ledger
    {
        public const int DefaultRetentionDays = 730;   // two years of detail

        private readonly List<LedgerEntry> _entries = new List<LedgerEntry>();

        public Ledger() { }

        public Ledger(Money openingBalance, GameDate date)
        {
            if (!openingBalance.IsZero)
                Post(date, LedgerCategory.CapitalExpenditure, openingBalance, "Opening capital");
        }

        public int RetentionDays { get; set; } = DefaultRetentionDays;

        public Money Balance { get; private set; } = Money.Zero;

        public IReadOnlyList<LedgerEntry> Entries => _entries;

        /// <summary>Post a signed amount. Positive credits, negative debits.</summary>
        public void Post(GameDate date, LedgerCategory category, Money amount,
                         string description, string? nodeId = null)
        {
            if (amount.IsZero) return;

            _entries.Add(new LedgerEntry(date, category, amount, description, nodeId));
            Balance += amount;
        }

        /// <summary>
        /// Attempt to spend. Returns false and posts NOTHING if funds are short —
        /// callers must treat a false return as "the action did not happen" and
        /// avoid any partial state mutation.
        /// </summary>
        public bool TryDebit(GameDate date, LedgerCategory category, Money amount,
                             string description, string? nodeId = null)
        {
            if (amount.IsNegative)
                throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be non-negative.");

            if (amount.IsZero) return true;
            if (Balance < amount) return false;

            Post(date, category, amount.Negated(), description, nodeId);
            return true;
        }

        /// <summary>Spend even if it drives the balance negative (interest, duty, wages).</summary>
        public void ForceDebit(GameDate date, LedgerCategory category, Money amount,
                               string description, string? nodeId = null)
        {
            if (amount.IsNegative)
                throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be non-negative.");
            Post(date, category, amount.Negated(), description, nodeId);
        }

        public void Credit(GameDate date, LedgerCategory category, Money amount,
                           string description, string? nodeId = null)
        {
            if (amount.IsNegative)
                throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be non-negative.");
            Post(date, category, amount, description, nodeId);
        }

        public Money TotalFor(LedgerCategory category, GameDate from, GameDate to)
        {
            long sum = 0;
            foreach (var e in _entries)
            {
                if (e.Category != category) continue;
                if (e.Date < from || e.Date > to) continue;
                sum += e.Amount.Cents;
            }
            return Money.FromCents(sum);
        }

        public IReadOnlyList<LedgerEntry> EntriesForPeriod(GameDate from, GameDate to) =>
            _entries.Where(e => e.Date >= from && e.Date <= to).ToList();

        /// <summary>Sum of every entry — used to assert the core invariant.</summary>
        public Money SumOfAllEntries()
        {
            long sum = 0;
            foreach (var e in _entries) sum += e.Amount.Cents;
            return Money.FromCents(sum);
        }

        /// <summary>
        /// Collapse entries older than RetentionDays into one summary entry per
        /// (year, month, category). Balance is unchanged by construction.
        /// </summary>
        public void CompactHistory(GameDate now)
        {
            var cutoff = now.AddDays(-RetentionDays);

            var old = _entries.Where(e => e.Date < cutoff && e.Category != LedgerCategory.MonthlySummary).ToList();
            if (old.Count == 0) return;

            var summaries = old
                .GroupBy(e => new { e.Date.Year, e.Date.Month, e.Category })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ThenBy(g => (int)g.Key.Category)
                .Select(g => new LedgerEntry(
                    GameDate.FromYearMonthDay(g.Key.Year, g.Key.Month, 1),
                    LedgerCategory.MonthlySummary,
                    Money.FromCents(g.Sum(e => e.Amount.Cents)),
                    g.Key.Category + " summary " + g.Key.Year.ToString("D4") + "-" + g.Key.Month.ToString("D2")))
                .ToList();

            _entries.RemoveAll(e => e.Date < cutoff && e.Category != LedgerCategory.MonthlySummary);
            _entries.InsertRange(0, summaries);
        }
    }
}
