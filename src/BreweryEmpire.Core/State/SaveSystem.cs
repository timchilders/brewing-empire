using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Ingredients;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Model.Staff;

namespace BreweryEmpire.Core.State
{
    /// <summary>
    /// Save/load.
    ///
    /// Hand-rolled DTOs rather than reflecting over the live model: the domain
    /// types use private setters and computed properties precisely so the
    /// simulation stays trustworthy, and prising those open just to satisfy a
    /// serializer would be the wrong trade. The DTO layer also gives us a
    /// stable on-disk shape that can evolve independently of internals.
    /// </summary>
    public static class SaveSystem
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Save(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.SyncRandomState();
            return JsonSerializer.Serialize(ToDto(state), Options);
        }

        public static GameState Load(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Empty save data.", nameof(json));

            var dto = JsonSerializer.Deserialize<GameStateDto>(json, Options)
                      ?? throw new InvalidOperationException("Save data could not be parsed.");

            if (dto.SaveVersion > GameState.CurrentSaveVersion)
                throw new InvalidOperationException(
                    "Save version " + dto.SaveVersion + " is newer than this build supports (" +
                    GameState.CurrentSaveVersion + ").");

            return FromDto(dto);
        }

        // ---------- mapping ----------

        private static GameStateDto ToDto(GameState s)
        {
            var dto = new GameStateDto
            {
                SaveVersion = s.SaveVersion,
                Seed = s.Seed,
                DateTotalDays = s.Date.TotalDays,
                RandomState = s.RandomState,
                ReputationBasisPoints = s.ReputationBasisPoints,
                IsBankrupt = s.IsBankrupt,
                NextEntityNumber = s.NextEntityNumber,
                LedgerBalanceCents = s.Ledger.Balance.Cents,
                Rivals = s.Rivals
            };

            foreach (var e in s.Ledger.Entries)
            {
                dto.LedgerEntries.Add(new LedgerEntryDto
                {
                    DateTotalDays = e.Date.TotalDays,
                    Category = (int)e.Category,
                    AmountCents = e.Amount.Cents,
                    Description = e.Description,
                    NodeId = e.NodeId
                });
            }

            foreach (var n in s.World.Nodes)
            {
                var nodeDto = new NodeDto
                {
                    Id = n.Id.Value,
                    Name = n.Name,
                    Type = (int)n.Type,
                    RegionId = n.RegionId.Value,
                    DailyOverheadCents = n.DailyOverhead.Cents,
                    Water = n.Water,
                    ClimateRegionId = n.Climate.RegionId,
                    ClimateTemps = n.Climate.MonthlyAvgTempCelsius
                };

                foreach (var v in n.Vessels)
                {
                    nodeDto.Vessels.Add(new VesselDto
                    {
                        Id = v.Id.Value,
                        Type = (int)v.Type,
                        Tier = (int)v.Tier,
                        CapacityLitres = v.CapacityLitres,
                        PurchaseCostCents = v.PurchaseCost.Cents,
                        DailyUpkeepCents = v.DailyUpkeep.Cents,
                        HygieneBasisPoints = v.HygieneBasisPoints,
                        ConditionBasisPoints = v.ConditionBasisPoints,
                        OccupiedBy = v.OccupiedBy?.Value,
                        DaysRemaining = v.DaysRemaining
                    });
                }

                foreach (var b in n.Batches)
                {
                    nodeDto.Batches.Add(new BatchDto
                    {
                        Id = b.Id.Value,
                        RecipeId = b.RecipeId.Value,
                        NodeId = b.NodeId,
                        VesselId = b.VesselId.Value,
                        VolumeLitres = b.VolumeLitres,
                        State = (int)b.State,
                        BrewedOnTotalDays = b.BrewedOn.TotalDays,
                        ReadyOnTotalDays = b.ReadyOn.TotalDays,
                        QualityBasisPoints = b.QualityBasisPoints,
                        CostOfGoodsCents = b.CostOfGoods.Cents,
                        Flavor = b.Flavor,
                        Infections = new List<Infection>(b.Infections)
                    });
                }

                foreach (var lot in n.Inventory.Lots)
                {
                    nodeDto.Lots.Add(new LotDto
                    {
                        Id = lot.Id.Value,
                        IngredientId = lot.IngredientId,
                        QuantityGrams = lot.QuantityGrams,
                        HarvestYear = lot.HarvestYear,
                        QualityBasisPoints = lot.QualityBasisPoints,
                        AcquiredOnTotalDays = lot.AcquiredOn.TotalDays,
                        UnitCostPaidCents = lot.UnitCostPaid.Cents
                    });
                }

                dto.Nodes.Add(nodeDto);
            }

            foreach (var m in s.Staff.All)
            {
                dto.Staff.Add(new StaffDto
                {
                    Id = m.Id.Value,
                    Name = m.Name,
                    Role = (int)m.Role,
                    SkillBasisPoints = m.SkillBasisPoints,
                    MonthlyWageCents = m.MonthlyWage.Cents,
                    Age = m.Age,
                    HiredOnTotalDays = m.HiredOn.TotalDays,
                    AssignedNodeId = m.AssignedNodeId,
                    IsHistoricalFigure = m.IsHistoricalFigure,
                    LoyaltyBasisPoints = m.LoyaltyBasisPoints,
                    Traits = new List<StaffTrait>(m.Traits)
                });
            }

            foreach (var kv in s.Recipes)
            {
                var r = kv.Value;
                var recipeDto = new RecipeDto
                {
                    Id = r.Id.Value,
                    Name = r.Name,
                    YeastIngredientId = r.YeastIngredientId,
                    TargetVolumeLitres = r.TargetVolumeLitres,
                    FermentationDays = r.FermentationDays,
                    ConditioningDays = r.ConditioningDays,
                    Grist = new List<GristItem>(r.Grist),
                    Hops = new List<HopAddition>(r.Hops),
                    MashSteps = new List<MashStep>(r.Mash.Steps)
                };
                dto.Recipes.Add(recipeDto);
            }

            return dto;
        }

        private static GameState FromDto(GameStateDto dto)
        {
            var state = new GameState
            {
                SaveVersion = dto.SaveVersion,
                Seed = dto.Seed,
                Date = GameDate.FromTotalDays(dto.DateTotalDays),
                RandomState = dto.RandomState ?? Array.Empty<ulong>(),
                ReputationBasisPoints = dto.ReputationBasisPoints,
                IsBankrupt = dto.IsBankrupt,
                NextEntityNumber = dto.NextEntityNumber,
                Rivals = dto.Rivals ?? new List<RivalBrewer>()
            };

            state.RestoreRandom();

            var ledger = new Ledger();
            foreach (var e in dto.LedgerEntries)
            {
                ledger.Post(GameDate.FromTotalDays(e.DateTotalDays),
                            (LedgerCategory)e.Category,
                            Money.FromCents(e.AmountCents),
                            e.Description, e.NodeId);
            }
            state.Ledger = ledger;

            var world = new WorldMap();
            foreach (var n in dto.Nodes)
            {
                var climate = new RegionClimate
                {
                    RegionId = n.ClimateRegionId,
                    MonthlyAvgTempCelsius = n.ClimateTemps ?? new int[12]
                };

                var node = new BreweryNode(new NodeId(n.Id), n.Name, (NodeType)n.Type,
                                           new RegionId(n.RegionId),
                                           n.Water ?? WaterProfile.London, climate)
                {
                    DailyOverhead = Money.FromCents(n.DailyOverheadCents)
                };

                foreach (var v in n.Vessels)
                {
                    var vessel = new Vessel(new VesselId(v.Id), (VesselType)v.Type,
                                            (EquipmentTier)v.Tier, v.CapacityLitres,
                                            Money.FromCents(v.PurchaseCostCents),
                                            Money.FromCents(v.DailyUpkeepCents))
                    {
                        HygieneBasisPoints = v.HygieneBasisPoints
                    };

                    vessel.DegradeCondition(10000 - v.ConditionBasisPoints);
                    if (v.OccupiedBy != null) vessel.Occupy(new BatchId(v.OccupiedBy), v.DaysRemaining);

                    node.AddVessel(vessel);
                }

                foreach (var b in n.Batches)
                {
                    var batch = new Batch(new BatchId(b.Id), new RecipeId(b.RecipeId), b.NodeId,
                                          new VesselId(b.VesselId), Math.Max(1, b.VolumeLitres),
                                          GameDate.FromTotalDays(b.BrewedOnTotalDays),
                                          GameDate.FromTotalDays(b.ReadyOnTotalDays))
                    {
                        CostOfGoods = Money.FromCents(b.CostOfGoodsCents),
                        Flavor = b.Flavor ?? new FlavorProfile()
                    };

                    batch.SetQuality(b.QualityBasisPoints);

                    // Restore exact volume, including zero.
                    int delta = batch.VolumeLitres - b.VolumeLitres;
                    if (delta > 0) batch.Remove(delta);

                    foreach (var inf in b.Infections ?? new List<Infection>())
                        batch.RestoreInfection(inf);

                    batch.SetQuality(b.QualityBasisPoints);

                    if ((BatchState)b.State == BatchState.Ready) batch.MarkReady();
                    else if ((BatchState)b.State == BatchState.Spoiled) batch.MarkSpoiled();

                    node.AddBatch(batch);
                }

                foreach (var l in n.Lots)
                {
                    node.Inventory.AddLot(new IngredientLot(
                        new LotId(l.Id), l.IngredientId, l.QuantityGrams, l.HarvestYear,
                        l.QualityBasisPoints, GameDate.FromTotalDays(l.AcquiredOnTotalDays),
                        Money.FromCents(l.UnitCostPaidCents)));
                }

                world.AddNode(node);
            }
            state.World = world;

            var roster = new Roster();
            foreach (var m in dto.Staff)
            {
                var member = new StaffMember(new StaffId(m.Id), m.Name, (StaffRole)m.Role,
                                             m.SkillBasisPoints, Money.FromCents(m.MonthlyWageCents),
                                             m.Age, GameDate.FromTotalDays(m.HiredOnTotalDays),
                                             m.Traits, m.IsHistoricalFigure);

                member.AssignTo(m.AssignedNodeId);
                member.AdjustLoyalty(m.LoyaltyBasisPoints - 10000);
                AddWithoutValidation(roster, member);
            }
            state.Staff = roster;

            foreach (var r in dto.Recipes)
            {
                var recipe = new Recipe
                {
                    Id = new RecipeId(r.Id),
                    Name = r.Name,
                    YeastIngredientId = r.YeastIngredientId,
                    TargetVolumeLitres = r.TargetVolumeLitres,
                    FermentationDays = r.FermentationDays,
                    ConditioningDays = r.ConditioningDays
                };

                foreach (var g in r.Grist ?? new List<GristItem>())
                    recipe.AddGrain(g.IngredientId, g.Grams);

                foreach (var h in r.Hops ?? new List<HopAddition>())
                    recipe.AddHop(h.IngredientId, h.Grams, h.BoilMinutesRemaining);

                var mash = new MashSchedule();
                foreach (var step in r.MashSteps ?? new List<MashStep>())
                    mash.AddStep(step);
                if (mash.Steps.Count > 0) recipe.Mash = mash;

                state.Recipes[recipe.Id.Value] = recipe;
            }

            return state;
        }

        /// <summary>
        /// Loading must not re-run hiring rules — a save may legitimately
        /// contain a historical figure hired long ago.
        /// </summary>
        private static void AddWithoutValidation(Roster roster, StaffMember member)
        {
            var hiredOn = member.HiredOn;
            if (member.IsHistoricalFigure)
            {
                var temp = new StaffMember(member.Id, member.Name, member.Role,
                                           member.SkillBasisPoints, member.MonthlyWage,
                                           member.Age, hiredOn, member.Traits,
                                           isHistoricalFigure: false);
                temp.AssignTo(member.AssignedNodeId);
                temp.AdjustLoyalty(member.LoyaltyBasisPoints - 10000);
                temp.IsHistoricalFigure = true;
                roster.Hire(temp, hiredOn);
                return;
            }

            roster.Hire(member, hiredOn);
        }

        // ---------- DTOs ----------

        internal sealed class GameStateDto
        {
            public int SaveVersion { get; set; }
            public int Seed { get; set; }
            public int DateTotalDays { get; set; }
            public ulong[]? RandomState { get; set; }
            public int ReputationBasisPoints { get; set; }
            public bool IsBankrupt { get; set; }
            public int NextEntityNumber { get; set; }
            public long LedgerBalanceCents { get; set; }
            public List<LedgerEntryDto> LedgerEntries { get; set; } = new List<LedgerEntryDto>();
            public List<NodeDto> Nodes { get; set; } = new List<NodeDto>();
            public List<StaffDto> Staff { get; set; } = new List<StaffDto>();
            public List<RecipeDto> Recipes { get; set; } = new List<RecipeDto>();
            public List<RivalBrewer>? Rivals { get; set; }
        }

        internal sealed class LedgerEntryDto
        {
            public int DateTotalDays { get; set; }
            public int Category { get; set; }
            public long AmountCents { get; set; }
            public string Description { get; set; } = string.Empty;
            public string? NodeId { get; set; }
        }

        internal sealed class NodeDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Type { get; set; }
            public string RegionId { get; set; } = string.Empty;
            public long DailyOverheadCents { get; set; }
            public WaterProfile? Water { get; set; }
            public string ClimateRegionId { get; set; } = string.Empty;
            public int[]? ClimateTemps { get; set; }
            public List<VesselDto> Vessels { get; set; } = new List<VesselDto>();
            public List<BatchDto> Batches { get; set; } = new List<BatchDto>();
            public List<LotDto> Lots { get; set; } = new List<LotDto>();
        }

        internal sealed class VesselDto
        {
            public string Id { get; set; } = string.Empty;
            public int Type { get; set; }
            public int Tier { get; set; }
            public int CapacityLitres { get; set; }
            public long PurchaseCostCents { get; set; }
            public long DailyUpkeepCents { get; set; }
            public int HygieneBasisPoints { get; set; }
            public int ConditionBasisPoints { get; set; }
            public string? OccupiedBy { get; set; }
            public int DaysRemaining { get; set; }
        }

        internal sealed class BatchDto
        {
            public string Id { get; set; } = string.Empty;
            public string RecipeId { get; set; } = string.Empty;
            public string NodeId { get; set; } = string.Empty;
            public string VesselId { get; set; } = string.Empty;
            public int VolumeLitres { get; set; }
            public int State { get; set; }
            public int BrewedOnTotalDays { get; set; }
            public int ReadyOnTotalDays { get; set; }
            public int QualityBasisPoints { get; set; }
            public long CostOfGoodsCents { get; set; }
            public FlavorProfile? Flavor { get; set; }
            public List<Infection>? Infections { get; set; }
        }

        internal sealed class LotDto
        {
            public string Id { get; set; } = string.Empty;
            public string IngredientId { get; set; } = string.Empty;
            public int QuantityGrams { get; set; }
            public int HarvestYear { get; set; }
            public int QualityBasisPoints { get; set; }
            public int AcquiredOnTotalDays { get; set; }
            public long UnitCostPaidCents { get; set; }
        }

        internal sealed class StaffDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Role { get; set; }
            public int SkillBasisPoints { get; set; }
            public long MonthlyWageCents { get; set; }
            public int Age { get; set; }
            public int HiredOnTotalDays { get; set; }
            public string? AssignedNodeId { get; set; }
            public bool IsHistoricalFigure { get; set; }
            public int LoyaltyBasisPoints { get; set; }
            public List<StaffTrait> Traits { get; set; } = new List<StaffTrait>();
        }

        internal sealed class RecipeDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string YeastIngredientId { get; set; } = string.Empty;
            public int TargetVolumeLitres { get; set; }
            public int FermentationDays { get; set; }
            public int ConditioningDays { get; set; }
            public List<GristItem>? Grist { get; set; }
            public List<HopAddition>? Hops { get; set; }
            public List<MashStep>? MashSteps { get; set; }
        }
    }
}
