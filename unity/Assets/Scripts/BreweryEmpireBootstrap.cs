using System.Text;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Scenario;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;
using UnityEngine;

/// <summary>
/// Phase-5 spike: prove the compiled BreweryEmpire.Core DLL runs inside Unity.
/// New game from a scenario, advance days from the GUI, render a text dashboard.
///
/// Attach to a single GameObject in an otherwise-empty scene. No prefabs,
/// no assets, no editor tooling — the dashboard is drawn with immediate-mode
/// GUI so the whole thing is one file.
/// </summary>
public class BreweryEmpireBootstrap : MonoBehaviour
{
    private GameState _state;
    private string _summary = "not started";

    private void Start()
    {
        NewGame(1750);
    }

    private void NewGame(int seed)
    {
        var scenario = ScenarioCatalog.Burton1750;
        _state = ScenarioAssembler.Assemble(seed, scenario);
        _state.Recipes["pale-ale"] = RecipeCatalog.PaleAle();
        _state.Recipes["porter"] = RecipeCatalog.Porter();
        _state.Recipes["pilsner"] = RecipeCatalog.Pilsner();
        Refresh();
    }

    private void Refresh()
    {
        if (_state == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Brewery Empire - Unity spike");
        sb.AppendLine("Date: " + _state.Date + "   Bankrupt: " + _state.IsBankrupt);
        sb.AppendLine("Balance: " + _state.Ledger.Balance);
        sb.AppendLine("Research: " + string.Join(", ", _state.Research.UnlockedTechIds));
        foreach (var n in _state.World.Nodes)
            sb.AppendLine("  site " + n.Id.Value + " (" + n.Type + ") vessels=" +
                          n.Vessels.Count + " batches=" + n.Batches.Count);
        foreach (var m in _state.Markets)
            sb.AppendLine("  market " + m.Id + " reputation=" + m.ReputationBasisPoints);
        _summary = sb.ToString();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(8, 8, 600, Screen.height - 16));
        GUILayout.Label(_summary);
        GUILayout.Space(8);

        if (GUILayout.Button("Advance 1 day")) Advance(1);
        if (GUILayout.Button("Advance 30 days")) Advance(30);
        if (GUILayout.Button("Brew pale ale")) Brew();
        if (GUILayout.Button("New game (1750)")) NewGame(1750);

        GUILayout.EndArea();
    }

    private void Advance(int days)
    {
        if (_state == null) return;
        for (int i = 0; i < days; i++)
        {
            if (i % 14 == 0)
                Brew();
            TickSystem.AdvanceDay(_state);
        }
        Refresh();
    }

    private void Brew()
    {
        if (_state == null) return;
        foreach (var n in _state.World.Nodes)
        {
            if (n.Type == NodeType.Brewery)
                BrewingSystem.TryStartBrew(_state, n.Id, new RecipeId("pale-ale"));
        }
    }
}
