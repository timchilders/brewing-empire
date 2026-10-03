using System.Collections.Generic;

namespace BreweryEmpire.Core.State
{
    /// <summary>Player research progress. Must survive a save; lives on GameState.</summary>
    public sealed class ResearchState
    {
        public List<string> UnlockedTechIds { get; set; } = new List<string>();
        public string? ActiveTechId { get; set; }
        public int ActiveProgressPoints { get; set; }
    }
}
