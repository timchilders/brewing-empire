namespace BreweryEmpire.Core.Model.Brewing
{
    /// <summary>
    /// Pure spoilage-domain rules that live in the model layer so Batch can use
    /// them without depending on the simulation namespace.
    /// </summary>
    public static class SpoilageModel
    {
        /// <summary>
        /// Whether an infection of this organism is actually the point of the
        /// beer. Lactobacillus/Pediococcus in a Berliner Weisse or lambic is a
        /// feature, not a bug; everywhere else it is a defect.
        /// </summary>
        public static bool IsIntentionalSour(BeerStyle style, SpoilageOrganism organism)
        {
            switch (style)
            {
                case BeerStyle.BerlinerWeisse:
                case BeerStyle.Lambic:
                    return organism == SpoilageOrganism.Lactobacillus ||
                           organism == SpoilageOrganism.Pediococcus ||
                           organism == SpoilageOrganism.Brettanomyces;
                default:
                    return false;
            }
        }
    }
}
