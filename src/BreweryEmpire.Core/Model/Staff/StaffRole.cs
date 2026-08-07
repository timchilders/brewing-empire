namespace BreweryEmpire.Core.Model.Staff
{
    public enum StaffRole
    {
        Brewmaster = 0,
        Cooper = 1,
        Drayman = 2,
        Chemist = 3,
        Maltster = 4,
        Salesman = 5,
        Foreman = 6
    }

    /// <summary>What a trait actually modifies. Kept as an enum so bonuses can
    /// be aggregated generically rather than with a bespoke field per effect.</summary>
    public enum TraitEffect
    {
        QualityBonus = 0,
        ConsistencyBonus = 1,
        InfectionResistance = 2,
        TransitSpeed = 3,
        ContainerLossReduction = 4,
        ResearchRate = 5,
        MashEfficiency = 6,
        PriceRealization = 7,
        UpkeepReduction = 8,
        WageDiscount = 9
    }

    /// <summary>
    /// A named ability attached to a staff member.
    ///
    /// AppliesTo exists so a trait only counts when held by the right kind of
    /// worker — a drayman's transit bonus must not apply because a chemist
    /// happens to carry the same trait id.
    /// </summary>
    public sealed record StaffTrait
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public StaffRole AppliesTo { get; init; }
        public int MagnitudeBasisPoints { get; init; }
        public TraitEffect Effect { get; init; }
    }
}
