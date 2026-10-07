using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Recipes
{
    public class RecipeCatalogTests
    {
        private static readonly IngredientCatalog Catalog = IngredientCatalog.CreateDefault();

        [Fact]
        public void Pale_Ale_Is_Brewable_And_Pale_Ale_Style()
        {
            var r = RecipeCatalog.PaleAle();
            r.Style.Should().Be(BeerStyle.PaleAle);
            r.HasSufficientDiastaticPower(Catalog).Should().BeTrue();
        }

        [Fact]
        public void Porter_Is_Dark_And_Brewable()
        {
            var r = RecipeCatalog.Porter();
            r.Style.Should().Be(BeerStyle.Porter);
            r.HasSufficientDiastaticPower(Catalog).Should().BeTrue();
        }

        [Fact]
        public void Pilsner_Uses_Lager_Yeast_And_Is_Brewable()
        {
            var r = RecipeCatalog.Pilsner();
            r.Style.Should().Be(BeerStyle.Pilsner);
            r.YeastIngredientId.Should().Be("lager-yeast");
            r.HasSufficientDiastaticPower(Catalog).Should().BeTrue();
        }
    }
}
