using System.Linq;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Scenario
{
    public class ScenarioDefinitionTests
    {
        [Fact]
        public void Definition_Holds_Its_Collections_In_Order()
        {
            var d = new ScenarioDefinition
            {
                Id = "x",
                Sites =
                {
                    new ScenarioSite { Id = "a" },
                    new ScenarioSite { Id = "b" }
                }
            };
            d.Sites.Select(s => s.Id).Should().Equal("a", "b");
        }
    }
}
