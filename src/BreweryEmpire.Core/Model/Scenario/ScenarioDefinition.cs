using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Scenario
{
    /// <summary>A starting site (brewery or warehouse) with its vessels and stock.</summary>
    public sealed record ScenarioSite
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public NodeType Type { get; init; } = NodeType.Brewery;
        public string RegionId { get; init; } = string.Empty;
        public WaterProfile Water { get; init; } = WaterProfile.London;
        public RegionClimate Climate { get; init; } = RegionClimate.BurtonEngland;
        public long DailyOverheadCents { get; init; }
        public List<ScenarioVessel> Vessels { get; init; } = new List<ScenarioVessel>();
        public int StockKilos { get; init; }
    }

    public sealed record ScenarioVessel
    {
        public VesselType Type { get; init; }
        public EquipmentTier Tier { get; init; }
        public int CapacityLitres { get; init; }
    }

    /// <summary>A route between two sites, with a transport mode.</summary>
    public sealed record ScenarioRoute
    {
        public string From { get; init; } = string.Empty;
        public string To { get; init; } = string.Empty;
        public int DistanceKm { get; init; }
        public TransportMode Mode { get; init; } = TransportMode.HorseCart;
    }

    public sealed record ScenarioMarket
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string AdjacentNodeId { get; init; } = string.Empty;
        public RegionClimate Climate { get; init; } = RegionClimate.BurtonEngland;
        public int Population { get; init; }
        public BeerStyle Style { get; init; } = BeerStyle.PaleAle;
        public int BaseDemandLitresPerTick { get; init; }
    }

    public sealed record ScenarioStaff
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public StaffRole Role { get; init; }
        public int SkillBasisPoints { get; init; }
        public Money MonthlyWage { get; init; }
        public string AssignToNodeId { get; init; } = string.Empty;
    }

    public sealed record ScenarioRival
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int StrengthBasisPoints { get; init; }
        public string HomeRegionId { get; init; } = string.Empty;
    }

    /// <summary>The full starting-world description. Pure data, like TechCatalog/EventCatalog.</summary>
    public sealed record ScenarioDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public GameDate Start { get; init; } = GameDate.FromYearMonthDay(1750, 1, 1);
        public Money OpeningCapital { get; init; } = Money.FromWhole(2000);
        public List<ScenarioSite> Sites { get; init; } = new List<ScenarioSite>();
        public List<ScenarioRoute> Routes { get; init; } = new List<ScenarioRoute>();
        public List<ScenarioMarket> Markets { get; init; } = new List<ScenarioMarket>();
        public List<ScenarioStaff> Staff { get; init; } = new List<ScenarioStaff>();
        public List<ScenarioRival> Rivals { get; init; } = new List<ScenarioRival>();
    }
}
