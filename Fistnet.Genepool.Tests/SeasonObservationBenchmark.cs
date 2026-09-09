using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

// A bounded observation-cost measurement, not an ecological trial or a timing
// assertion. Full learning-state passivity is covered by SeasonObservationTests.
internal static class SeasonObservationBenchmark
{
    internal static object LatestMeasurement { get; private set; }

    internal static IEnumerable<TestCase> Cases()
    {
        yield return new("season observations: measure full-board observer and capture costs separately", "season-observation-cost", Measure);
    }

    private sealed record Measurement(bool Observed, double[] SeasonMilliseconds, double[] CaptureMilliseconds,
        long[] SeasonAllocatedBytes, long[] CaptureAllocatedBytes, string Digest, string RandomState,
        long RandomDraws, int Population, int CompletedSeasons);

    private static void Measure()
    {
        LatestMeasurement = null;
        const int warmup = 3, samples = 5, requestedPairs = 2;
        var timer = Stopwatch.StartNew();
        var pairs = new List<object>();
        var partial = new List<object>();
        bool complete = true;
        for (int repeat = 0; repeat < requestedPairs; repeat++)
        {
            if (timer.Elapsed.TotalSeconds >= 10) { complete = false; break; }
            // Reverse the order of the second pair to reduce simple order bias.
            Measurement first = Run(repeat == 1);
            if (first == null) { complete = false; break; }
            Measurement second = Run(repeat != 1);
            if (second == null) { partial.Add(first); complete = false; break; }
            Measurement off = first.Observed ? second : first;
            Measurement on = first.Observed ? first : second;
            Check.Equal((off.Digest, off.RandomState, off.RandomDraws, off.Population, off.CompletedSeasons),
                (on.Digest, on.RandomState, on.RandomDraws, on.Population, on.CompletedSeasons),
                "Observation changed the compact whole-board state or RNG in the controlled workload");
            double baseline = off.SeasonMilliseconds.Average(), observed = on.SeasonMilliseconds.Average();
            pairs.Add(new
            {
                repeat = repeat + 1, executionOrder = first.Observed ? "on, off" : "off, on",
                observerOff = off, observerOn = on,
                meanSeasonOverheadMilliseconds = observed - baseline,
                meanSeasonOverheadPercent = baseline > 0 ? (observed / baseline - 1) * 100 : (double?)null,
                meanCaptureMilliseconds = on.CaptureMilliseconds.Average(),
                meanAdditionalSeasonAllocatedBytes = on.SeasonAllocatedBytes.Average() - off.SeasonAllocatedBytes.Average(),
                meanCaptureAllocatedBytes = on.CaptureAllocatedBytes.Average()
            });
        }
        LatestMeasurement = new
        {
            schema = "r03-step4-observation-cost-v1", complete, requestedPairs, completedPairs = pairs.Count,
            population = 10000, warmupSeasons = warmup, measuredSeasonsPerRun = samples,
            elapsedSeconds = timer.Elapsed.TotalSeconds, pairs, partial,
            boundary = "10-second cooperative budget checked before setup and between completed seasons. An already-running season finishes; no timing threshold determines test success.",
            workload = "Deterministic reference mode, serial cell phases, seed 17293, 10000 occupied cells, fixed Move/Self repertoire, no births/deaths/movement. No selected organism. First three seasons and captures discarded; five measured seasons per run. Pair order alternates; setup, checks and compact digest are outside phase measurements.",
            allocationScope = "GC.GetAllocatedBytesForCurrentThread; fixture engine and collector both execute synchronously on this thread. No forced GC. Engine phase includes BeginSeason and observation callbacks; capture is measured separately.",
            verificationScope = "Paired digest compares every cell's food and occupant ID/position/health/reserves/age/sequence age/lifetime/DNA code and all gene slot/type/target/codes, plus board season/rule and RNG state/draw count. It excludes learning internals and does not replace the separate full-state passivity regression.",
            limitations = "Synthetic full occupancy with one action type measures this host/run only. It does not establish mixed-action or long-history throughput, and percentage differences can include JIT, GC and other host activity. Incomplete pairs are reported without comparison."
        };

        Measurement Run(bool observe)
        {
            if (timer.Elapsed.TotalSeconds >= 10) return null;
            Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 17293,
                InitialPopulationPercent = 100, CellExecution = CellExecutionMode.Serial }, (_, _) => CreateActor());
            using var collector = observe ? new ViewCollector() : null;
            var seasonTime = new List<double>(); var captureTime = new List<double>();
            var seasonBytes = new List<long>(); var captureBytes = new List<long>();
            for (int season = 0; season < warmup + samples; season++)
            {
                if (timer.Elapsed.TotalSeconds >= 10)
                {
                    partial.Add(new { observed = observe, completedSeasons = season, measuredSeasons = seasonTime.Count,
                        seasonMilliseconds = seasonTime, captureMilliseconds = captureTime,
                        seasonAllocatedBytes = seasonBytes, captureAllocatedBytes = captureBytes });
                    return null;
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                collector?.BeginSeason(); Board.ExecuteSingleSeason(false);
                double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Check.Equal(10000, Board.BoardOrganismCount, "Benchmark workload lost occupancy");
                double captured = 0; long capturedBytes = 0;
                if (collector != null)
                {
                    allocated = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
                    var frame = collector.Capture(1, 0, "Paused", false, null, 0, 0, 15);
                    captured = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    capturedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    Check.Equal(10000, frame.SeasonActions.Count, "Measured observer dropped actors");
                    Check.Equal(0L, frame.Births); Check.Equal(0L, frame.Deaths);
                }
                if (season >= warmup)
                { seasonTime.Add(elapsed); seasonBytes.Add(bytes); captureTime.Add(captured); captureBytes.Add(capturedBytes); }
            }
            return new Measurement(observe, seasonTime.ToArray(), captureTime.ToArray(), seasonBytes.ToArray(),
                captureBytes.ToArray(), CompactDigest(), Common.RandomSource.State, Common.RandomSource.DrawCount,
                Board.BoardOrganismCount, Board.Season);
        }
    }

    private static Organism CreateActor()
    {
        var actor = new FixtureOrganism(); actor.SetState(age: 1);
        actor.SetGenes((me, slot) =>
        {
            var gene = new MoveDnaElement(me, slot);
            Check.Property(gene, "Target", TargetTypes.Self); Check.Property(gene, "DnaCode", 0);
            return gene;
        });
        return actor;
    }

    private static string CompactDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var values = new long[12];
        for (int y = 0; y < Board.BOARD_SIZE; y++)
            for (int x = 0; x < Board.BOARD_SIZE; x++)
            {
                var square = Board.BoardElement[x, y]; var actor = square.Occupant;
                Check.True(actor != null, "Compact digest found an empty benchmark cell");
                values[0] = x; values[1] = y; values[2] = square.FoodRemaining; values[3] = actor.Id;
                values[4] = actor.Health; values[5] = actor.FoodBalance; values[6] = actor.Age;
                values[7] = actor.SequenceAge; values[8] = actor.LifetimeSeasons; values[9] = actor.DnaCode;
                values[10] = actor.Parent1Id; values[11] = actor.Parent2Id;
                hash.AppendData(MemoryMarshal.AsBytes(values.AsSpan()));
                foreach (var gene in actor.DnaSequence)
                {
                    values[0] = gene.DnaSequenceIndex; values[1] = (byte)gene.DnaType;
                    values[2] = (byte)gene.Target; values[3] = gene.DnaCode;
                    hash.AppendData(MemoryMarshal.AsBytes(values.AsSpan(0, 4)));
                }
            }
        hash.AppendData(Encoding.UTF8.GetBytes($"{Board.Season}|{RuleManager.CurrentRuleIndex}|{Common.RandomSource.State}|{Common.RandomSource.DrawCount}"));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
