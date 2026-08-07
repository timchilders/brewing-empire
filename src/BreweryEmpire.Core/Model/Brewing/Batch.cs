using System;
using System.Collections.Generic;
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

        public int VolumeLitres { get; private set; }
        public BatchState State { get; private set; } = BatchState.Fermenting;
        public GameDate BrewedOn { get; set; }
        public GameDate ReadyOn { get; set; }

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
