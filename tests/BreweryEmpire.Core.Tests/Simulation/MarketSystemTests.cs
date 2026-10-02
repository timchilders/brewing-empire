using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Markets;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Simulation
{
    public class MarketSystemTests
    {
        private static GameState WithMarket()
        {
            var s = TestScenario.Standard();
            s.Markets.Add(MarketNode.Create("burton-market", "Burton", new NodeId("burton"),
                RegionClimate.BurtonEngland, population: 10_000));
            s.Markets[0].SetBaseDemand(BeerStyle.PaleAle, 10_000);
            return s;
        }

        private static Batch BrewReady(GameState s)
        {
            var res = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            res.Success.Should().BeTrue();
            var batch = res.Batch!;
            TickSystem.AdvanceDays(s, s.Date.DaysUntil(batch.ReadyOn));
            return batch;
        }

        // ---- C1: Market node ----

        [Fact]
        public void Market_Reputation_Clamps()
        {
            var m = MarketNode.Create("m", "M", new NodeId("n"), RegionClimate.BurtonEngland, 1000);
            m.ReputationBasisPoints.Should().Be(5000);

            m.AdjustReputation(10_000);
            m.ReputationBasisPoints.Should().Be(10000);

            m.AdjustReputation(-99_999);
            m.ReputationBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Unknown_Style_Has_Zero_Base_Demand()
        {
            var m = MarketNode.Create("m", "M", new NodeId("n"), RegionClimate.BurtonEngland, 1000);
            m.BaseDemandLitresPerTick(BeerStyle.Porter).Should().Be(0);
        }

        // ---- C2: Dynamic demand ----

        [Fact]
        public void Summer_Raises_Demand_For_A_Refreshing_Style()
        {
            var m = MarketNode.Create("m", "M", new NodeId("n"), RegionClimate.BurtonEngland, 10_000);
            m.SetBaseDemand(BeerStyle.PaleAle, 100);

            var jan = GameDate.FromYearMonthDay(1750, 1, 15);
            var jul = GameDate.FromYearMonthDay(1750, 7, 15);

            m.EffectiveDemandLitres(BeerStyle.PaleAle, jul)
                .Should().BeGreaterThan(m.EffectiveDemandLitres(BeerStyle.PaleAle, jan));
        }

        [Fact]
        public void Larger_Population_Drives_More_Demand()
        {
            var small = MarketNode.Create("s", "S", new NodeId("n"), RegionClimate.BurtonEngland, 5_000);
            var big = MarketNode.Create("b", "B", new NodeId("n"), RegionClimate.BurtonEngland, 40_000);
            small.SetBaseDemand(BeerStyle.PaleAle, 100);
            big.SetBaseDemand(BeerStyle.PaleAle, 100);

            var date = GameDate.FromYearMonthDay(1750, 7, 15);
            big.EffectiveDemandLitres(BeerStyle.PaleAle, date)
                .Should().BeGreaterThan(small.EffectiveDemandLitres(BeerStyle.PaleAle, date));
        }

        [Fact]
        public void Higher_Reputation_Drives_More_Demand()
        {
            var m = MarketNode.Create("m", "M", new NodeId("n"), RegionClimate.BurtonEngland, 10_000);
            m.SetBaseDemand(BeerStyle.PaleAle, 100);
            var date = GameDate.FromYearMonthDay(1750, 7, 15);

            int lowRep = m.EffectiveDemandLitres(BeerStyle.PaleAle, date);

            m.AdjustReputation(4000);   // 5000 -> 9000
            int highRep = m.EffectiveDemandLitres(BeerStyle.PaleAle, date);

            highRep.Should().BeGreaterThan(lowRep);
        }

        [Fact]
        public void Demand_Is_Continuous_Across_The_Year_Boundary()
        {
            var m = MarketNode.Create("m", "M", new NodeId("n"), RegionClimate.BurtonEngland, 10_000);
            m.SetBaseDemand(BeerStyle.PaleAle, 100);

            var dec = GameDate.FromYearMonthDay(1750, 12, 31);
            var jan = GameDate.FromYearMonthDay(1751, 1, 1);

            int d = m.EffectiveDemandLitres(BeerStyle.PaleAle, dec);
            int j = m.EffectiveDemandLitres(BeerStyle.PaleAle, jan);
            Math.Abs(d - j).Should().BeLessThan(5);
        }

        // ---- C3: Flavor-fit pricing ----

        [Fact]
        public void Market_Fit_Is_Symmetric_And_Perfect_On_Exact_Match()
        {
            var p = new FlavorProfile { BitternessBasisPoints = 4000, BodyBasisPoints = 3000 };
            p.MarketFitBasisPoints(p).Should().Be(10000);

            var q = new FlavorProfile { BitternessBasisPoints = 1000, BodyBasisPoints = 7000 };
            p.MarketFitBasisPoints(q).Should().Be(q.MarketFitBasisPoints(p));
        }

        [Fact]
        public void Higher_Fit_Realizes_Higher_Price()
        {
            var s = WithMarket();
            var batch = BrewReady(s);
            var market = s.Markets[0];
            market.SetBaseDemand(BeerStyle.PaleAle, 10_000);

            // Market preference exactly matches the batch -> high fit.
            market.PreferredProfile = batch.Flavor with { };
            int matchedPrice = MarketSystem.PricePerLitreBasisPoints(s, market, batch);

            // A market wanting the opposite of everything -> low fit.
            market.PreferredProfile = new FlavorProfile
            {
                BitternessBasisPoints = 9500, SweetnessBasisPoints = 9500,
                MaltinessBasisPoints = 9500, HopAromaBasisPoints = 9500,
                BodyBasisPoints = 9500, AlcoholBasisPoints = 9500
            };
            int mismatchedPrice = MarketSystem.PricePerLitreBasisPoints(s, market, batch);

            matchedPrice.Should().BeGreaterThan(mismatchedPrice);
        }

        [Fact]
        public void Salesman_Bonus_Raises_Price()
        {
            var s = WithMarket();
            var batch = BrewReady(s);
            var market = s.Markets[0];
            market.SetBaseDemand(BeerStyle.PaleAle, 10_000);

            int before = MarketSystem.PricePerLitreBasisPoints(s, market, batch);

            var salesman = new BreweryEmpire.Core.Model.Staff.StaffMember(
                new StaffId("sales-1"), "A. Busch", BreweryEmpire.Core.Model.Staff.StaffRole.Salesman,
                6000, Money.FromWhole(20), 40, s.Date);
            salesman.AssignTo("burton");
            s.Staff.Hire(salesman, s.Date);

            // Give him the price-realization trait directly.
            var trait = new BreweryEmpire.Core.Model.Staff.StaffTrait
            {
                Id = "sales", DisplayName = "Sales", AppliesTo = BreweryEmpire.Core.Model.Staff.StaffRole.Salesman,
                Effect = BreweryEmpire.Core.Model.Staff.TraitEffect.PriceRealization, MagnitudeBasisPoints = 1000
            };
            salesman.Traits = new[] { trait };

            int after = MarketSystem.PricePerLitreBasisPoints(s, market, batch);

            after.Should().BeGreaterThan(before);
        }

        // ---- C4: MarketSystem sells ----

        private static Batch BrewAndMark(GameState s, BatchState state)
        {
            var res = BrewingSystem.TryStartBrew(s, new NodeId("burton"), new RecipeId("pale-ale"));
            res.Success.Should().BeTrue();
            var batch = res.Batch!;
            if (state == BatchState.Ready) batch.MarkReady();
            else if (state == BatchState.Spoiled) batch.MarkSpoiled();
            return batch;
        }

        [Fact]
        public void A_Sale_Credits_The_Ledger_And_Reduces_Volume()
        {
            var s = WithMarket();
            var batch = BrewAndMark(s, BatchState.Ready);
            batch.VolumeLitres.Should().BeGreaterThan(0);

            long balanceBefore = s.Ledger.Balance.Cents;
            MarketSystem.ProcessMarkets(s);

            s.Ledger.Balance.Cents.Should().BeGreaterThan(balanceBefore);
            s.World.Get(new NodeId("burton")).Batches.Should().NotContain(b => b.Id == batch.Id,
                "the batch should be fully sold and removed");
        }

        [Fact]
        public void Excise_Duty_Is_Charged_On_Sales()
        {
            var s = WithMarket();
            BrewAndMark(s, BatchState.Ready);

            MarketSystem.ProcessMarkets(s);

            s.Ledger.Entries.Should().Contain(e => e.Category == LedgerCategory.ExciseDuty);
        }

        [Fact]
        public void Spoiled_Delivery_Damages_Market_Reputation()
        {
            var s = WithMarket();
            BrewAndMark(s, BatchState.Spoiled);
            var market = s.Markets[0];
            int before = market.ReputationBasisPoints;

            MarketSystem.ProcessMarkets(s);

            market.ReputationBasisPoints.Should().BeLessThan(before);
        }
    }
}
