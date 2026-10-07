using BreweryEmpire.Core.Model.Brewing;

namespace BreweryEmpire.Core.Model.Recipes
{
    /// <summary>
    /// Built-in recipes, so a scenario or headless run can start brewing without
    /// hand-rolling grist and hop schedules. Pure data, deterministic.
    /// </summary>
    public static class RecipeCatalog
    {
        public static Recipe PaleAle() => new Recipe
        {
            Id = new RecipeId("pale-ale"),
            Name = "Burton Pale Ale",
            TargetVolumeLitres = 1000,
            FermentationDays = 7,
            ConditioningDays = 14,
            Mash = MashSchedule.SingleInfusion(66)
        }
        .WithGrain("pale-malt", 200_000)
        .WithGrain("crystal-malt", 20_000)
        .WithHop("goldings-hops", 2000, 60)
        .WithHop("goldings-hops", 1000, 10);

        public static Recipe Porter() => new Recipe
        {
            Id = new RecipeId("porter"),
            Name = "London Porter",
            Style = BeerStyle.Porter,
            TargetVolumeLitres = 1000,
            FermentationDays = 7,
            ConditioningDays = 14,
            Mash = MashSchedule.SingleInfusion(68)
        }
        .WithGrain("pale-malt", 180_000)
        .WithGrain("crystal-malt", 20_000)
        .WithGrain("chocolate-malt", 15_000)
        .WithGrain("roasted-barley", 10_000)
        .WithHop("goldings-hops", 1500, 60);

        public static Recipe Pilsner() => new Recipe
        {
            Id = new RecipeId("pilsner"),
            Name = "Pilsner",
            YeastIngredientId = "lager-yeast",
            Style = BeerStyle.Pilsner,
            TargetVolumeLitres = 1000,
            FermentationDays = 10,
            ConditioningDays = 28,
            Mash = MashSchedule.SingleInfusion(64)
        }
        .WithGrain("pale-malt", 220_000)
        .WithHop("saaz-hops", 1800, 60)
        .WithHop("saaz-hops", 800, 5);

        private static Recipe WithGrain(this Recipe recipe, string ingredientId, int grams)
        {
            recipe.AddGrain(ingredientId, grams);
            return recipe;
        }

        private static Recipe WithHop(this Recipe recipe, string ingredientId, int grams, int boilMinutesRemaining)
        {
            recipe.AddHop(ingredientId, grams, boilMinutesRemaining);
            return recipe;
        }
    }
}
