using System;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Packaging
{
    /// <summary>
    /// Tracks a fleet of returnable containers.
    ///
    /// CONSERVATION INVARIANT: OwnedTotal == Available + InUse + AtCustomerSites + Lost,
    /// always. Casks were a major capital asset historically and breweries
    /// employed people purely to chase them; leaking containers silently would
    /// hide a genuine cost centre from the player.
    /// </summary>
    public sealed class ContainerPool
    {
        public PackagingType Type { get; set; }
        public int OwnedTotal { get; private set; }
        public int InUse { get; private set; }
        public int AtCustomerSites { get; private set; }
        public int Lost { get; private set; }
        public Money ReplacementCost { get; set; }

        public ContainerPool() { }

        public ContainerPool(PackagingType type, int ownedTotal, Money replacementCost)
        {
            if (ownedTotal < 0)
                throw new ArgumentOutOfRangeException(nameof(ownedTotal));

            Type = type;
            OwnedTotal = ownedTotal;
            ReplacementCost = replacementCost;
        }

        public int Available => OwnedTotal - InUse - AtCustomerSites - Lost;

        public void Purchase(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            OwnedTotal += count;
        }

        /// <summary>
        /// Reserve containers for filling. Returns false and mutates NOTHING
        /// when short — callers treat false as "the packaging run did not happen".
        /// </summary>
        public bool TryReserve(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return true;
            if (Available < count) return false;

            InUse += count;
            return true;
        }

        public void MarkDelivered(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > InUse)
                throw new InvalidOperationException(
                    "Cannot deliver " + count + " containers; only " + InUse + " are in use.");

            InUse -= count;
            AtCustomerSites += count;
        }

        /// <summary>
        /// Attempt to recover containers from customers. Losses are rolled per
        /// container so the result is deterministic for a given rng and seed.
        /// Returns the number actually recovered.
        /// </summary>
        public int ProcessReturns(int count, DeterministicRandom rng, int lossBasisPoints)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > AtCustomerSites)
                throw new InvalidOperationException(
                    "Cannot return " + count + " containers; only " + AtCustomerSites + " are out.");

            int recovered = 0;
            int lost = 0;

            for (int i = 0; i < count; i++)
            {
                if (rng.Chance(lossBasisPoints)) lost++;
                else recovered++;
            }

            AtCustomerSites -= count;
            Lost += lost;
            return recovered;
        }

        /// <summary>Buy replacements for lost containers; clears that many from Lost.</summary>
        public Money ReplaceLost(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > Lost)
                throw new InvalidOperationException("Cannot replace more containers than were lost.");

            Lost -= count;
            OwnedTotal -= count;
            return ReplacementCost * count;
        }

        /// <summary>Conservation check used by tests and save validation.</summary>
        public bool InvariantHolds() =>
            OwnedTotal == Available + InUse + AtCustomerSites + Lost && Available >= 0;
    }
}
