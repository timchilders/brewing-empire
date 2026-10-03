using System.Linq;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Research;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Research
{
    public class TechCatalogTests
    {
        [Fact]
        public void Every_Node_Has_Unique_Id()
        {
            TechCatalog.All.Select(t => t.Id).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void AvailableIn_Filters_By_Era()
        {
            var in1750 = TechCatalog.AvailableIn(GameDate.FromYearMonthDay(1750, 6, 1)).Select(t => t.Id).ToList();
            in1750.Should().NotContain("saccharometer");

            var in1900 = TechCatalog.AvailableIn(GameDate.FromYearMonthDay(1900, 6, 1)).Select(t => t.Id).ToList();
            in1900.Should().Contain("pure-yeast");
        }

        [Fact]
        public void Prerequisites_Reference_Known_Ids()
        {
            var ids = TechCatalog.All.Select(t => t.Id).ToHashSet();
            foreach (var t in TechCatalog.All)
                foreach (var p in t.PrerequisiteIds)
                    ids.Should().Contain(p, "every prerequisite must name a real tech");
        }

        [Fact]
        public void Get_Unknown_Returns_Null()
        {
            TechCatalog.Get("nope").Should().BeNull();
        }

        [Fact]
        public void RequiredTechFor_Maps_Styles()
        {
            TechCatalog.RequiredTechFor(BeerStyle.PaleAle).Should().BeNull();
            TechCatalog.RequiredTechFor(BeerStyle.Porter).Should().Be("malting-kilns");
            TechCatalog.RequiredTechFor(BeerStyle.Pilsner).Should().Be("pale-revolution");
        }

        [Fact]
        public void TargetWaterFor_Maps_Styles()
        {
            TechCatalog.TargetWaterFor(BeerStyle.PaleAle).Should().Be(WaterProfile.Burton);
            TechCatalog.TargetWaterFor(BeerStyle.Stout).Should().Be(WaterProfile.Dublin);
        }
    }
}
