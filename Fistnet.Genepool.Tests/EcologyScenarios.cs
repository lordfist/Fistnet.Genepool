using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Tests;

// Explicit Part 4 subprocess entry. Normal suites only exercise the pure input
// and candidate contracts below; they do not start long ecological comparisons.
internal static class EcologyScenarios
{
    private const int MaximumSeasons = 2048;
    private static readonly object OutputLock = new();
    internal record Options(int Seed, int Seasons, SimulationMode Mode, string Candidate, bool Observe);
    private record Criteria(int CompletedSeasons, int InitialPopulation, int MinimumPopulation, int PeakPopulation,
        int? FirstCollapseSeason, int? FirstExtinctionSeason, int? FirstDominationSeason,
        int LongestSamePatternDominationStreak, bool? PopulationCriterionSatisfied,
        bool DominationCriterionSatisfied, bool? CompletedActivityWindowsSatisfied,
        int CompleteActivityWindows, int IncompleteActivityWindowSeasons, bool PopulationAccountingValid);

    internal static Options Parse(string[] args)
    {
        if (args == null || args.Length != 6 || args[0] != "--ecology-scenario"
            || args.Any(string.IsNullOrWhiteSpace)
            || args.Skip(1).Any(a => a.StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException("Use --ecology-scenario <seed> <1..2048 seasons> <production|reference> <default|constrained|regrowth2|capped> <on|off>.");
        if (!int.TryParse(args[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int seed)
            || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out int seasons)
            || seasons < 1 || seasons > MaximumSeasons)
            throw new ArgumentException("Seed must be a signed 32-bit integer and seasons must be 1..2048.");
        SimulationMode mode = args[3] switch
        {
            "production" => SimulationMode.Production,
            "reference" => SimulationMode.DeterministicReference,
            _ => throw new ArgumentException("Mode must be production or reference.")
        };
        if (args[4] is not ("default" or "constrained" or "regrowth2" or "capped"))
            throw new ArgumentException("Unknown fixed ecological candidate.");
        bool observe = args[5] switch
        {
            "on" => true, "off" => false,
            _ => throw new ArgumentException("Observation must be on or off.")
        };
        return new(seed, seasons, mode, args[4], observe);
    }

    internal static SimulationRunOptions Settings(Options options)
    {
        if (options.Candidate is not ("default" or "constrained" or "regrowth2" or "capped"))
            throw new ArgumentException("Unknown fixed ecological candidate.");
        return new SimulationRunOptions
        {
            Mode = options.Mode, Seed = options.Seed,
            FounderRepertoire = options.Candidate == "constrained"
                ? FounderRepertoire.GatherAndReproduce : FounderRepertoire.UnrestrictedRandom,
            FoodRegrowthPerAge = options.Candidate == "regrowth2" ? 2 : 1,
            Policy = options.Candidate == "capped"
                ? new SimulationPolicy { Health = HealthPolicy.Capped, MaximumHealth = 50 }
                : new SimulationPolicy()
        };
    }

    internal static int RunCli(string[] args)
    {
        Options options;
        try { options = Parse(args); }
        catch (ArgumentException error) { Console.Error.WriteLine(error.Message); return 2; }
        return Run(options);
    }

    private static int Run(Options options)
    {
        var total = Stopwatch.StartNew();
        int completed = 0, terminal = 0;
        long engineTicks = 0, validationTicks = 0, exportTicks = 0, initialObservationTicks = 0;
        long allocations = GC.GetTotalAllocatedBytes(false);
        int[] collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        EcologyMetrics observer = null;
        Criteria lastFlushedCriteria = null;
        SimulationRunOptions settings = Settings(options);

        // A process-local hard fallback reads only a scalar completed-season
        // publication. It never snapshots a world that another thread is mutating.
        // Even a stalled final export is stopped; the parent has a 120 s fallback.
        using var watchdog = new System.Threading.Timer(_ =>
        {
            if (Volatile.Read(ref terminal) == 2) return;
            try
            {
                if (Interlocked.CompareExchange(ref terminal, 1, 0) == 0)
                {
                    Criteria retained = Volatile.Read(ref lastFlushedCriteria);
                    Write(new { kind = "final", actor = "Genepool Analyzer", complete = false,
                        reason = "115-second process watchdog; an in-flight operation may be unfinished.",
                        requestedSeasons = options.Seasons, completedSeasons = Volatile.Read(ref completed),
                        seconds = total.Elapsed.TotalSeconds, ecologicalScreen = Screen(retained, false, options),
                        criteria = retained, criteriaEvidenceSeason = retained?.CompletedSeasons,
                        coverage = "Criteria retain the last successfully flushed detached summary at criteriaEvidenceSeason. Later completed or in-flight state is unverified; horizon coverage is incomplete. A retained violation remains a failure." });
                }
            }
            finally { Environment.Exit(3); }
        }, null, TimeSpan.FromSeconds(115), Timeout.InfiniteTimeSpan);

        void Emit(string kind, bool complete, string reason = null)
        {
            long exportStart = Stopwatch.GetTimestamp();
            EcologyReport snapshot = observer?.Export();
            Criteria criteria = Summarize(snapshot);
            EcologyReport report = kind == "final" ? snapshot : null;
            // Threshold accounting is every season. This display trajectory is
            // deliberately sampled to avoid retaining huge repeated JSON exports.
            if (report != null)
                report = report with { Trajectory = report.Trajectory
                    .Where(s => s.Season % 8 == 0 || s.Season == report.CompletedSeasons).ToArray() };
            string stateHash = kind == "final" && options.Mode == SimulationMode.DeterministicReference
                ? SimulationSnapshot.Capture().Sha256 : null;
            string screen = Screen(criteria, complete, options);
            exportTicks += Stopwatch.GetTimestamp() - exportStart;
            Write(new
            {
                kind, actor = "Genepool Analyzer", schema = "genepool.ecology-scenario.v1", complete, reason,
                requestedSeasons = options.Seasons, completedSeasons = Board.Season,
                configuration = new
                {
                    seed = options.Seed, mode = options.Mode.ToString(), candidate = options.Candidate,
                    observer = options.Observe, settings.InitialPopulationPercent, settings.InitialCellFood,
                    settings.FoodRegrowthPerAge, settings.InitialOrganismFood,
                    founderRepertoire = settings.FounderRepertoire.ToString(),
                    cellExecution = settings.CellExecution.ToString(), policy = settings.Policy
                },
                metrics = observer?.Latest,
                random = new { algorithm = Common.RandomSource.Algorithm, draws = Common.RandomSource.DrawCount,
                    stateSha256 = stateHash },
                ecologicalScreen = screen, criteria, criteriaEvidenceSeason = criteria?.CompletedSeasons, ecology = report,
                coverage = new
                {
                    thresholds = options.Observe ? "Every completed season, with exact counters and fixed activity windows." : "Observer disabled for state/RNG passivity comparison; ecology not measured.",
                    trajectory = "Final report only: season 0, every eighth completed season, and actual final completed season. Intermediate exports contain the latest sample and cumulative criterion summary, including prior violations.",
                    horizon = "A passing ecological screen requires the full 2048 seasons; a shorter or timed-out prefix cannot pass. Observed violations remain violations.",
                    initialization = "Same seed is not cell-for-cell paired initialization when the founder repertoire changes its random draw sequence."
                },
                timing = new
                {
                    engineCallSeconds = Seconds(engineTicks),
                    collectorSeconds = Seconds(observer?.ObservationTicks ?? 0),
                    collectorCensusSeconds = Seconds(observer?.CensusTicks ?? 0),
                    initialCollectorSeconds = Seconds(initialObservationTicks),
                    engineExcludingCollectorSeconds = Seconds(Math.Max(0, engineTicks
                        - ((observer?.ObservationTicks ?? 0) - initialObservationTicks))),
                    validationSeconds = Seconds(validationTicks), exportPreparationSeconds = Seconds(exportTicks),
                    totalSeconds = total.Elapsed.TotalSeconds,
                    explanation = "Engine calls include collector callbacks; subtraction removes their measured bodies but not hook dispatch. Census is included in collector time. Initial collector census is outside engine calls. Export timing includes final state hashing; final JSON encoding/stdout is excluded. No discarded warmup, no rendering, no forced GC. Full board statistics run every eighth season."
                },
                memory = new { allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocations,
                    gcCollections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - collections[i]).ToArray(),
                    scope = "Process-wide since before reset, including setup, collector, validation and exports." },
                runtime = Environment.Version.ToString(), assemblySha256 = kind == "initial" ? Program.AssemblyIdentities() : null
            }, () => kind == "final" || Volatile.Read(ref terminal) == 0,
                () => Volatile.Write(ref lastFlushedCriteria, criteria));
        }

        int Finish(bool complete, string reason = null)
        {
            if (Interlocked.CompareExchange(ref terminal, 1, 0) != 0) return 3;
            try { Emit("final", complete, reason); Volatile.Write(ref terminal, 2); return complete ? 0 : 3; }
            catch (Exception error)
            {
                // Partial stdout is possible: do not invent a second final row.
                Console.Error.WriteLine("Final export failed; completion is unverified: " + error);
                Volatile.Write(ref terminal, 2);
                return 1;
            }
        }

        try
        {
            SimulationDiagnostics.Detach();
            Board.Reset(settings);
            if (options.Observe) observer = new EcologyMetrics();
            initialObservationTicks = observer?.ObservationTicks ?? 0;
            Emit("initial", false, "Requested horizon not yet run.");
            while (Board.Season < options.Seasons)
            {
                if (total.Elapsed.TotalSeconds >= 110)
                    return Finish(false, "Stopped at a completed season before the 115-second watchdog.");
                long started = Stopwatch.GetTimestamp();
                Board.ExecuteSingleSeason((Board.Season + 1) % 8 == 0);
                engineTicks += Stopwatch.GetTimestamp() - started;
                Volatile.Write(ref completed, Board.Season);
                if (Board.Season is 128 or 512 || Board.Season == options.Seasons)
                {
                    started = Stopwatch.GetTimestamp();
                    if (Board.Season == options.Seasons && Board.Season % 8 != 0) Board.RefreshStatistics();
                    ScenarioRunner.ValidateWorld();
                    validationTicks += Stopwatch.GetTimestamp() - started;
                    if (Board.Season == options.Seasons) return Finish(true);
                    Emit("checkpoint", false);
                }
                else if (Board.Season % 64 == 0) Emit("progress", false);
            }
            return Finish(true);
        }
        catch (Exception error)
        {
            if (Interlocked.CompareExchange(ref terminal, 1, 0) == 0)
            {
                Criteria retained = Volatile.Read(ref lastFlushedCriteria);
                try { Write(new { kind = "final", actor = "Genepool Analyzer", complete = false,
                    reason = "Exception; current world may be mid-season and is not exported.",
                    requestedSeasons = options.Seasons, completedSeasons = Volatile.Read(ref completed),
                    ecologicalScreen = Screen(retained, false, options), criteria = retained,
                    criteriaEvidenceSeason = retained?.CompletedSeasons,
                    coverage = "Criteria retain the last successfully flushed detached summary at criteriaEvidenceSeason. Later state is unverified and the requested horizon is incomplete.",
                    seconds = total.Elapsed.TotalSeconds, error = error.ToString() }); }
                finally { Volatile.Write(ref terminal, 2); }
            }
            return 1;
        }
        finally { observer?.Dispose(); SimulationDiagnostics.Detach(); }
    }

    private static Criteria Summarize(EcologyReport report) => report == null ? null : new(
        report.CompletedSeasons, report.InitialPopulation, report.MinimumPopulation, report.PeakPopulation,
        report.FirstCollapseSeason, report.FirstExtinctionSeason, report.FirstDominationSeason,
        report.LongestSamePatternDominationStreak, report.PopulationCriterionSatisfied,
        report.DominationCriterionSatisfied, report.CompletedActivityWindowsSatisfied,
        report.ActivityWindows.Length, report.IncompleteActivityWindowSeasons, report.PopulationAccountingValid);

    private static string Screen(Criteria report, bool complete, Options options)
    {
        if (!options.Observe) return "not_measured";
        if (report == null) return "inconclusive_horizon";
        if (!report.PopulationAccountingValid) return "invalid_accounting";
        if (report.PopulationCriterionSatisfied == false || !report.DominationCriterionSatisfied
            || report.CompletedActivityWindowsSatisfied == false) return "observed_failure";
        if (!complete || options.Seasons != MaximumSeasons || report.CompletedSeasons != MaximumSeasons)
            return "inconclusive_horizon";
        return report.PopulationCriterionSatisfied == true && report.CompletedActivityWindowsSatisfied == true
            ? "passed" : "undefined_criterion";
    }

    private static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;
    private static void Write(object value, Func<bool> allowed = null, Action flushed = null)
    {
        string json = JsonSerializer.Serialize(value);
        lock (OutputLock)
        {
            if (allowed?.Invoke() == false) return;
            Console.WriteLine(json);
            Console.Out.Flush();
            // Publish only after a successful flush, never before attempting I/O.
            flushed?.Invoke();
        }
    }

    internal static IEnumerable<TestCase> Cases()
    {
        yield return new("ecology flushed criteria retain recovered collapse across a later interruption", "ecology", () =>
        {
            EcologyCensus Census(int population, int initial, int descendants) => new(population, initial,
                initial, descendants, descendants > 0 ? 1 : 0, descendants, 0, 0, 2, 1, Math.Min(50, population));
            var accumulator = new EcologyAccumulator(Census(100, 100, 0));
            for (int season = 1; season <= 128; season++) accumulator.TakeSample(season, Census(100, 100, 0));
            for (int i = 0; i < 91; i++) accumulator.RecordDeath(true, true);
            accumulator.TakeSample(129, Census(9, 9, 0));
            for (int i = 0; i < 91; i++) accumulator.RecordBirth(1);
            for (int season = 130; season <= 192; season++) accumulator.TakeSample(season, Census(100, 9, 91));
            Criteria lastFlushed = Summarize(accumulator.Export());
            Check.Equal(192, lastFlushed.CompletedSeasons);
            Check.Equal((int?)129, lastFlushed.FirstCollapseSeason);
            Check.True(lastFlushed.PopulationAccountingValid, "Synthetic cohort accounting is invalid.");
            Check.Equal(100, accumulator.Latest.Population);
            var options = new Options(11, MaximumSeasons, SimulationMode.Production, "default", true);
            Check.Equal("observed_failure", Screen(lastFlushed, false, options));
            // The worker may finish more seasons without another successful flush.
            // Interruption must keep season192 evidence, not claim season200 state.
            for (int season = 193; season <= 200; season++) accumulator.TakeSample(season, Census(100, 9, 91));
            Check.Equal(200, accumulator.Latest.Season);
            Check.Equal(192, lastFlushed.CompletedSeasons);
            Check.Equal("observed_failure", Screen(lastFlushed, false, options));
            Check.Equal("inconclusive_horizon", Screen(null, false, options));
            Check.Equal("inconclusive_horizon", Screen(lastFlushed with
                { FirstCollapseSeason = null, PopulationCriterionSatisfied = true }, false, options));
            Check.Equal("observed_failure", Screen(lastFlushed with
                { FirstCollapseSeason = null, PopulationCriterionSatisfied = true,
                    FirstDominationSeason = 128, DominationCriterionSatisfied = false }, false, options));
            Check.Equal("observed_failure", Screen(lastFlushed with
                { FirstCollapseSeason = null, PopulationCriterionSatisfied = true,
                    CompletedActivityWindowsSatisfied = false }, false, options));
        });
        yield return new("ecology command rejects ambiguous and out-of-contract inputs without execution", "ecology", () =>
        {
            string[] good = { "--ecology-scenario", "11", "2048", "production", "default", "on" };
            var bad = new List<string[]> { Array.Empty<string>(), good[..5], good.Concat(new[] { "extra" }).ToArray() };
            foreach ((int index, string value) in new[] { (1, "2147483648"), (1, " 11"), (2, "0"), (2, "2049"),
                (2, "-1"), (3, "parallel"), (4, "combined"), (4, "baseline"), (5, "true"), (5, "--all") })
            { var candidate = (string[])good.Clone(); candidate[index] = value; bad.Add(candidate); }
            foreach (string[] args in bad)
            {
                bool rejected = false;
                try { Parse(args); } catch (ArgumentException) { rejected = true; }
                Check.True(rejected, "Invalid ecology input was accepted: " + string.Join(" ", args));
            }
            Check.Equal(new Options(11, 2048, SimulationMode.Production, "default", true), Parse(good));
        });
        yield return new("ecology candidates change exactly their prospective parameter", "ecology", () =>
        {
            foreach (string candidate in new[] { "default", "constrained", "regrowth2", "capped" })
            {
                var settings = Settings(new(29, 2048, SimulationMode.Production, candidate, true));
                settings.Validate();
                Check.Equal(10, settings.InitialPopulationPercent); Check.Equal(3, settings.InitialCellFood);
                Check.Equal(5, settings.InitialOrganismFood); Check.Equal(29, settings.Seed);
                Check.Equal(CellExecutionMode.Automatic, settings.CellExecution);
                Check.Equal(candidate == "regrowth2" ? 2 : 1, settings.FoodRegrowthPerAge);
                Check.Equal(candidate == "constrained" ? FounderRepertoire.GatherAndReproduce
                    : FounderRepertoire.UnrestrictedRandom, settings.FounderRepertoire);
                Check.Equal(candidate == "capped" ? HealthPolicy.Capped : HealthPolicy.LegacyOverweight, settings.Policy.Health);
                Check.Equal(LearningPolicy.RepairedLegacy, settings.Policy.Learning);
                Check.Equal(AttackPolicy.DamageOnly, settings.Policy.Attack);
                Check.Equal(50, settings.Policy.MaximumHealth); Check.Equal(64, settings.Policy.ExternalContextLimit);
                Check.Equal(10, settings.Policy.ExplorationPercent); Check.Equal(1, settings.Policy.AttackCost);
                Check.Equal(5, settings.Policy.AttackFoodLimit); Check.True(settings.RandomSource == null, "Candidate supplied an unexpected RNG.");
            }
        });
    }
}
