using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.State;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Brewing
{
    public class FlavorProfileTests
    {
        [Fact]
        public void Identical_Profiles_Match_Perfectly()
        {
            var p = new FlavorProfile
            {
                BitternessBasisPoints = 4000, SweetnessBasisPoints = 3000,
                MaltinessBasisPoints = 5000, HopAromaBasisPoints = 2000,
                BodyBasisPoints = 4500, AlcoholBasisPoints = 500
            };

            p.MatchScoreBasisPoints(p).Should().Be(10000);
        }

        [Fact]
        public void Opposite_Profiles_Score_Poorly()
        {
            var hoppy = new FlavorProfile { BitternessBasisPoints = 9000, HopAromaBasisPoints = 9000 };
            var malty = new FlavorProfile { MaltinessBasisPoints = 9000, SweetnessBasisPoints = 9000 };

            hoppy.MatchScoreBasisPoints(malty).Should().BeLessThan(5000);
        }

        [Fact]
        public void Match_Is_Symmetric()
        {
            var a = new FlavorProfile { BitternessBasisPoints = 6000, BodyBasisPoints = 3000 };
            var b = new FlavorProfile { BitternessBasisPoints = 2000, BodyBasisPoints = 7000 };

            a.MatchScoreBasisPoints(b).Should().Be(b.MatchScoreBasisPoints(a));
        }

        [Fact]
        public void Match_Clamps_At_Zero()
        {
            var extreme = new FlavorProfile
            {
                BitternessBasisPoints = 10000, SweetnessBasisPoints = 10000,
                MaltinessBasisPoints = 10000, HopAromaBasisPoints = 10000,
                BodyBasisPoints = 10000, AlcoholBasisPoints = 10000
            };

            extreme.MatchScoreBasisPoints(new FlavorProfile()).Should().Be(0);
        }

        [Fact]
        public void Null_Target_Is_Rejected()
        {
            Action act = () => new FlavorProfile().MatchScoreBasisPoints(null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }

    public class BatchTests
    {
        private static GameDate D(int day) => GameDate.FromTotalDays(day);

        private static Batch NewBatch(int litres = 1000) =>
            new Batch(new BatchId("b1"), new RecipeId("pale-ale"), "brewery-1",
                      new VesselId("v1"), litres, D(0), D(21));

        [Fact]
        public void New_Batch_Starts_Fermenting_And_Is_Not_Sellable()
        {
            var b = NewBatch();
            b.State.Should().Be(BatchState.Fermenting);
            b.IsSellable.Should().BeFalse();
        }

        [Fact]
        public void Non_Positive_Volume_Throws()
        {
            Action act = () => new Batch(new BatchId("b"), new RecipeId("r"), "n",
                                         new VesselId("v"), 0, D(0), D(1));
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Ready_Batch_Is_Sellable()
        {
            var b = NewBatch();
            b.MarkReady();

            b.State.Should().Be(BatchState.Ready);
            b.IsSellable.Should().BeTrue();
        }

        [Fact]
        public void Spoiled_Batch_Can_Never_Become_Ready()
        {
            var b = NewBatch();
            b.MarkSpoiled();

            Action act = () => b.MarkReady();
            act.Should().Throw<InvalidOperationException>();
            b.IsSellable.Should().BeFalse();
        }

        [Fact]
        public void Severe_Infection_Spoils_The_Batch()
        {
            var b = NewBatch();
            b.AddInfection(new Infection
            {
                Character = OffFlavor.Sour,
                SeverityBasisPoints = 6000,
                Cause = "Dirty wooden fermenter"
            });

            b.State.Should().Be(BatchState.Spoiled);
        }

        [Fact]
        public void Mild_Infection_Only_Degrades_Quality()
        {
            var b = NewBatch();
            b.SetQuality(8000);
            b.AddInfection(new Infection
            {
                Character = OffFlavor.Diacetyl,
                SeverityBasisPoints = 1500,
                Cause = "Rushed fermentation"
            });

            b.State.Should().Be(BatchState.Fermenting);
            b.QualityBasisPoints.Should().Be(6500);
            b.Infections.Should().ContainSingle();
        }

        [Fact]
        public void Infections_Accumulate()
        {
            var b = NewBatch();
            b.SetQuality(9000);
            b.AddInfection(new Infection { Character = OffFlavor.Diacetyl, SeverityBasisPoints = 1000 });
            b.AddInfection(new Infection { Character = OffFlavor.Sulfur, SeverityBasisPoints = 500 });

            b.Infections.Should().HaveCount(2);
            b.QualityBasisPoints.Should().Be(7500);
        }

        [Fact]
        public void Quality_Clamps_To_Valid_Range()
        {
            var b = NewBatch();
            b.SetQuality(99999);
            b.QualityBasisPoints.Should().Be(10000);

            b.SetQuality(-500);
            b.QualityBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Removing_Beer_Reduces_Volume()
        {
            var b = NewBatch(1000);
            b.Remove(400);
            b.VolumeLitres.Should().Be(600);
        }

        [Fact]
        public void Removing_More_Than_Available_Throws_And_Preserves_Volume()
        {
            var b = NewBatch(100);
            Action act = () => b.Remove(500);

            act.Should().Throw<InvalidOperationException>();
            b.VolumeLitres.Should().Be(100);
        }

        [Fact]
        public void Loss_Basis_Points_Reduce_Volume_Proportionally()
        {
            var b = NewBatch(1000);
            b.ApplyLossBasisPoints(500);   // 5%
            b.VolumeLitres.Should().Be(950);
        }

        [Fact]
        public void Loss_Never_Drives_Volume_Negative()
        {
            var b = NewBatch(10);
            b.ApplyLossBasisPoints(10000);
            b.VolumeLitres.Should().Be(0);
        }

        [Fact]
        public void Empty_Batch_Is_Not_Sellable()
        {
            var b = NewBatch(100);
            b.MarkReady();
            b.Remove(100);

            b.IsSellable.Should().BeFalse();
        }

        [Fact]
        public void Cost_Per_Litre_Is_Derived_From_Cogs()
        {
            var b = NewBatch(1000);
            b.CostOfGoods = Money.FromCents(50_000);

            b.CostPerLitre.Should().Be(Money.FromCents(50));
        }

        [Fact]
        public void Cost_Per_Litre_Is_Zero_Safe_On_An_Empty_Batch()
        {
            var b = NewBatch(100);
            b.CostOfGoods = Money.FromCents(1000);
            b.Remove(100);

            b.CostPerLitre.Should().Be(Money.Zero);
        }
    }
}
