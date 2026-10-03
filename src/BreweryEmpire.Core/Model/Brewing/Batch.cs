using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Brewing
{
    /// <summary>
    /// Minimal batch lifecycle. Deliberately three states rather than a full
    /// stage timeline: the vessel already tracks how long it is busy, so the
    /// batch only needs to know whether it is working, sellable, or ruined.
    /// </summary>
    public enum BatchState
    {
        Fermenting = 0,
        Ready = 1,
        Spoiled = 2
    }

    /// <summary>
    /// A production batch from pitch to sale.
    ///
    /// Carries its own COGS so profit per batch is auditable, and its own
    /// quality/flavour so the market can price it independently of how it
    /// was made.
    /// </summary>
    public sealed class Batch
    {
        private readonly List<Infection> _infections = new List<Infection>();

        public BatchId Id { get; set; }
        public RecipeId RecipeId { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public VesselId VesselId { get; set; }

        /// <summary>The yeast strain pitched (ale-yeast / lager-yeast).</summary>
        public string YeastIngredientId { get; set; } = "ale-yeast";

        /// <summary>Repitch count; higher generations drift and attenuate less.</summary>
        public int YeastGeneration { get; set; } = 1;

        /// <summary>The style this batch was brewed as (sour-intent lives here).</summary>
        public BeerStyle Style { get; set; } = BeerStyle.PaleAle;

        /// <summary>Whether the batch has been pasteurized (near-immunity to spoilage).</summary>
        public bool IsPasteurized { get; set; }

        public int VolumeLitres { get; private set; }
        public BatchState State { get; private set; } = BatchState.Fermenting;
        public GameDate BrewedOn { get; set; }
        public GameDate ReadyOn { get; set; }

        /// <summary>Days the beer keeps before it starts to stale (style/strength/tech driven).</summary>
        public int ShelfLifeDays { get; set; } = 30;

        /// <summary>Days since the batch became ready to sell.</summary>
        public int AgeDays(GameDate now) => Math.Max(0, now.TotalDays - ReadyOn.TotalDays);

        /// <summary>Original gravity in gravity points (50 = 1.050).</summary>
        public int OriginalGravityPoints { get; set; }

        /// <summary>Final gravity in gravity points (10 = 1.010).</summary>
        public int FinalGravityPoints { get; set; }

        /// <summary>Apparent attenuation, basis points (8000 = 80%).</summary>
        public int AttenuationBasisPoints { get; set; }

        /// <summary>Alcohol by volume, basis points (500 = 5.00%).</summary>
        public int AbvBasisPoints { get; set; }

        /// <summary>Bitterness in tenths of an IBU (350 = 35.0 IBU).</summary>
        public int IbuTenths { get; set; }

        /// <summary>Colour in Lovibond (SRM).</summary>
        public int SrmLovibond { get; set; }

        /// <summary>Execution axis: how well it was made.</summary>
        public int QualityBasisPoints { get; private set; } = 5000;

        /// <summary>Design axis: what it tastes like.</summary>
        public FlavorProfile Flavor { get; set; } = new FlavorProfile();

        /// <summary>Accumulated ingredient cost, for honest margin reporting.</summary>
        public Money CostOfGoods { get; set; }

        public IReadOnlyList<Infection> Infections => _infections;

        public Batch() { }

        public Batch(BatchId id, RecipeId recipeId, string nodeId, VesselId vesselId,
                     int volumeLitres, GameDate brewedOn, GameDate readyOn)
        {
            if (volumeLitres <= 0)
                throw new ArgumentOutOfRangeException(nameof(volumeLitres),
                    volumeLitres, "Batch volume must be positive.");

            Id = id;
            RecipeId = recipeId;
            NodeId = nodeId;
            VesselId = vesselId;
            VolumeLitres = volumeLitres;
            BrewedOn = brewedOn;
            ReadyOn = readyOn;
        }

        public bool IsSellable => State == BatchState.Ready && VolumeLitres > 0;

        public void SetQuality(int basisPoints) => QualityBasisPoints = Clamp(basisPoints);

        public void AdjustQuality(int delta) => QualityBasisPoints = Clamp(QualityBasisPoints + delta);

        private static int Clamp(int v) => v < 0 ? 0 : (v > 10000 ? 10000 : v);

        /// <summary>
        /// Record an infection. Severe infections ruin the batch outright;
        /// mild ones just drag quality down and leave an off-flavour the
        /// player can taste in the tasting notes.
        /// </summary>
        public void AddInfection(Infection infection)
        {
            if (infection == null) throw new ArgumentNullException(nameof(infection));

            _infections.Add(infection);
            AdjustQuality(-infection.SeverityBasisPoints);

            if (infection.SeverityBasisPoints >= 5000)
                State = BatchState.Spoiled;
        }

        /// <summary>Re-attach an infection during load without re-applying its penalty.</summary>
        public void RestoreInfection(Infection infection)
        {
            if (infection == null) throw new ArgumentNullException(nameof(infection));
            _infections.Add(infection);
        }

        /// <summary>
        /// Contract an infection from a known organism. Starts progression at zero;
        /// the spoilage system advances it toward ruin. A wanted (sour) organism on
        /// a sour style is not a defect and never spoils the batch.
        /// </summary>
        public void ContractInfection(SpoilageOrganism organism, string cause)
        {
            bool intentional = SpoilageModel.IsIntentionalSour(Style, organism);

            _infections.Add(new Infection
            {
                Character = OffFlavor.Sour,
                SeverityBasisPoints = 0,
                Organism = organism,
                ProgressionBasisPoints = 0,
                Cause = cause,
                IsIntentionalSour = intentional
            });

            if (!intentional)
            {
                // A detectable infection already drags quality.
                AdjustQuality(-400);
            }
        }

        /// <summary>
        /// Advance the latest non-sour infection toward ruin. Returns true exactly
        /// when the batch crosses the spoilage threshold (so the caller can write
        /// off the cost once).
        /// </summary>
        public bool AdvanceInfection(int progressionDelta)
        {
            if (progressionDelta < 0) throw new ArgumentOutOfRangeException(nameof(progressionDelta));

            var active = _infections
                .Where(i => !i.IsIntentionalSour && i.ProgressionBasisPoints < 10000)
                .OrderByDescending(i => i.ProgressionBasisPoints)
                .FirstOrDefault();

            if (active == null) return false;

            active.ProgressionBasisPoints += progressionDelta;

            if (active.ProgressionBasisPoints >= 10000)
            {
                active.ProgressionBasisPoints = 10000;
                State = BatchState.Spoiled;
                return true;
            }

            return false;
        }

        /// <summary>True if this batch carries a deliberate, wanted sour infection.</summary>
        public bool HasIntentionalSour => _infections.Any(i => i.IsIntentionalSour);

        /// <summary>Transition to sellable. Spoiled beer can never become ready.</summary>
        public void MarkReady()
        {
            if (State == BatchState.Spoiled)
                throw new InvalidOperationException("A spoiled batch cannot become ready.");
            State = BatchState.Ready;
        }

        public void MarkSpoiled() => State = BatchState.Spoiled;

        /// <summary>Draw beer off for packaging or sale.</summary>
        public void Remove(int litres)
        {
            if (litres < 0) throw new ArgumentOutOfRangeException(nameof(litres));
            if (litres > VolumeLitres)
                throw new InvalidOperationException(
                    "Cannot remove " + litres + "L from a batch holding " + VolumeLitres + "L.");

            VolumeLitres -= litres;
        }

        /// <summary>Ullage and evaporation losses.</summary>
        public void ApplyLossBasisPoints(int basisPoints)
        {
            if (basisPoints <= 0) return;
            int loss = VolumeLitres * basisPoints / 10000;
            VolumeLitres = Math.Max(0, VolumeLitres - loss);
        }

        /// <summary>Cost per litre, for pricing floors. Zero-volume safe.</summary>
        public Money CostPerLitre =>
            VolumeLitres <= 0 ? Money.Zero : Money.FromCents(CostOfGoods.Cents / VolumeLitres);
    }
}
