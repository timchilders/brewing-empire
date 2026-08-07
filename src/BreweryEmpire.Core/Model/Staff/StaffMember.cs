using System;
using System.Collections.Generic;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Staff
{
    /// <summary>
    /// An employee.
    ///
    /// DESIGN TRAP (intentional): wages are owed whether or not the member is
    /// assigned to a node, but bonuses only apply where they are assigned.
    /// Hoarding talent therefore costs real money and does nothing — the
    /// player must actually deploy people.
    /// </summary>
    public sealed class StaffMember
    {
        private int _skillBasisPoints;

        public StaffId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public StaffRole Role { get; set; }

        /// <summary>Clamped to [0, 10000].</summary>
        public int SkillBasisPoints
        {
            get => _skillBasisPoints;
            set => _skillBasisPoints = Clamp(value);
        }

        public Money MonthlyWage { get; set; }
        public int Age { get; set; }
        public GameDate HiredOn { get; set; }

        /// <summary>Null means unassigned — still paid, contributes nothing.</summary>
        public string? AssignedNodeId { get; private set; }

        public IReadOnlyList<StaffTrait> Traits { get; set; } = Array.Empty<StaffTrait>();
        public bool IsHistoricalFigure { get; set; }
        public int LoyaltyBasisPoints { get; private set; } = 10000;

        public StaffMember() { }

        public StaffMember(StaffId id, string name, StaffRole role, int skillBasisPoints,
                           Money monthlyWage, int age, GameDate hiredOn,
                           IReadOnlyList<StaffTrait>? traits = null,
                           bool isHistoricalFigure = false)
        {
            Id = id;
            Name = name;
            Role = role;
            SkillBasisPoints = skillBasisPoints;
            MonthlyWage = monthlyWage;
            Age = age;
            HiredOn = hiredOn;
            Traits = traits ?? Array.Empty<StaffTrait>();
            IsHistoricalFigure = isHistoricalFigure;
        }

        private static int Clamp(int value) => value < 0 ? 0 : (value > 10000 ? 10000 : value);

        public bool HasResigned => LoyaltyBasisPoints <= 0;

        public void AdjustLoyalty(int delta) => LoyaltyBasisPoints = Clamp(LoyaltyBasisPoints + delta);

        public void AssignTo(string? nodeId) => AssignedNodeId = nodeId;

        /// <summary>
        /// Sum of this member's trait magnitudes for one effect. Traits whose
        /// AppliesTo does not match this member's role are ignored.
        /// </summary>
        public int BonusFor(TraitEffect effect)
        {
            int total = 0;
            foreach (var t in Traits)
            {
                if (t.Effect != effect) continue;
                if (t.AppliesTo != Role) continue;
                total += t.MagnitudeBasisPoints;
            }
            return total;
        }
    }
}
