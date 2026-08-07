using System;
using System.Collections.Generic;
using System.Linq;
using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Staff
{
    /// <summary>The company payroll and assignment book.</summary>
    public sealed class Roster
    {
        private readonly List<StaffMember> _staff = new List<StaffMember>();

        public IReadOnlyList<StaffMember> All => _staff;

        public int Count => _staff.Count;

        /// <summary>
        /// Hire someone. Historical figures are exclusive and era-gated: you
        /// cannot employ Pasteur in 1750, and you cannot employ him twice.
        /// </summary>
        public void Hire(StaffMember member, GameDate now)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));

            if (member.IsHistoricalFigure)
            {
                if (_staff.Any(s => s.Id == member.Id))
                    throw new InvalidOperationException(
                        "Historical figure " + member.Name + " is already employed. " +
                        "There is only one of them.");

                var figure = HistoricalFigures.All.FirstOrDefault(f => f.Id == member.Id.Value);
                if (figure != null && now.Year < figure.AvailableFromYear)
                    throw new InvalidOperationException(
                        member.Name + " is not available until " + figure.AvailableFromYear +
                        " (current year " + now.Year + ").");
            }

            _staff.Add(member);
        }

        public bool Contains(StaffId id) => _staff.Any(s => s.Id == id);

        public IEnumerable<StaffMember> AtNode(string nodeId) =>
            _staff.Where(s => string.Equals(s.AssignedNodeId, nodeId, StringComparison.Ordinal));

        /// <summary>
        /// Highest-skilled member of a role at a node. Ties break by StaffId
        /// ordinal so brew outcomes never depend on list insertion order.
        /// </summary>
        public StaffMember? BestFor(string nodeId, StaffRole role) =>
            AtNode(nodeId)
                .Where(s => s.Role == role)
                .OrderByDescending(s => s.SkillBasisPoints)
                .ThenBy(s => s.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>Wages for everyone, assigned or not. Over-hiring hurts.</summary>
        public Money TotalMonthlyWages
        {
            get
            {
                long cents = 0;
                foreach (var s in _staff) cents += s.MonthlyWage.Cents;
                return Money.FromCents(cents);
            }
        }

        /// <summary>Sum of an effect across staff assigned to one node only.</summary>
        public int AggregateBonus(string nodeId, TraitEffect effect)
        {
            int total = 0;
            foreach (var s in AtNode(nodeId)) total += s.BonusFor(effect);
            return total;
        }

        public List<StaffMember> ProcessResignations()
        {
            var leaving = _staff.Where(s => s.HasResigned).ToList();
            foreach (var s in leaving) _staff.Remove(s);
            return leaving;
        }

        public bool Fire(StaffId id)
        {
            var member = _staff.FirstOrDefault(s => s.Id == id);
            if (member == null) return false;
            _staff.Remove(member);
            return true;
        }
    }
}
