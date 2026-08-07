using System;
using System.Collections.Generic;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Economy
{
    /// <summary>
    /// Base prices for the economy. Phase 1 gets the plumbing right; these
    /// numbers are placeholders and will be tuned once Phase 2's market system
    /// exists. Every cost in the game resolves through here so balance passes
    /// have a single place to edit.
    /// </summary>
    public sealed class PriceBook
    {
        private readonly Dictionary<string, Money> _ingredientPrices =
            new Dictionary<string, Money>(StringComparer.Ordinal);

        private readonly Dictionary<StaffRoleKey, Money> _baseWages =
            new Dictionary<StaffRoleKey, Money>();

        /// <summary>Excise duty per litre sold, by era. Rises with state capacity.</summary>
        private readonly Dictionary<Era, Money> _exciseDutyPerLitre = new Dictionary<Era, Money>
        {
            { Era.PreIndustrial, Money.FromCents(2) },
            { Era.Industrial,    Money.FromCents(5) },
            { Era.Scientific,    Money.FromCents(9) },
            { Era.Modern,        Money.FromCents(18) }
        };

        /// <summary>Multiplier applied to duty by events (wartime spikes). 10000 = normal.</summary>
        public int ExciseDutyModifierBasisPoints { get; set; } = 10000;

        public void SetIngredientPrice(string ingredientId, Money pricePerKg) =>
            _ingredientPrices[ingredientId] = pricePerKg;

        public Money IngredientPricePerKg(string ingredientId) =>
            _ingredientPrices.TryGetValue(ingredientId, out var p) ? p : Money.FromCents(50);

        public Money ExciseDutyPerLitre(Era era)
        {
            var basis = _exciseDutyPerLitre.TryGetValue(era, out var d) ? d : Money.FromCents(5);
            return basis.PercentBasisPoints(ExciseDutyModifierBasisPoints);
        }

        public void SetExciseDutyPerLitre(Era era, Money amount) => _exciseDutyPerLitre[era] = amount;

        public void SetBaseWage(int roleId, Money monthlyWage) =>
            _baseWages[new StaffRoleKey(roleId)] = monthlyWage;

        public Money BaseWage(int roleId) =>
            _baseWages.TryGetValue(new StaffRoleKey(roleId), out var w) ? w : Money.FromWhole(20);

        private readonly struct StaffRoleKey : IEquatable<StaffRoleKey>
        {
            private readonly int _id;
            public StaffRoleKey(int id) { _id = id; }
            public bool Equals(StaffRoleKey other) => _id == other._id;
            public override bool Equals(object? obj) => obj is StaffRoleKey k && Equals(k);
            public override int GetHashCode() => _id;
        }
    }
}
