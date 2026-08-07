using System;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Model.Recipes
{
    public class MashScheduleTests
    {
        [Fact]
        public void Single_Infusion_Has_One_Step()
        {
            var m = MashSchedule.SingleInfusion(66);
            m.Steps.Should().ContainSingle();
            m.WeightedAverageTemperature.Should().Be(66);
        }

        [Fact]
        public void Step_Mash_Is_Duration_Weighted_Not_A_Plain_Mean()
        {
            var m = MashSchedule.StepMash();
            // Plain mean of 52,63,72,78 is 66; weighted by duration it differs.
            m.WeightedAverageTemperature.Should().NotBe(66);
            m.TotalDurationMinutes.Should().Be(100);
        }

        [Fact]
        public void Low_Mash_Is_More_Fermentable_Than_High_Mash()
        {
            var dry = MashSchedule.SingleInfusion(62);
            var sweet = MashSchedule.SingleInfusion(72);

            dry.FermentabilityBasisPoints.Should().BeGreaterThan(sweet.FermentabilityBasisPoints);
        }

        [Fact]
        public void Fermentability_Is_Monotonic_Across_The_Mash_Range()
        {
            int previous = int.MaxValue;
            for (int t = 60; t <= 75; t++)
            {
                int f = MashSchedule.SingleInfusion(t).FermentabilityBasisPoints;
                f.Should().BeLessOrEqualTo(previous);
                previous = f;
            }
        }

        [Fact]
        public void Fermentability_Plateaus_At_The_Extremes()
        {
            MashSchedule.SingleInfusion(55).FermentabilityBasisPoints.Should().Be(9000);
            MashSchedule.SingleInfusion(80).FermentabilityBasisPoints.Should().Be(5500);
        }

        [Fact]
        public void Invalid_Steps_Are_Rejected()
        {
            var m = new MashSchedule();

            Action tooHot = () => m.AddStep(new MashStep { TemperatureCelsius = 150, DurationMinutes = 10 });
            tooHot.Should().Throw<ArgumentOutOfRangeException>();

            Action noTime = () => m.AddStep(new MashStep { TemperatureCelsius = 65, DurationMinutes = 0 });
            noTime.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Empty_Schedule_Is_Zero()
        {
            var m = new MashSchedule();
            m.WeightedAverageTemperature.Should().Be(0);
            m.TotalDurationMinutes.Should().Be(0);
        }
    }

    public class HopAdditionTests
    {
        [Fact]
        public void Long_Boil_Gives_Bitterness_Not_Aroma()
        {
            var bittering = new HopAddition { BoilMinutesRemaining = 60, Grams = 50 };

            bittering.UtilizationBasisPoints.Should().Be(3000);
            bittering.AromaRetentionBasisPoints.Should().Be(0);
        }

        [Fact]
        public void Flameout_Addition_Gives_Aroma_Not_Bitterness()
        {
            var aroma = new HopAddition { BoilMinutesRemaining = 0, Grams = 50 };

            aroma.UtilizationBasisPoints.Should().Be(0);
            aroma.AromaRetentionBasisPoints.Should().Be(10000);
        }

        [Fact]
        public void Utilization_Rises_With_Boil_Time()
        {
            int previous = -1;
            for (int m = 0; m <= 60; m += 5)
            {
                int u = new HopAddition { BoilMinutesRemaining = m }.UtilizationBasisPoints;
                u.Should().BeGreaterOrEqualTo(previous);
                previous = u;
            }
        }

        [Fact]
        public void Utilization_Plateaus_Past_An_Hour()
        {
            new HopAddition { BoilMinutesRemaining = 90 }.UtilizationBasisPoints.Should().Be(3000);
        }
    }

    public class RecipeTests
    {
        private static Recipe PaleAle()
        {
            var r = new Recipe { Id = new RecipeId("pale-ale"), Name = "Pale Ale", TargetVolumeLitres = 1000 };
            r.AddGrain("pale-malt", 200_000);
            r.AddGrain("crystal-malt", 20_000);
            r.AddHop("goldings-hops", 2000, 60);
            r.AddHop("goldings-hops", 1000, 10);
            return r;
        }

        [Fact]
        public void Grist_Totals_Correctly()
        {
            PaleAle().TotalGristGrams.Should().Be(220_000);
        }

        [Fact]
        public void A_Sane_Recipe_Has_Sufficient_Diastatic_Power()
        {
            var catalog = IngredientCatalog.CreateDefault();
            PaleAle().HasSufficientDiastaticPower(catalog).Should().BeTrue();
        }

        [Fact]
        public void An_All_Roasted_Grist_Cannot_Convert()
        {
            var catalog = IngredientCatalog.CreateDefault();
            var impossible = new Recipe { Id = new RecipeId("bad"), Name = "All Roast" };
            impossible.AddGrain("roasted-barley", 100_000);
            impossible.AddGrain("chocolate-malt", 50_000);

            impossible.HasSufficientDiastaticPower(catalog).Should().BeFalse();
            impossible.WeightedDiastaticPower(catalog).Should().Be(0);
        }

        [Fact]
        public void Too_Much_Specialty_Malt_Falls_Below_The_Conversion_Floor()
        {
            var catalog = IngredientCatalog.CreateDefault();
            var r = new Recipe { Id = new RecipeId("stout"), Name = "Heavy Stout" };
            r.AddGrain("pale-malt", 20_000);       // 140 Lintner
            r.AddGrain("roasted-barley", 100_000); // 0 Lintner

            // Weighted: 140*20000/120000 = 23 Lintner, below the floor of 35.
            r.HasSufficientDiastaticPower(catalog).Should().BeFalse();
        }

        [Fact]
        public void Empty_Grist_Has_No_Conversion()
        {
            var catalog = IngredientCatalog.CreateDefault();
            new Recipe().HasSufficientDiastaticPower(catalog).Should().BeFalse();
        }

        [Fact]
        public void Roasted_Grain_Darkens_The_Estimated_Color()
        {
            var catalog = IngredientCatalog.CreateDefault();

            var pale = new Recipe();
            pale.AddGrain("pale-malt", 100_000);

            var stout = new Recipe();
            stout.AddGrain("pale-malt", 80_000);
            stout.AddGrain("roasted-barley", 20_000);

            stout.EstimatedColorLovibond(catalog)
                 .Should().BeGreaterThan(pale.EstimatedColorLovibond(catalog));
        }

        [Fact]
        public void Total_Days_To_Ready_Sums_Fermentation_And_Conditioning()
        {
            var r = new Recipe { FermentationDays = 7, ConditioningDays = 21 };
            r.TotalDaysToReady.Should().Be(28);
        }

        [Fact]
        public void Non_Positive_Quantities_Are_Rejected()
        {
            var r = new Recipe();
            Action grain = () => r.AddGrain("pale-malt", 0);
            Action hop = () => r.AddHop("goldings-hops", -1, 60);

            grain.Should().Throw<ArgumentOutOfRangeException>();
            hop.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Hops_Preserve_Their_Boil_Timings()
        {
            var r = PaleAle();
            r.Hops.Should().HaveCount(2);
            r.Hops[0].BoilMinutesRemaining.Should().Be(60);
            r.Hops[1].BoilMinutesRemaining.Should().Be(10);
        }

        [Fact]
        public void Null_Catalog_Is_Rejected()
        {
            var r = PaleAle();
            Action act = () => r.HasSufficientDiastaticPower(null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
