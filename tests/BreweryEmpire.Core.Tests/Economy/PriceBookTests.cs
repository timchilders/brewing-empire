using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Economy
{
    public class PriceBookTests
    {
        [Fact]
        public void Excise_Duty_Rises_Across_Eras()
        {
            var pb = new PriceBook();

            var pre = pb.ExciseDutyPerLitre(Era.PreIndustrial);
            var ind = pb.ExciseDutyPerLitre(Era.Industrial);
            var sci = pb.ExciseDutyPerLitre(Era.Scientific);
            var mod = pb.ExciseDutyPerLitre(Era.Modern);

            ind.Should().BeGreaterThan(pre);
            sci.Should().BeGreaterThan(ind);
            mod.Should().BeGreaterThan(sci);
        }

        [Fact]
        public void Excise_Duty_Modifier_Scales_Duty()
        {
            var pb = new PriceBook();
            var normal = pb.ExciseDutyPerLitre(Era.Industrial);

            pb.ExciseDutyModifierBasisPoints = 20000;   // wartime doubling
            pb.ExciseDutyPerLitre(Era.Industrial).Should().Be(normal * 2);
        }

        [Fact]
        public void Ingredient_Price_Round_Trips()
        {
            var pb = new PriceBook();
            pb.SetIngredientPrice("pale-malt", Money.FromCents(120));
            pb.IngredientPricePerKg("pale-malt").Should().Be(Money.FromCents(120));
        }

        [Fact]
        public void Unknown_Ingredient_Falls_Back_To_Default_Price()
        {
            var pb = new PriceBook();
            pb.IngredientPricePerKg("nonexistent").Should().Be(Money.FromCents(50));
        }

        [Fact]
        public void Base_Wage_Round_Trips()
        {
            var pb = new PriceBook();
            pb.SetBaseWage(1, Money.FromWhole(45));
            pb.BaseWage(1).Should().Be(Money.FromWhole(45));
        }

        [Fact]
        public void Excise_Duty_Override_Applies()
        {
            var pb = new PriceBook();
            pb.SetExciseDutyPerLitre(Era.PreIndustrial, Money.FromCents(7));
            pb.ExciseDutyPerLitre(Era.PreIndustrial).Should().Be(Money.FromCents(7));
        }
    }
}
