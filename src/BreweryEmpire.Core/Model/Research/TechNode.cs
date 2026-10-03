using System;
using System.Collections.Generic;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Core.Model.Research
{
    /// <summary>
    /// One node in the research tree. Pure data: prerequisites and cost shape the
    /// DAG, effects are applied by ResearchSystem so the catalog stays serializable
    /// and the behaviour stays in one place.
    /// </summary>
    public sealed class TechNode
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Earliest era the node may be researched.</summary>
        public Era MinEra { get; init; } = Era.PreIndustrial;

        /// <summary>Research points required to complete.</summary>
        public int ResearchCostPoints { get; init; }

        /// <summary>Tech ids that must already be unlocked.</summary>
        public IReadOnlyList<string> PrerequisiteIds { get; init; } = Array.Empty<string>();

        public string Description { get; init; } = string.Empty;

        /// <summary>The educational "Science Snapshot" shown to the player.</summary>
        public string LoreText { get; init; } = string.Empty;
    }
}
