using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Logistics;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Scenario
{
    /// <summary>The built-in starting worlds.</summary>
    public static class ScenarioCatalog
    {
        public static ScenarioDefinition Burton1750 { get; } = new ScenarioDefinition
        {
            Id = "burton-1750",
            Name = "Burton, 1750",
            Start = GameDate.FromYearMonthDay(1750, 1, 1),
            OpeningCapital = Money.FromWhole(50_000),
            Sites =
            {
                new ScenarioSite
                {
                    Id = "burton", Name = "Burton Brewery", Type = NodeType.Brewery,
                    RegionId = "midlands", Water = WaterProfile.Burton,
                    Climate = RegionClimate.BurtonEngland, DailyOverheadCents = 200,
                    StockKilos = 5000,
                    Vessels =
                    {
                        new ScenarioVessel { Type = VesselType.MashTun, Tier = EquipmentTier.Copper, CapacityLitres = 2000 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Wooden, CapacityLitres = 1200 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Copper, CapacityLitres = 1200 }
                    }
                },
                new ScenarioSite
                {
                    Id = "london", Name = "London Warehouse", Type = NodeType.Warehouse,
                    RegionId = "london", Water = WaterProfile.London,
                    Climate = RegionClimate.Dublin, DailyOverheadCents = 100
                }
            },
            Routes =
            {
                new ScenarioRoute { From = "burton", To = "london", DistanceKm = 180, Mode = TransportMode.HorseCart }
            },
            Markets =
            {
                new ScenarioMarket { Id = "burton-market", Name = "Burton", AdjacentNodeId = "burton",
                    Climate = RegionClimate.BurtonEngland, Population = 12_000, Style = BeerStyle.PaleAle, BaseDemandLitresPerTick = 250 },
                new ScenarioMarket { Id = "london-market", Name = "London", AdjacentNodeId = "london",
                    Climate = RegionClimate.Dublin, Population = 20_000, Style = BeerStyle.Porter, BaseDemandLitresPerTick = 300 }
            },
            Staff =
            {
                new ScenarioStaff { Id = "bm-1", Name = "Sam Allsopp", Role = StaffRole.Brewmaster,
                    SkillBasisPoints = 6000, MonthlyWage = Money.FromWhole(25), AssignToNodeId = "burton" }
            },
            Rivals =
            {
                new ScenarioRival { Id = "rival-1", Name = "Rival & Co", StrengthBasisPoints = 3000, HomeRegionId = "london" }
            }
        };

        public static ScenarioDefinition London1890 { get; } = new ScenarioDefinition
        {
            Id = "london-1890",
            Name = "London, 1890",
            Start = GameDate.FromYearMonthDay(1890, 1, 1),
            OpeningCapital = Money.FromWhole(200_000),
            Sites =
            {
                new ScenarioSite
                {
                    Id = "london", Name = "London Brewery", Type = NodeType.Brewery,
                    RegionId = "london", Water = WaterProfile.London,
                    Climate = RegionClimate.Dublin, DailyOverheadCents = 500,
                    StockKilos = 20_000,
                    Vessels =
                    {
                        new ScenarioVessel { Type = VesselType.MashTun, Tier = EquipmentTier.Copper, CapacityLitres = 5000 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Iron, CapacityLitres = 3000 },
                        new ScenarioVessel { Type = VesselType.OpenFermenter, Tier = EquipmentTier.Iron, CapacityLitres = 3000 }
                    }
                },
                new ScenarioSite
                {
                    Id = "hamburg", Name = "Hamburg Depot", Type = NodeType.Warehouse,
                    RegionId = "hamburg", Water = WaterProfile.Pilsen,
                    Climate = RegionClimate.Bavaria, DailyOverheadCents = 300
                }
            },
            Routes =
            {
                new ScenarioRoute { From = "london", To = "hamburg", DistanceKm = 700, Mode = TransportMode.SteamRail }
            },
            Markets =
            {
                new ScenarioMarket { Id = "london-market", Name = "London", AdjacentNodeId = "london",
                    Climate = RegionClimate.Dublin, Population = 30_000, Style = BeerStyle.PaleAle, BaseDemandLitresPerTick = 600 },
                new ScenarioMarket { Id = "hamburg-market", Name = "Hamburg", AdjacentNodeId = "hamburg",
                    Climate = RegionClimate.Bavaria, Population = 25_000, Style = BeerStyle.Pilsner, BaseDemandLitresPerTick = 500 }
            },
            Staff =
            {
                new ScenarioStaff { Id = "bm-2", Name = "Carl Linde", Role = StaffRole.Chemist,
                    SkillBasisPoints = 8000, MonthlyWage = Money.FromWhole(60), AssignToNodeId = "london" },
                new ScenarioStaff { Id = "bm-3", Name = "Adolphus Busch", Role = StaffRole.Salesman,
                    SkillBasisPoints = 7000, MonthlyWage = Money.FromWhole(55), AssignToNodeId = "london" }
            },
            Rivals =
            {
                new ScenarioRival { Id = "rival-2", Name = "Continental Lager Co", StrengthBasisPoints = 6000, HomeRegionId = "hamburg" }
            }
        };

        public static IReadOnlyList<ScenarioDefinition> All { get; } =
            new List<ScenarioDefinition> { Burton1750, London1890 };
    }
}
