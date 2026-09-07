using System.Diagnostics;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

internal static class ScenarioRunner
{
    internal record SeasonMetric(int Season, int Population, int TotalFood, int ActionPatterns,
        double LargestPatternFraction, int MinimumSequenceAge, int MaximumSequenceAge, double MeanSequenceAge);
    internal record Result(string Mode, int Seed, int Seasons, int InitialPopulationPercent, string RandomAlgorithm,
        long RandomDraws, string StateSha256, int? FirstExtinctionSeason, double Seconds, List<SeasonMetric> Trajectory,
        string Runtime, Dictionary<string, string> AssemblySha256);

    public static Result Run(int seed, int seasons, SimulationMode mode, int density = 10, bool observe = false)
    {
        if (seasons < 1 || seasons > 128) throw new ArgumentOutOfRangeException(nameof(seasons), "Use 1..128 seasons for the bounded suite.");
        var clock = Stopwatch.StartNew();
        Board.Reset(new SimulationRunOptions { Mode = mode, Seed = seed, InitialPopulationPercent = density });
        var history = new PopulationHistory();
        using var renderer = observe ? new GameboardBitmap(200) : null;
        void Observe(int season, int population) { history.Add(season, population); renderer.RefreshAndResize(); }
        if (observe) Board.SeasonCompleted += Observe;
        var trajectory = new List<SeasonMetric>();
        int? extinction = null;
        try
        {
            for (int i = 0; i < seasons; i++)
            {
                if (clock.Elapsed.TotalSeconds > 60) throw new TimeoutException("Scenario incomplete: 60-second budget exceeded.");
                Board.ExecuteSingleSeason(true);
                ValidateWorld();
                var squares = Board.BoardElement.Cast<BoardSquare>().ToArray();
                var occupants = squares.Where(s => s.IsOccupied).Select(s => s.Occupant).ToArray();
                if (occupants.Length == 0 && extinction == null) extinction = Board.Season;
                var patterns = occupants.GroupBy(o => string.Join(",", o.DnaSequence.Select(g => (int)g.DnaType)))
                    .Select(g => g.Count()).ToArray();
                trajectory.Add(new(Board.Season, occupants.Length, squares.Sum(s => (int)s.FoodRemaining), patterns.Length,
                    patterns.Length == 0 ? 0 : patterns.Max() / (double)occupants.Length,
                    occupants.Length == 0 ? 0 : occupants.Min(o => o.SequenceAge),
                    occupants.Length == 0 ? 0 : occupants.Max(o => o.SequenceAge),
                    occupants.Length == 0 ? 0 : occupants.Average(o => o.SequenceAge)));
            }
            if (observe)
            {
                Check.Equal(seasons, history.Count);
                Check.Equal(trajectory[^1].Population, history.Snapshot()[^1].Population);
            }
            string hash = mode == SimulationMode.DeterministicReference ? SimulationSnapshot.Capture().Sha256 : null;
            return new(mode.ToString(), seed, seasons, density, Common.RandomSource.Algorithm,
                Common.RandomSource.DrawCount, hash, extinction, clock.Elapsed.TotalSeconds, trajectory,
                Environment.Version.ToString(), Program.AssemblyIdentities());
        }
        finally { if (observe) Board.SeasonCompleted -= Observe; }
    }

    public static void ValidateWorld(bool allowSyntheticReserves = false)
    {
        var seen = new HashSet<Organism>(ReferenceEqualityComparer.Instance);
        var histogram = new Dictionary<DnaTypes, int>();
        int population = 0;
        foreach (BoardSquare cell in Board.BoardElement)
        {
            Check.True(cell.FoodRemaining <= BoardSquare.MAX_FOOD, "food exceeds cap");
            if (!cell.IsOccupied) continue;
            Check.True(seen.Add(cell.Occupant), "one organism occupies multiple cells");
            Check.True(!cell.Occupant.IsDead, "dead organism remains after shared resolution");
            Check.True(cell.Occupant.FoodBalance >= 0 && (allowSyntheticReserves || cell.Occupant.FoodBalance <= Organism.MAX_FOOD_CARRY),
                "organism reserves outside the declared capacity");
            population++;
            Check.True(Check.Field<Dictionary<string, Dictionary<byte, float>>>(cell.Occupant.Brain, "mesh")
                .SelectMany(p => p.Value.Values).All(float.IsFinite), "non-finite brain score");
            foreach (var gene in cell.Occupant.DnaSequence)
            {
                Check.True(ReferenceEquals(gene.Me, cell.Occupant), "gene has wrong owner");
                histogram[gene.DnaType] = histogram.GetValueOrDefault(gene.DnaType) + 1;
            }
        }
        Check.Equal(population, Board.BoardOrganismCount, "reported population differs from actual occupancy");
        foreach (DnaTypes type in Enum.GetValues<DnaTypes>())
            Check.Equal(histogram.GetValueOrDefault(type), Board.DnaUsageStatistics.GetValueOrDefault(type), "action histogram " + type);
    }

    public static ProcessStartInfo Child(params string[] arguments)
    {
        string executable = Environment.ProcessPath;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    public static (int ExitCode, string Output, string Error) ExecuteChild(params string[] args)
    {
        using var process = Process.Start(Child(args));
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000))
        {
            process.Kill(entireProcessTree: true); process.WaitForExit();
            throw new TimeoutException("Child scenario incomplete: 60-second budget exceeded.");
        }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    public static readonly List<Result> EvaluationResults = new();
    // Accepted Part 2, commit c5a494a. Part 3 observation/performance work must
    // retain the default model, not merely agree with another changed run.
    private static readonly Dictionary<int, (string Hash, long Draws)> Part2Reference = new()
    {
        [11] = ("9F48B02B3E1E752A8448BA87F703DD65CC77BAA5EB0AC3316145B8DFEFF600B6", 843754),
        [29] = ("AB4EF8C91070F6C53AFB9FC2BB1217E3D61898FA3CE2BD660156181424A194E2", 935898),
        [47] = ("E3A0251A828379FD3C11A594697467D31A0ED34E03E5711996B83C5D86B124E7", 868307),
        [83] = ("0C6021B6F48DAF361B927E4BBF29F60FC0A1F46603796B4286449BF1AFB33A21", 840616)
    };
    public static IEnumerable<TestCase> Cases()
    {
        foreach (int seed in new[] { 11, 29, 47, 83 })
            yield return new($"reference seed {seed}: repeatability and observer independence over 128 seasons", "scenarios", () =>
            {
                var plain = Run(seed, 128, SimulationMode.DeterministicReference);
                var observed = Run(seed, 128, SimulationMode.DeterministicReference, observe: true);
                Check.Equal(Part2Reference[seed].Hash, plain.StateSha256, "Part 3 changed accepted default reference state");
                Check.Equal(Part2Reference[seed].Draws, plain.RandomDraws, "Part 3 changed default random consumption");
                Check.Equal(plain.StateSha256, observed.StateSha256, "observer changed complete state");
                Check.Equal(plain.RandomDraws, observed.RandomDraws);
                Check.Equal(JsonSerializer.Serialize(plain.Trajectory), JsonSerializer.Serialize(observed.Trajectory));
                EvaluationResults.Add(plain);
            });
        yield return new("reference replay agrees across fresh processes", "scenarios", () =>
        {
            var first = ExecuteChild("--scenario", "11", "128", "reference");
            var second = ExecuteChild("--scenario", "11", "128", "reference");
            Check.Equal(0, first.ExitCode, first.Error); Check.Equal(0, second.ExitCode, second.Error);
            var a = JsonSerializer.Deserialize<Result>(first.Output); var b = JsonSerializer.Deserialize<Result>(second.Output);
            Check.Equal(a.StateSha256, b.StateSha256); Check.Equal(a.RandomDraws, b.RandomDraws);
        });
        foreach (int density in new[] { 0, 1, 100 })
            yield return new($"production smoke at {density}% initial occupancy", "integration", () =>
            {
                var result = Run(29, 8, SimulationMode.Production, density);
                Check.Equal(8, result.Trajectory.Count);
            });
    }
}
