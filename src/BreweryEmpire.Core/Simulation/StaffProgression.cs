using System;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Staff;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Simulation
{
    /// <summary>
    /// Staff learn by doing. Skill rises slowly with successful work, clamped
    /// at the ceiling, so a retained brewmaster is genuinely worth more than a
    /// fresh hire — the retention payoff that makes long-tenure staff matter.
    /// </summary>
    public static class StaffProgression
    {
        /// <summary>Grant experience; skill is clamped [0, 10000].</summary>
        public static void AwardExperience(StaffMember member, int amount)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            member.SkillBasisPoints = Math.Min(10000, member.SkillBasisPoints + amount);
        }

        /// <summary>
        /// On a successful brew, the assigned brewmaster (or another role) gains
        /// skill. Unassigned staff gain nothing — you must deploy people to grow
        /// them.
        /// </summary>
        public static void OnSuccessfulBrew(GameState state, NodeId nodeId, StaffRole role, int amount)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var member = state.Staff.BestFor(nodeId.Value, role);
            if (member != null)
                AwardExperience(member, amount);
        }
    }
}
