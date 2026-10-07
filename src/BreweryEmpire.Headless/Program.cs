using BreweryEmpire.Core.Economy;
using BreweryEmpire.Core.Model;
using BreweryEmpire.Core.Model.Brewing;
using BreweryEmpire.Core.Model.Packaging;
using BreweryEmpire.Core.Model.Recipes;
using BreweryEmpire.Core.Model.Scenario;
using BreweryEmpire.Core.Model.Sites;
using BreweryEmpire.Core.Simulation;
using BreweryEmpire.Core.State;

namespace BreweryEmpire.Headless
{
    /// <summary>
    /// Headless smoke-test runner: assemble a scenario, run a deterministic
    /// autopilot for N days, and print a world/finance summary. This is the
    /// exact assembly a Unity front end will consume, run outside the editor.
    /// </summary>
    public static class Program
    {
        private sealed class Options
        {
            public string ScenarioId = "burton-1750";
            public int Seed = 1750;
            public int Days = 365;
            public string? SavePath;

            public static void PrintUsage(TextWriter w)
            {
                w.WriteLine("Brewery Empire — headless runner");
                w.WriteLine("Usage: BreweryEmpire.Headless [options]");
                w.WriteLine("  --scenario <id>   burton-1750 (default) | london-1890");
                w.WriteLine("  --seed <int>      PRNG seed (default 1750)");
                w.WriteLine("  --days <int>      days to simulate (default 365)");
                w.WriteLine("  --save <path>     write a save file at the end");
            }

            public static Options? Parse(string[] args)
            {
                var o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string Next(string name)
                    {
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("--" + name + " requires a value");
                        return args[++i];
                    }

                    switch (args[i])
                    {
                        case "--scenario": o.ScenarioId = Next("scenario"); break;
                        case "--seed":
                            if (!int.TryParse(Next("seed"), out o.Seed))
                                throw new ArgumentException("--seed must be an integer");
                            break;
                        case "--days":
                            if (!int.TryParse(Next("days"), out o.Days) || o.Days < 0)
                                throw new ArgumentException("--days must be a non-negative integer");
                            break;
                        case "--save": o.SavePath = Next("save"); break;
                        case "--help": case "-h": PrintUsage(Console.Out); return null;
                        default:
                            throw new ArgumentException("Unknown argument: " + args[i]);
                    }
                }
                return o;
            }
        }

        public static int Main(string[] args)
        {
            Options? opts;
            try
            {
                opts = Options.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                Options.PrintUsage(Console.Error);
                return 2;
            }

            if (opts == null) return 0;   // --help

            var scenario = opts.ScenarioId == "london-1890"
                ? ScenarioCatalog.London1890
                : ScenarioCatalog.Burton1750;

            var state = ScenarioAssembler.Assemble(opts.Seed, scenario);
            RegisterRecipes(state);

            RunAutopilot(state, opts.Days);

            if (opts.SavePath != null)
            {
                File.WriteAllText(opts.SavePath, SaveSystem.Save(state));
                Console.WriteLine("Saved to " + opts.SavePath);
                Console.WriteLine();
            }

            PrintSummary(scenario, opts, state);
            return 0;
        }

        private static void RegisterRecipes(GameState state)
        {
            state.Recipes["pale-ale"] = RecipeCatalog.PaleAle();
            state.Recipes["porter"] = RecipeCatalog.Porter();
            state.Recipes["pilsner"] = RecipeCatalog.Pilsner();
        }

        /// <summary>Brew pale ale on a fixed cadence and ship ready beer to any
        /// reachable market depot. Deterministic: no wall clock, no extra RNG.</summary>
        private static void RunAutopilot(GameState state, int days)
        {
            var breweries = state.World.Nodes.Where(n => n.Type == NodeType.Brewery).ToList();

            for (int day = 0; day < days; day++)
            {
                if (day % 14 == 0)
                    foreach (var b in breweries)
                        BrewingSystem.TryStartBrew(state, b.Id, new RecipeId("pale-ale"));

                foreach (var b in breweries)
                {
                    var batch = b.Batches.FirstOrDefault(x => x.IsSellable && x.VolumeLitres >= 100);
                    if (batch == null) continue;

                    var target = ReachableMarketFor(state, b.Id, batch.Style);
                    if (target != null)
                        LogisticsSystem.DispatchShipment(state, b.Id, target.Value, batch.Id,
                            100, PackagingType.WoodenCask, 1);
                }

                TickSystem.AdvanceDay(state);
            }
        }

        /// <summary>A reachable depot whose adjacent market has demand for the style,
        /// so the autopilot ships the right beer to the right market.</summary>
        private static NodeId? ReachableMarketFor(GameState state, NodeId from, BeerStyle style)
        {
            foreach (var m in state.Markets)
            {
                if (m.BaseDemandLitresPerTick(style) <= 0) continue;
                var depot = new NodeId(m.AdjacentNodeId);
                if (depot != from && state.World.HasRoute(from, depot))
                    return depot;
            }
            return null;
        }

        private static void PrintSummary(ScenarioDefinition scenario, Options opts, GameState state)
        {
            Console.WriteLine("=== Brewery Empire — headless run ===");
            Console.WriteLine("Scenario: " + scenario.Name + " (" + scenario.Id + ")");
            Console.WriteLine("Seed: " + opts.Seed + "   Days: " + opts.Days + "   Final date: " + state.Date);
            Console.WriteLine("Bankrupt: " + state.IsBankrupt);
            Console.WriteLine("Balance: " + state.Ledger.Balance + "   (" + state.Ledger.Entries.Count + " ledger entries)");
            Console.WriteLine("Research: " + string.Join(", ", state.Research.UnlockedTechIds));
            Console.WriteLine("Prestige: " + state.PrestigeBasisPoints + "   Reputation: " + state.ReputationBasisPoints);
            Console.WriteLine("Shipments in transit: " + state.Shipments.Count + "   Pending events: " + state.PendingEvents.Count);
            Console.WriteLine();

            Console.WriteLine("Sites:");
            foreach (var n in state.World.Nodes)
                Console.WriteLine("  " + n.Id.Value.PadRight(10) + " " + n.Type.ToString().PadRight(10) +
                                  " vessels=" + n.Vessels.Count + " batches=" + n.Batches.Count +
                                  " capacity=" + n.TotalCapacityLitres + "L");
            Console.WriteLine();

            Console.WriteLine("Markets:");
            foreach (var m in state.Markets)
                Console.WriteLine("  " + m.Id.PadRight(16) + " pop=" + m.Population.ToString().PadRight(6) +
                                  " reputation=" + m.ReputationBasisPoints);
            Console.WriteLine();

            Console.WriteLine("Staff: " + state.Staff.All.Count() + "   Rivals: " + state.Rivals.Count);
            Console.WriteLine();

            Console.WriteLine("Ledger by category:");
            foreach (var cat in Enum.GetValues<LedgerCategory>())
            {
                var total = state.Ledger.TotalFor(cat, scenario.Start, state.Date);
                if (!total.IsZero)
                    Console.WriteLine("  " + cat.ToString().PadRight(20) + " " + total);
            }
        }
    }
}
