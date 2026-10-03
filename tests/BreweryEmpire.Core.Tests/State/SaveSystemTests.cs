using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Events;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Simulation;
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

        [Fact]
        public void Shipments_In_Transit_Round_Trip()
        {
            var s = TestScenario.Standard();
            var r = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            var batch = r.Batch!;
            batch.MarkReady();

            var sh = new Shipment(new ShipmentId("s1"), batch.Id,
                new NodeId("burton"), new NodeId("london"), 5, PackagingType.WoodenCask, 100)
            {
                Mode = TransportMode.SteamRail,
                IsRefrigerated = true,
                Cargo = batch
            };
            s.Shipments.Add(sh);

            var reloaded = SaveSystem.Load(SaveSystem.Save(s));
            reloaded.Shipments.Should().ContainSingle(x => x.Id == sh.Id);
            var sh2 = reloaded.Shipments[0];
            sh2.Mode.Should().Be(TransportMode.SteamRail);
            sh2.IsRefrigerated.Should().BeTrue();
            sh2.Cargo.Should().NotBeNull();
            sh2.Cargo!.Style.Should().Be(batch.Style);
            sh2.Cargo.QualityBasisPoints.Should().Be(batch.QualityBasisPoints);
        }

        [Fact]
        public void Version_3_Save_Loads_With_No_Shipments()
        {
            var s = TestScenario.Standard();
            var json = SaveSystem.Save(s).Replace("\"SaveVersion\":4", "\"SaveVersion\":3");
            var reloaded = SaveSystem.Load(json);
            reloaded.Shipments.Should().BeEmpty();
        }
    }
}
