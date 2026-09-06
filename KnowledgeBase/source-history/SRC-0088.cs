using System.Diagnostics;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

// Explicitly invoked R02 measurements. No normal test run creates files or runs a long scenario.
internal static class DiagnosticScenarios
{
    private const int MaximumSeasons = 2048;
    private const int MaximumBatchSamples = 256;
    private static readonly object OutputLock = new();

    internal record Options(int Seed, int Seasons, SimulationMode Mode, bool Diagnostics,
        int Batch, bool Render, int Density = 10, string Fixture = null);

    internal static int RunCli(string[] args)
    {
        try
        {
            Options options;
            if (args[0] == "--diagnostic-fixture")
            {
                if (args.Length is < 4 or > 5)
                    throw new ArgumentException("Use --diagnostic-fixture <0|10|50|100 density> <early|large> <on|off> [1..64 age batches].");
                int density = int.Parse(args[1]);
                if (!new[] { 0, 10, 50, 100 }.Contains(density)) throw new ArgumentException("Fixture density must be 0, 10, 50 or 100.");
                if (args[2] != "early" && args[2] != "large") throw new ArgumentException("Fixture history must be early or large.");
                int batches = args.Length == 5 ? int.Parse(args[4]) : 4;
                if (batches < 1 || batches > 64) throw new ArgumentException("Use 1..64 fixture age batches.");
                options = new(29, batches * 8, SimulationMode.Production, OnOff(args[3]), 8, false, density, args[2]);
            }
            else
            {
                if (args.Length is < 7 or > 8)
                    throw new ArgumentException("Use --diagnostic-scenario <seed> <1..2048 seasons> <reference|production> <on|off> <1|8 batch> <render|no-render> [0..100 density].");
                var mode = args[3] == "reference" ? SimulationMode.DeterministicReference
                    : args[3] == "production" ? SimulationMode.Production : throw new ArgumentException("Unknown mode.");
                bool render = args[6] == "render" ? true : args[6] == "no-render" ? false : throw new ArgumentException("Use render or no-render.");
                options = new(int.Parse(args[1]), int.Parse(args[2]), mode, OnOff(args[4]), int.Parse(args[5]), render,
                    args.Length == 8 ? int.Parse(args[7]) : 10);
            }
            return Run(options);
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }

    private static bool OnOff(string text) => text == "on" ? true : text == "off" ? false
        : throw new ArgumentException("Diagnostics must be on or off.");

    private static int Run(Options options)
    {
        if (options.Seasons < 1 || options.Seasons > MaximumSeasons) throw new ArgumentOutOfRangeException(nameof(options.Seasons));
        if (options.Batch != 1 && options.Batch != 8) throw new ArgumentException("Batch must be 1 or 8.");
        if (options.Density < 0 || options.Density > 100) throw new ArgumentOutOfRangeException(nameof(options.Density));
        var total = Stopwatch.StartNew();
        int completedSeason = 0, finished = 0, terminalWriteStarted = 0;
        // A hard process-local fallback retains a minimal final line even if an engine call or export stalls.
        // It reads no live world/session state. The parent process still owns the external 120-second limit.
        using var timeout = new System.Threading.Timer(_ =>
        {
            if (Interlocked.CompareExchange(ref finished, 1, 0) != 0) return;
            try
            {
                Write(new { kind = "final", complete = false, reason = "115-second process budget; an in-flight operation may be unfinished",
                    requestedSeasons = options.Seasons, completedSeasons = Volatile.Read(ref completedSeason),
                    seconds = total.Elapsed.TotalSeconds, stateVerification = "Only previously emitted checkpoints are verified." });
            }
            finally { Volatile.Write(ref finished, 2); Environment.Exit(3); }
        }, null, TimeSpan.FromSeconds(115), Timeout.InfiniteTimeSpan);

        void WriteFailure(Exception error) => Write(new { kind = "final", complete = false, reason = "Exception",
            requestedSeasons = options.Seasons, completedSeasons = Volatile.Read(ref completedSeason),
            seconds = total.Elapsed.TotalSeconds, error = error.ToString() }, writing: () => Volatile.Write(ref terminalWriteStarted, 1));

        int Finish(int exitCode, Action emit)
        {
            // Reserve before exporting, not after writing. Only one path may produce a terminal record.
            if (Interlocked.CompareExchange(ref finished, 1, 0) != 0)
            {
                SpinWait.SpinUntil(() => Volatile.Read(ref finished) == 2, 1000);
                return 3;
            }
            try { emit(); return exitCode; }
            catch (Exception ex)
            {
                // A failed stdout write may already have delivered bytes. Do not emit a second final.
                if (Volatile.Read(ref terminalWriteStarted) == 0) WriteFailure(ex);
                else Console.Error.WriteLine("Terminal output failed; completion is not verified: " + ex);
                return 1;
            }
            finally { Volatile.Write(ref finished, 2); }
        }

        SimulationDiagnostics.Detach();
        long allocationStart = GC.GetTotalAllocatedBytes(false);
        int[] collectionStart = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        long engineCallTicks = 0, rendererTicks = 0, validationTicks = 0, exportTicks = 0;
        var sampleWindow = new Queue<BatchSample>();
        var segments = new[] { new TimingSegment("early"), new TimingSegment("middle"), new TimingSegment("late") };
        int batchCount = 0, sampleOmissions = 0;
        int? extinction = null;
        DiagnosticSession session = null;
        GameboardBitmap renderer = null;
        try
        {
            if (options.Fixture == null)
                Board.Reset(new SimulationRunOptions { Mode = options.Mode, Seed = options.Seed, InitialPopulationPercent = options.Density });
            else SetupFixture(options.Density, options.Fixture == "large" ? 32 : 0);
            if (options.Diagnostics) SimulationDiagnostics.Attach(session = new DiagnosticSession(128, 256));
            if (options.Render) renderer = new GameboardBitmap(800);

            object Metrics()
            {
                var cells = Board.BoardElement.Cast<BoardSquare>();
                long food = 0, reserves = 0, contexts = 0, entries = 0;
                int population = 0, maximumContexts = 0, maximumEntries = 0;
                foreach (var cell in cells)
                {
                    food += cell.FoodRemaining;
                    if (!cell.IsOccupied) continue;
                    population++; reserves += cell.Occupant.FoodBalance;
                    int c = cell.Occupant.Brain.LearningContextCount, e = cell.Occupant.Brain.LearningEntryCount;
                    contexts += c; entries += e; maximumContexts = Math.Max(maximumContexts, c); maximumEntries = Math.Max(maximumEntries, e);
                }
                if (population == 0 && extinction == null) extinction = Board.Season;
                return new { population, boardFood = food, organismReserves = reserves, learningContexts = contexts,
                    learningEntries = entries, maximumContexts, maximumEntries };
            }

            void Emit(string kind, bool complete, string reason = null)
            {
                long exportStart = Stopwatch.GetTimestamp();
                object metrics = Metrics();
                string state = options.Mode == SimulationMode.DeterministicReference ? SimulationSnapshot.Capture().Sha256 : null;
                DiagnosticReport diagnostics = session?.Export();
                object exportedDiagnostics = diagnostics == null ? null : kind == "final" ? diagnostics : new
                {
                    diagnostics.Schema, diagnostics.StopwatchFrequency, diagnostics.Totals, diagnostics.TimingsTicks,
                    diagnostics.TimingSamples, diagnostics.EventSamplesOmitted, diagnostics.SeasonReportsOmitted,
                    coverage = "Aggregate checkpoint only; bounded event and season samples appear in the final result. Census totals sum per-season observations, including per-season maximum values."
                };
                exportTicks += Stopwatch.GetTimestamp() - exportStart;
                double? engineOnly = diagnostics == null ? Seconds(engineCallTicks)
                    : Seconds(diagnostics.TimingsTicks.GetValueOrDefault("engine.season"));
                Write(new
                {
                    kind, complete, reason, options, requestedSeasons = options.Seasons, completedSeasons = Board.Season,
                    firstExtinctionObservedAtBatchEnd = extinction, metrics,
                    random = new { algorithm = Common.RandomSource.Algorithm, draws = Common.RandomSource.DrawCount, stateSha256 = state },
                    timing = new { engineSeconds = engineOnly, engineCallSeconds = Seconds(engineCallTicks), rendererSeconds = Seconds(rendererTicks),
                        validationSeconds = Seconds(validationTicks), exportPreparationSeconds = Seconds(exportTicks), totalSeconds = total.Elapsed.TotalSeconds,
                        explanation = "Engine call includes optional diagnostics census; engineSeconds uses engine.season when enabled. Export includes census/hash/session export. Final JSON encoding and stdout are excluded from the reported total; prior output is included. No actual WinForms paint is measured by this renderer scenario.",
                        warmup = "No discarded warmup. Reset/setup and initial export are in total; first measured batch includes any remaining JIT/first-use work. Early/middle/late comparisons retain that fact." },
                    workload = options.Fixture == null ? "Unmodified model and initial settings at the requested density."
                        : "Synthetic no-op genes, reserve 100, two active learning contexts plus " + (options.Fixture == "large" ? 32 : 0)
                          + " external contexts per organism, eight entries each; actual production scheduler. This is not a natural late-run population.",
                    memory = new { allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocationStart,
                        gcCollections = Enumerable.Range(0, 3).Select(g => GC.CollectionCount(g) - collectionStart[g]).ToArray(),
                        scope = "Process-wide since before reset; includes setup, diagnostics, renderer, validation, exports and runtime activity; no forced GC." },
                    batchCount, batchSamples = sampleWindow.ToArray(), batchSamplesOmitted = sampleOmissions,
                    segments = segments.Select(s => s.Export()).ToArray(), diagnostics = exportedDiagnostics,
                    runtime = Environment.Version.ToString(), assemblySha256 = kind == "initial" ? Program.AssemblyIdentities() : null
                }, () => kind == "final" || Volatile.Read(ref finished) == 0,
                    () => { if (kind == "final") Volatile.Write(ref terminalWriteStarted, 1); });
            }

            Emit("initial", false, "Requested horizon not yet run.");
            while (Board.Season < options.Seasons)
            {
                if (total.Elapsed.TotalSeconds >= 110)
                {
                    return Finish(3, () => Emit("final", false, "Stopped at a completed batch before the 115-second process limit."));
                }
                int before = Board.Season;
                long start = Stopwatch.GetTimestamp();
                if (options.Batch == 8 && options.Seasons - before >= 8) Board.ExecuteOneAge();
                else Board.ExecuteSingleSeason(true);
                long engine = Stopwatch.GetTimestamp() - start;
                engineCallTicks += engine;
                Volatile.Write(ref completedSeason, Board.Season);
                start = Stopwatch.GetTimestamp();
                renderer?.RefreshAndResize();
                long render = Stopwatch.GetTimestamp() - start;
                rendererTicks += render;
                batchCount++;
                var sample = new BatchSample(before + 1, Board.Season, Seconds(engine), Seconds(render));
                if (sampleWindow.Count == MaximumBatchSamples) { sampleWindow.Dequeue(); sampleOmissions++; }
                sampleWindow.Enqueue(sample);
                int segment = Math.Min(2, before * 3 / options.Seasons);
                segments[segment].Add(engine);
                if (Board.BoardOrganismCount == 0 && extinction == null) extinction = Board.Season;
                bool checkpoint = Board.Season is 128 or 512 or 2048;
                if (checkpoint || Board.Season == options.Seasons)
                {
                    start = Stopwatch.GetTimestamp();
                    ScenarioRunner.ValidateWorld();
                    if (options.Fixture != null)
                        Check.Equal(Board.BOARD_SIZE * Board.BOARD_SIZE * options.Density / 100, Board.BoardOrganismCount, "Synthetic workload population changed");
                    validationTicks += Stopwatch.GetTimestamp() - start;
                    if (Board.Season == options.Seasons) return Finish(0, () => Emit("final", true));
                    Emit("checkpoint", false);
                }
            }
            return Finish(0, () => Emit("final", true));
        }
        catch (Exception ex)
        {
            return Finish(1, () => WriteFailure(ex));
        }
        finally
        {
            renderer?.Dispose();
            SimulationDiagnostics.Detach();
        }
    }

    internal static void SetupFixture(int density, int externalContexts)
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.Production, Seed = 29, InitialPopulationPercent = density },
            (x, y) =>
            {
                if ((x * Board.BOARD_SIZE + y) % 100 >= density) return null;
                var organism = new FixtureOrganism();
                organism.SetState(food: 100);
                organism.SetGenes((owner, index) => new NoOpElement(owner, index));
                var mesh = Check.Scores(organism);
                mesh[0] = Scores();
                mesh[organism.DnaCode] = Scores();
                for (int context = 0; context < externalContexts; context++) mesh[-1 - context] = Scores();
                return organism;
            });
    }

    private static Dictionary<byte, float> Scores() => Enumerable.Range(0, 8).ToDictionary(i => (byte)i, _ => 0f);
    private static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;
    private static void Write(object result, Func<bool> allowed = null, Action writing = null)
    {
        string json = JsonSerializer.Serialize(result);
        lock (OutputLock)
        {
            if (allowed?.Invoke() == false) return;
            writing?.Invoke();
            Console.WriteLine(json); Console.Out.Flush();
        }
    }
    private record BatchSample(int FirstSeason, int LastSeason, double EngineCallSeconds, double RendererSeconds);

    private sealed class TimingSegment
    {
        private readonly string name;
        private readonly List<long> samples = new(64);
        private int count;
        private long total, minimum = long.MaxValue, maximum;
        public TimingSegment(string name) { this.name = name; }
        public void Add(long ticks)
        {
            count++; total += ticks; minimum = Math.Min(minimum, ticks); maximum = Math.Max(maximum, ticks);
            // Keep the latest 64 samples per segment, with exact aggregate min/max/mean over all batches.
            if (samples.Count == 64) samples.RemoveAt(0);
            samples.Add(ticks);
        }
        public object Export()
        {
            var ordered = samples.OrderBy(t => t).ToArray();
            double? Percentile(double p) => ordered.Length == 0 ? null : Seconds(ordered[(int)Math.Ceiling(p * ordered.Length) - 1]);
            return new { name, batchCount = count, meanSeconds = count == 0 ? (double?)null : Seconds(total) / count,
                minimumSeconds = count == 0 ? (double?)null : Seconds(minimum), maximumSeconds = count == 0 ? (double?)null : Seconds(maximum),
                sampleCount = ordered.Length, omitted = count - ordered.Length, medianSeconds = Percentile(.5), p95Seconds = Percentile(.95),
                sampling = "Last 64 batch timings in each third of the requested horizon; percentiles describe retained samples only." };
        }
    }

    private sealed class NoOpElement : IDnaElement
    {
        public NoOpElement(Organism me, byte index) { Me = me; DnaSequenceIndex = index; }
        public int DnaCode => 42 + DnaSequenceIndex;
        public byte DnaSequenceIndex { get; }
        public TargetTypes Target => TargetTypes.Self;
        // The existing enum has no no-op type. This test-only tag is not a real Eat implementation.
        public DnaTypes DnaType => DnaTypes.Eat;
        public Organism Me { get; }
        public void ExecuteDna(Organism organismAffected) { }
        public IDnaElement CopyToChild(Organism child) => new NoOpElement(child, DnaSequenceIndex);
    }
}
