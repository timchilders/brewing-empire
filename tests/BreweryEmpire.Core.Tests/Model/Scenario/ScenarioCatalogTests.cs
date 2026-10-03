using System.Linq;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Scenario
{
    public class ScenarioCatalogTests
    {
        [Fact]
        public void Catalog_Has_At_Least_Two_Scenarios()
        {
            ScenarioCatalog.All.Count.Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public void London_1890_Has_A_Rail_Route()
        {
            var l = ScenarioCatalog.London1890;
            l.Routes.Should().Contain(r => r.Mode == TransportMode.SteamRail);
        }

        [Fact]
        public void Ids_Are_Unique()
        {
            ScenarioCatalog.All.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        }
    }
}
