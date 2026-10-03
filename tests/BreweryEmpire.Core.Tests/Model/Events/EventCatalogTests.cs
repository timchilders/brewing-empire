using System.Linq;
using BreweryEmpire.Core.Model.Events;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Events
{
    public class EventCatalogTests
    {
        [Fact]
        public void Every_Event_Has_At_Least_Two_Choices()
        {
            foreach (var e in EventCatalog.All)
                e.Choices.Count.Should().BeGreaterThanOrEqualTo(2, e.Id + " must be a real choice");
        }

        [Fact]
        public void Ids_Are_Unique()
        {
            EventCatalog.All.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        }
    }
}
