using System;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Ingredients
{
    public class IngredientTests
    {
        [Fact]
        public void Roasted_Grain_Has_Zero_Diastatic_Power()
        {
            var c = IngredientCatalog.CreateDefault();
            c.Get("roasted-barley").DiastaticPowerLintner.Should().Be(0);
            c.Get("chocolate-malt").DiastaticPowerLintner.Should().Be(0);
            c.Get("black-patent").DiastaticPowerLintner.Should().Be(0);
            c.Get("crystal-malt").DiastaticPowerLintner.Should().Be(0);
        }

        [Fact]
        public void Base_Malt_Has_Positive_Diastatic_Power()
        {
            var c = IngredientCatalog.CreateDefault();
            c.Get("pale-malt").DiastaticPowerLintner.Should().BePositive();
            c.Get("munich-malt").DiastaticPowerLintner.Should().BePositive();
            c.Get("pale-malt").CanSelfConvert.Should().BeTrue();
            c.Get("roasted-barley").CanSelfConvert.Should().BeFalse();
        }

        [Fact]
        public void Validate_Rejects_Roasted_Grain_With_Enzymes()
        {
            var bad = new Ingredient
            {
                Id = "impossible-malt",
                Type = IngredientType.RoastedGrain,
                DiastaticPowerLintner = 50
            };
            Action act = () => bad.Validate();
            act.Should().Throw<ArgumentException>().WithMessage("*kilning destroys the enzymes*");
        }

        [Fact]
        public void Validate_Rejects_Alpha_Acid_On_Non_Hops()
        {
            var bad = new Ingredient
            {
                Id = "weird-malt", Type = IngredientType.BaseMalt,
                DiastaticPowerLintner = 100, AlphaAcidBasisPoints = 300
            };
            Action act = () => bad.Validate();
            act.Should().Throw<ArgumentException>().WithMessage("*Only hops carry alpha acid*");
        }

        [Fact]
        public void Default_Catalog_Contains_All_Expected_Ingredients()
        {
            var c = IngredientCatalog.CreateDefault();
            var expected = new[]
            {
                "pale-malt", "munich-malt", "crystal-malt", "chocolate-malt",
                "roasted-barley", "black-patent", "goldings-hops", "fuggles-hops",
                "saaz-hops", "ale-yeast", "lager-yeast"
            };
            foreach (var id in expected) c.Contains(id).Should().BeTrue("catalog should have " + id);
            c.Count.Should().Be(expected.Length);
        }

        [Fact]
        public void Unknown_Ingredient_Lookup_Throws()
        {
            var c = IngredientCatalog.CreateDefault();
            Action act = () => c.Get("unobtainium");
            act.Should().Throw<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void Roasted_Grains_Are_Darker_Than_Base_Malts()
        {
            var c = IngredientCatalog.CreateDefault();
            c.Get("roasted-barley").ColorLovibond
             .Should().BeGreaterThan(c.Get("pale-malt").ColorLovibond);
        }
    }

    public class IngredientLotTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);

        private static IngredientLot Lot(string id, string ingredient, int grams, int day,
                                         long unitCents = 100)
            => new IngredientLot(new LotId(id), ingredient, grams, 1750, 10000,
                                 D(day), Money.FromCents(unitCents));

        [Fact]
        public void Negative_Quantity_Throws()
        {
            Action act = () => Lot("l1", "pale-malt", -1, 0);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Removing_More_Than_Available_Throws_And_Leaves_Lot_Unchanged()
        {
            var lot = Lot("l1", "pale-malt", 1000, 0);

            Action act = () => lot.Remove(1500);
            act.Should().Throw<InvalidOperationException>();

            lot.QuantityGrams.Should().Be(1000);
        }

        [Fact]
        public void Remove_Reduces_Quantity()
        {
            var lot = Lot("l1", "pale-malt", 1000, 0);
            lot.Remove(400);
            lot.QuantityGrams.Should().Be(600);
        }

        [Fact]
        public void Hop_Alpha_Acid_Degrades_Monotonically_And_Never_Negative()
        {
            var catalog = IngredientCatalog.CreateDefault();
            var hops = catalog.Get("goldings-hops");
            var lot = Lot("h1", "goldings-hops", 5000, 0);

            int previous = int.MaxValue;
            for (int year = 0; year <= 5; year++)
            {
                int alpha = lot.EffectiveAlphaAcidBasisPoints(hops, D(year * GameDate.DaysPerYear));
                alpha.Should().BeLessOrEqualTo(previous);
                alpha.Should().BeGreaterOrEqualTo(0);
                previous = alpha;
            }
        }

        [Fact]
        public void Fresh_Hops_Retain_Full_Alpha_Acid()
        {
            var catalog = IngredientCatalog.CreateDefault();
            var hops = catalog.Get("goldings-hops");
            var lot = Lot("h1", "goldings-hops", 5000, 0);

            lot.EffectiveAlphaAcidBasisPoints(hops, D(0)).Should().Be(hops.AlphaAcidBasisPoints);
        }

        [Fact]
        public void Non_Hops_Have_No_Alpha_Acid()
        {
            var catalog = IngredientCatalog.CreateDefault();
            var malt = catalog.Get("pale-malt");
            var lot = Lot("m1", "pale-malt", 5000, 0);

            lot.EffectiveAlphaAcidBasisPoints(malt, D(1000)).Should().Be(0);
        }
    }

    public class InventoryTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);

        private static IngredientLot Lot(string id, string ingredient, int grams, int day,
                                         long unitCents = 100)
            => new IngredientLot(new LotId(id), ingredient, grams, 1750, 10000,
                                 D(day), Money.FromCents(unitCents));

        [Fact]
        public void TotalGramsOf_Sums_Across_Lots()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("a", "pale-malt", 1000, 0));
            inv.AddLot(Lot("b", "pale-malt", 500, 1));
            inv.AddLot(Lot("c", "saaz-hops", 200, 2));

            inv.TotalGramsOf("pale-malt").Should().Be(1500);
            inv.TotalGramsOf("saaz-hops").Should().Be(200);
            inv.TotalGramsOf("nonexistent").Should().Be(0);
        }

        [Fact]
        public void HasAtLeast_Is_Correct()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("a", "pale-malt", 1000, 0));

            inv.HasAtLeast("pale-malt", 1000).Should().BeTrue();
            inv.HasAtLeast("pale-malt", 1001).Should().BeFalse();
        }

        [Fact]
        public void Consume_Draws_Oldest_Lot_First_Across_A_Boundary()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("newest", "pale-malt", 1000, 20));
            inv.AddLot(Lot("oldest", "pale-malt", 300, 1));
            inv.AddLot(Lot("middle", "pale-malt", 400, 10));

            // 500g must take all 300 from oldest, then 200 from middle.
            var draws = inv.Consume("pale-malt", 500);

            draws.Should().HaveCount(2);
            draws[0].LotId.Value.Should().Be("oldest");
            draws[0].Grams.Should().Be(300);
            draws[1].LotId.Value.Should().Be("middle");
            draws[1].Grams.Should().Be(200);

            inv.TotalGramsOf("pale-malt").Should().Be(1200);
        }

        [Fact]
        public void Fifo_Tie_Break_By_LotId_Is_Deterministic()
        {
            for (int run = 0; run < 5; run++)
            {
                var inv = new Inventory();
                // Same acquisition date - must break by LotId ordinal.
                inv.AddLot(Lot("zebra", "pale-malt", 100, 5));
                inv.AddLot(Lot("alpha", "pale-malt", 100, 5));
                inv.AddLot(Lot("mango", "pale-malt", 100, 5));

                var draws = inv.Consume("pale-malt", 150);

                draws[0].LotId.Value.Should().Be("alpha");
                draws[1].LotId.Value.Should().Be("mango");
            }
        }

        [Fact]
        public void Consume_Beyond_Stock_Throws_And_Mutates_Nothing()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("a", "pale-malt", 100, 0));

            Action act = () => inv.Consume("pale-malt", 500);
            act.Should().Throw<InvalidOperationException>();

            inv.TotalGramsOf("pale-malt").Should().Be(100);
            inv.Lots.Should().HaveCount(1);
        }

        [Fact]
        public void Emptied_Lots_Are_Removed()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("a", "pale-malt", 100, 0));
            inv.AddLot(Lot("b", "pale-malt", 100, 1));

            inv.Consume("pale-malt", 100);

            inv.Lots.Should().HaveCount(1);
            inv.Lots.Single().Id.Value.Should().Be("b");
        }

        [Fact]
        public void Consume_Zero_Is_A_Noop()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("a", "pale-malt", 100, 0));

            inv.Consume("pale-malt", 0).Should().BeEmpty();
            inv.TotalGramsOf("pale-malt").Should().Be(100);
        }

        [Fact]
        public void CostOfConsumption_Computes_Cogs_From_Draws()
        {
            var inv = new Inventory();
            // 100 cents/kg -> 1000g costs 100 cents
            inv.AddLot(Lot("a", "pale-malt", 1000, 0, unitCents: 100));
            // 200 cents/kg -> 1000g costs 200 cents
            inv.AddLot(Lot("b", "pale-malt", 1000, 1, unitCents: 200));

            var draws = inv.Consume("pale-malt", 2000);
            Inventory.CostOfConsumption(draws).Should().Be(Money.FromCents(300));
        }

        [Fact]
        public void Cogs_Reflects_Fifo_Ordering_Not_Average()
        {
            var inv = new Inventory();
            inv.AddLot(Lot("cheap", "pale-malt", 1000, 0, unitCents: 100));
            inv.AddLot(Lot("dear", "pale-malt", 1000, 5, unitCents: 900));

            // Only the cheap (older) lot should be drawn.
            var draws = inv.Consume("pale-malt", 1000);
            Inventory.CostOfConsumption(draws).Should().Be(Money.FromCents(100));
        }
    }
}
