using System.Linq;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Scenario;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Scenario
{
    public class ScenarioAssemblerTests
    {
        [Fact]
        public void Burton_Scenario_Builds_A_Populated_World()
        {
            var s = ScenarioAssembler.Assemble(42, ScenarioCatalog.Burton1750);

            s.World.NodeCount.Should().Be(2);
            s.World.Get(new NodeId("burton")).Vessels.Count.Should().Be(3);
            s.Markets.Count.Should().Be(2);
            s.Staff.All.Count().Should().Be(1);
            s.Rivals.Count.Should().Be(1);
            s.World.HasRoute(new NodeId("burton"), new NodeId("london")).Should().BeTrue();
        }

        [Fact]
        public void Same_Seed_And_Scenario_Are_Identical()
        {
            var a = ScenarioAssembler.Assemble(7, ScenarioCatalog.Burton1750);
            var b = ScenarioAssembler.Assemble(7, ScenarioCatalog.Burton1750);
            TestScenario.Fingerprint(a).Should().Be(TestScenario.Fingerprint(b));
        }
    }
}
