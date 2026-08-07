using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Staff
{
    /// <summary>
    /// Real people from brewing history, gated to the year their contribution
    /// actually became available.
    ///
    /// WHY REAL FIGURES: hiring Emil Hansen in 1883 and watching infections
    /// collapse teaches why pure yeast culture mattered far better than a tech
    /// tree node called "Yeast II". Each figure is exclusive — there is only
    /// one Pasteur, and a rival may take him first.
    /// </summary>
    public static class HistoricalFigures
    {
        public sealed class Figure
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public StaffRole Role { get; set; }
            public int AvailableFromYear { get; set; }
            public int SkillBasisPoints { get; set; }
            public Money MonthlyWage { get; set; }
            public IReadOnlyList<StaffTrait> Traits { get; set; } = new List<StaffTrait>();
            public string Contribution { get; set; } = string.Empty;
        }

        private static StaffTrait Trait(string id, string name, StaffRole role,
                                        TraitEffect effect, int magnitude, string description) =>
            new StaffTrait
            {
                Id = id,
                DisplayName = name,
                AppliesTo = role,
                Effect = effect,
                MagnitudeBasisPoints = magnitude,
                Description = description
            };

        public static IReadOnlyList<Figure> All { get; } = new List<Figure>
        {
            new Figure
            {
                Id = "hansen", Name = "Emil Christian Hansen", Role = StaffRole.Chemist,
                AvailableFromYear = 1883, SkillBasisPoints = 9800,
                MonthlyWage = Money.FromWhole(180),
                Contribution = "Isolated the first pure yeast culture at Carlsberg.",
                Traits = new List<StaffTrait>
                {
                    Trait("pure-yeast", "Pure Yeast Isolation", StaffRole.Chemist,
                          TraitEffect.InfectionResistance, 3000,
                          "Single-cell yeast propagation nearly eliminates wild infection."),
                    Trait("hansen-research", "Carlsberg Laboratory", StaffRole.Chemist,
                          TraitEffect.ResearchRate, 1500, "Systematic laboratory method.")
                }
            },
            new Figure
            {
                Id = "pasteur", Name = "Louis Pasteur", Role = StaffRole.Chemist,
                AvailableFromYear = 1864, SkillBasisPoints = 9700,
                MonthlyWage = Money.FromWhole(200),
                Contribution = "Proved fermentation is microbial; invented pasteurization.",
                Traits = new List<StaffTrait>
                {
                    Trait("pasteurization", "Pasteurization", StaffRole.Chemist,
                          TraitEffect.InfectionResistance, 2500,
                          "Gentle heating kills spoilage organisms before shipping."),
                    Trait("germ-theory", "Germ Theory", StaffRole.Chemist,
                          TraitEffect.ResearchRate, 2000, "Foundational microbiology.")
                }
            },
            new Figure
            {
                Id = "linde", Name = "Carl von Linde", Role = StaffRole.Chemist,
                AvailableFromYear = 1873, SkillBasisPoints = 9500,
                MonthlyWage = Money.FromWhole(190),
                Contribution = "Built the first practical mechanical refrigeration.",
                Traits = new List<StaffTrait>
                {
                    Trait("refrigeration", "Mechanical Refrigeration", StaffRole.Chemist,
                          TraitEffect.ResearchRate, 2500,
                          "Year-round cold fermentation without ice harvest.")
                }
            },
            new Figure
            {
                Id = "sedlmayr", Name = "Gabriel Sedlmayr II", Role = StaffRole.Brewmaster,
                AvailableFromYear = 1834, SkillBasisPoints = 9400,
                MonthlyWage = Money.FromWhole(150),
                Contribution = "Modernised Spaten; systematised lager brewing.",
                Traits = new List<StaffTrait>
                {
                    Trait("lager-mastery", "Lager Mastery", StaffRole.Brewmaster,
                          TraitEffect.QualityBonus, 1500, "Cold fermentation expertise."),
                    Trait("saccharometer", "Saccharometer Discipline", StaffRole.Brewmaster,
                          TraitEffect.MashEfficiency, 1200, "Measures rather than guesses.")
                }
            },
            new Figure
            {
                Id = "dreher", Name = "Anton Dreher", Role = StaffRole.Brewmaster,
                AvailableFromYear = 1841, SkillBasisPoints = 9300,
                MonthlyWage = Money.FromWhole(145),
                Contribution = "Created Vienna lager using English pale-malt kilning.",
                Traits = new List<StaffTrait>
                {
                    Trait("vienna-malt", "Vienna Kilning", StaffRole.Brewmaster,
                          TraitEffect.QualityBonus, 1400, "Amber malt without scorching.")
                }
            },
            new Figure
            {
                Id = "groll", Name = "Josef Groll", Role = StaffRole.Brewmaster,
                AvailableFromYear = 1842, SkillBasisPoints = 9600,
                MonthlyWage = Money.FromWhole(160),
                Contribution = "Brewed the first pale lager in Pilsen.",
                Traits = new List<StaffTrait>
                {
                    Trait("pilsner-origin", "Pilsner Originator", StaffRole.Brewmaster,
                          TraitEffect.QualityBonus, 2000,
                          "Unmatched with soft water and pale malt.")
                }
            },
            new Figure
            {
                Id = "guinness", Name = "Arthur Guinness", Role = StaffRole.Foreman,
                AvailableFromYear = 1759, SkillBasisPoints = 9000,
                MonthlyWage = Money.FromWhole(120),
                Contribution = "Signed a 9,000-year lease at St. James's Gate.",
                Traits = new List<StaffTrait>
                {
                    Trait("shrewd-lease", "Shrewd Lease", StaffRole.Foreman,
                          TraitEffect.UpkeepReduction, 2000,
                          "Ruthless negotiation on fixed costs.")
                }
            },
            new Figure
            {
                Id = "busch", Name = "Adolphus Busch", Role = StaffRole.Salesman,
                AvailableFromYear = 1876, SkillBasisPoints = 9500,
                MonthlyWage = Money.FromWhole(170),
                Contribution = "Built refrigerated rail distribution across America.",
                Traits = new List<StaffTrait>
                {
                    Trait("rail-network", "Refrigerated Rail", StaffRole.Salesman,
                          TraitEffect.PriceRealization, 1800,
                          "National reach commands better prices."),
                    Trait("relentless-sales", "Relentless Salesmanship", StaffRole.Salesman,
                          TraitEffect.TransitSpeed, 1000, "Beer moves before it turns.")
                }
            }
        };

        public static IEnumerable<Figure> AvailableIn(GameDate date) =>
            All.Where(f => date.Year >= f.AvailableFromYear);

        public static Figure Get(string id) => All.First(f => f.Id == id);

        /// <summary>Materialise a figure as a hireable staff member.</summary>
        public static StaffMember ToStaffMember(this Figure figure, GameDate hiredOn) =>
            new StaffMember(new StaffId(figure.Id), figure.Name, figure.Role,
                            figure.SkillBasisPoints, figure.MonthlyWage,
                            age: 40, hiredOn: hiredOn,
                            traits: figure.Traits, isHistoricalFigure: true);
    }
}
