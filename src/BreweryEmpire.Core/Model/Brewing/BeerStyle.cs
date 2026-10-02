namespace BreweryEmpire.Core.Model.Brewing
{
    /// <summary>
    /// Broad beer style categories. Drives market demand, style→water fit, and
    /// whether a sour infection is a defect or a feature.
    /// </summary>
    public enum BeerStyle
    {
        PaleAle = 0,
        Porter = 1,
        Stout = 2,
        Pilsner = 3,
        Lager = 4,
        Bock = 5,
        BerlinerWeisse = 6,
        Lambic = 7,
        ViennaLager = 8,
        Mild = 9
    }

    /// <summary>
    /// The organism behind a spoilage infection. Knowing WHICH organism lets the
    /// player actually diagnose and fix the cause (dirty wood → Lactobacillus,
    /// oxygen ingress → Acetobacter), and it drives the defence model: hops
    /// inhibit Gram-positive bacteria, alcohol resists everything.
    /// </summary>
    public enum SpoilageOrganism
    {
        Lactobacillus = 0,
        Pediococcus = 1,
        Brettanomyces = 2,
        Acetobacter = 3,
        WildYeast = 4
    }
}
