using BreweryEmpire.Core.Model.Events;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.State
{
    public class SaveSystemTests
    {
        [Fact]
        public void Research_State_Round_Trips()
        {
            var s = TestScenario.Standard();
            s.Research.UnlockedTechIds.Add("malting-kilns");
            s.Research.ActiveTechId = "saccharometer";
            s.Research.ActiveProgressPoints = 77;
            s.PrestigeBasisPoints = 1234;
            s.PendingEvents.Add(new GameEvent { Id = "e1", Title = "T" });
            s.ResolvedEventIds.Add("e0");

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));

            reloaded.Research.UnlockedTechIds.Should().Equal("malting-kilns");
            reloaded.Research.ActiveTechId.Should().Be("saccharometer");
            reloaded.Research.ActiveProgressPoints.Should().Be(77);
            reloaded.PrestigeBasisPoints.Should().Be(1234);
            reloaded.PendingEvents.Should().ContainSingle(e => e.Id == "e1");
            reloaded.ResolvedEventIds.Should().Equal("e0");
        }

        [Fact]
        public void Minimal_Version_2_Save_Loads_With_Empty_Research()
        {
            // A v2 save has none of the Phase 3 fields; the loader must default them.
            const string json = "{\"SaveVersion\":2,\"Seed\":42,\"DateTotalDays\":0,\"RandomState\":null," +
                "\"ReputationBasisPoints\":5000,\"IsBankrupt\":false,\"NextEntityNumber\":1,\"LedgerBalanceCents\":0," +
                "\"LedgerEntries\":[],\"Nodes\":[],\"Staff\":[],\"Recipes\":[],\"Markets\":[],\"Rivals\":null}";

            var reloaded = SaveSystem.Load(json);

            reloaded.Research.UnlockedTechIds.Should().BeEmpty();
            reloaded.Research.ActiveTechId.Should().BeNull();
            reloaded.PrestigeBasisPoints.Should().Be(0);
            reloaded.PendingEvents.Should().BeEmpty();
        }
    }
}
