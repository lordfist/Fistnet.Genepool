using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control;

namespace Fistnet.Genepool.Tests;

internal record TestCase(string Name, string Group, Action Run);

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            int expected = args[0] switch
            {
                "--all" or "--runner-negative-control" or "--baseline-smoke" or "--performance-micro" => 1,
                "--group" or "--render-preview" => 2,
                "--scenario" => 4,
                "--diagnostic-scenario" => args.Length == 8 ? 8 : 7,
                "--diagnostic-fixture" => args.Length == 5 ? 5 : 4,
                "--performance-fixture" => 5,
                "--ecology-scenario" => 6,
                _ => -1
            };
            if (args.Length != expected || args.Skip(1).Any(a => string.IsNullOrWhiteSpace(a) || a.StartsWith("--", StringComparison.Ordinal)))
            { Console.Error.WriteLine("Select exactly one supported command with its required arguments."); return 2; }
        }
        if (args.Length > 0 && args[0] is "--performance-fixture" or "--performance-micro")
            return PerformanceBenchmarks.RunCli(args);
        if (args.Length > 0 && args[0] == "--ecology-scenario")
            return EcologyScenarios.RunCli(args);
        if (args.Length > 0 && (args[0] == "--diagnostic-scenario" || args[0] == "--diagnostic-fixture"))
            return DiagnosticScenarios.RunCli(args);
        if (args.Length == 2 && args[0] == "--render-preview")
        { RenderingTests.ExportPreview(args[1]); return 0; }
        if (args.Length == 4 && args[0] == "--scenario")
        {
            try
            {
                var mode = args[3] == "reference" ? SimulationMode.DeterministicReference
                    : args[3] == "production" ? SimulationMode.Production : throw new ArgumentException("Unknown mode");
                Console.WriteLine(JsonSerializer.Serialize(ScenarioRunner.Run(int.Parse(args[1]), int.Parse(args[2]), mode)));
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
        }
        if (args.Contains("--runner-negative-control"))
        {
            try { Check.True(false, "intentional runner negative-control failure"); }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 7; }
            return 0;
        }
        if (args.Contains("--baseline-smoke"))
        {
            var timer = Stopwatch.StartNew();
            Board.InitalizeBoard(true);
            var population = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Board.ExecuteSingleSeason(true);
                population.Add(Board.BoardElement.Cast<BoardSquare>().Count(s => s.IsOccupied));
            }
            Console.WriteLine(JsonSerializer.Serialize(new { mode = "production", seasons = 4, population,
                seconds = timer.Elapsed.TotalSeconds, characterization = "Unseeded current production scheduler; not a reproducibility comparison." }));
            return 0;
        }
        string group = args.SkipWhile(a => a != "--group").Skip(1).FirstOrDefault();
        var cases = RegressionTests.Cases().ToList();
        AddCases(cases);
        if (group != null) cases = cases.Where(c => c.Group == group).ToList();
        else if (!args.Contains("--all")) cases = cases.Where(c => c.Group != "scenarios").ToList();
        if (cases.Count == 0) { Console.Error.WriteLine("No tests selected."); return 2; }
        var results = new List<object>();
        int failed = 0;
        var total = Stopwatch.StartNew();
        foreach (var test in cases)
        {
            var timer = Stopwatch.StartNew();
            string error = null;
            try
            {
                Board.Reset(new SimulationRunOptions { Seed = 1729, InitialPopulationPercent = 0 });
                test.Run();
            }
            catch (Exception ex) { error = ex.GetBaseException().ToString(); failed++; }
            results.Add(new { name = test.Name, group = test.Group, passed = error == null,
                milliseconds = timer.Elapsed.TotalMilliseconds, error });
            Console.WriteLine($"{(error == null ? "PASS" : "FAIL")} {test.Name}{(error == null ? "" : ": " + error)}");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { total = cases.Count, passed = cases.Count - failed, failed,
            seconds = total.Elapsed.TotalSeconds, runtime = Environment.Version.ToString(),
            assemblySha256 = AssemblyIdentities(), results,
            scenarios = ScenarioRunner.EvaluationResults, uiDiagnostics = UiDiagnosticsTests.LatestMeasurement }));
        return failed == 0 ? 0 : 1;
    }

    internal static Dictionary<string, string> AssemblyIdentities() => new[]
        { typeof(Program).Assembly, typeof(Board).Assembly, typeof(Dna.Organism).Assembly,
          typeof(Visualization.GameboardBitmap).Assembly, typeof(App.MainForm).Assembly }
        .ToDictionary(a => a.GetName().Name, a => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))));

    private static void AddCases(List<TestCase> cases)
    {
        cases.AddRange(MechanicsTests.Cases());
        cases.AddRange(IntegrationTests.Cases());
        cases.AddRange(RenderingTests.Cases());
        cases.AddRange(ScenarioRunner.Cases());
        cases.AddRange(DiagnosticsTests.Cases());
        cases.AddRange(UiDiagnosticsTests.Cases());
        cases.AddRange(LearningPolicyTests.Cases());
        cases.AddRange(ActionTransactionTests.Cases());
        cases.AddRange(DnaActionTests.Cases());
        cases.AddRange(ViewerBackendTests.Cases());
        cases.AddRange(PerformanceBenchmarks.Cases());
        cases.AddRange(FounderSetupTests.Cases());
        cases.AddRange(EcologyTests.Cases());
        cases.AddRange(EcologyScenarios.Cases());
        cases.Add(new("runner malformed selection fails before any tests execute", "runner", () =>
        {
            foreach (string[] arguments in new[] { new[] { "--group" }, new[] { "--scenario", "11" },
                new[] { "--all", "--all" }, new[] { "--unknown" }, new[] { "--all", "extra" } })
            {
                var child = ScenarioRunner.ExecuteChild(arguments);
                Check.Equal(2, child.ExitCode); Check.True(!child.Output.Contains("PASS "), "Malformed selection ran tests");
            }
        }));
        cases.Add(new("runner negative control reports failure and exit 7", "runner", () =>
        {
            var child = ScenarioRunner.ExecuteChild("--runner-negative-control");
            Check.Equal(7, child.ExitCode);
            Check.True(child.Error.Contains("intentional runner negative-control failure"), "failure name missing");
        }));
    }
}
