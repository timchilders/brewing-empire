using BreweryEmpire.Core.Model.Research;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Research
{
    public class TechNodeTests
    {
        [Fact]
        public void Prerequisites_Default_Empty_Not_Null()
        {
            var n = new TechNode { Id = "x", DisplayName = "X" };
            n.PrerequisiteIds.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void PrerequisiteIds_Are_Preserved_In_Order()
        {
            var n = new TechNode { Id = "y", PrerequisiteIds = new[] { "a", "b" } };
            n.PrerequisiteIds.Should().Equal("a", "b");
        }

        [Fact]
        public void MinEra_Defaults_To_PreIndustrial()
        {
            new TechNode().MinEra.Should().Be(Era.PreIndustrial);
        }
    }
}
