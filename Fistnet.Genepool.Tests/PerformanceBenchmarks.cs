using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control.Rules;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

// Explicit opt-in bounded measurements. These fixtures establish engineering
// cost at controlled load; they do not test ecological success or tune policies.
internal static class PerformanceBenchmarks
{
    internal static int RunCli(string[] args)
    {
        try
        {
            if (args[0] == "--performance-micro") return RunMicro();
            if (args.Length != 5 || !int.TryParse(args[1], out int density)
                || !new[] { 0, 10, 50, 100 }.Contains(density)
                || args[2] is not ("early" or "large") || args[3] is not ("serial" or "parallel")
                || !int.TryParse(args[4], out int batches) || batches < 1 || batches > 4)
                throw new ArgumentException("Use --performance-fixture <0|10|50|100> <early|large> <serial|parallel> <1..4 age batches>.");
            var execution = args[3] == "serial" ? CellExecutionMode.Serial : CellExecutionMode.BoundedParallel;
            return DiagnosticScenarios.Run(new(29, batches * 8, SimulationMode.Production, false, 8, false, density, args[2], execution));
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static FixtureOrganism HistoryActor(int contexts)
    {
        var actor = new FixtureOrganism();
        actor.SetGenes((owner, slot) => new PassiveGene(owner, slot));
        actor.SetState(age: 1, food: 10);
        var scores = Check.Scores(actor);
        scores["Self"] = Enumerable.Range(0, 8).ToDictionary(slot => (byte)slot, _ => 0f);
        for (int context = 0; context < contexts; context++)
            scores["E:" + (-1 - context)] = Enumerable.Range(0, 8).ToDictionary(slot => (byte)slot, _ => 0f);
        return actor;
    }

    private static int RunMicro()
    {
        SimulationDiagnostics.Detach();
        var total = Stopwatch.StartNew();
        var rows = new List<object>();
        foreach (int contexts in new[] { 0, 32 })
            foreach (string operation in new[] { "decision-and-completion", "child-construction-and-inheritance" })
            {
                int operations = operation == "decision-and-completion" ? 2000 : 200;
                var repeats = new List<object>();
                for (int repeat = 0; repeat <= 5; repeat++)
                {
                    if (total.Elapsed.TotalSeconds >= 25)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(new { complete = false, reason = "25-second batch budget", rows,
                            partial = new { operation, externalContexts = contexts, operationsPerRepeat = operations, repeats } }));
                        return 3;
                    }
                    Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 0 });
                    var first = HistoryActor(contexts); var second = HistoryActor(contexts);
                    second.CopyGenes(first); second.Brain.SynchronizeGenes();
                    var origin = Board.BoardElement[50, 50]; origin.AddOccupant(first);
                    long checksum = 0, started = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
                    for (int index = 0; index < operations; index++)
                    {
                        if (operation == "decision-and-completion")
                        {
                            var decision = first.Brain.Choose(ExecuteOrganismSequenceRule.BuildCandidates(origin), Common.Policy);
                            first.Brain.Complete(decision, new ActionOutcome { ActorPresent = true, TargetPresent = true,
                                ActorAfter = first.CreateSnapshot(), TargetAfter = first.CreateSnapshot() });
                            checksum += decision.Candidate.Identity.Slot;
                        }
                        else
                        {
                            var child = new Organism(first, second);
                            checksum += child.Brain.LearningEntryCount;
                        }
                    }
                    long allocationBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    double seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
                    if (repeat > 0) repeats.Add(new { repeat, seconds, allocationBytes, checksum, randomDraws = Common.RandomSource.DrawCount });
                }
                rows.Add(new { operation, externalContexts = contexts, operationsPerRepeat = operations, repeats });
            }
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "r02-part3-micro-v1", complete = true, rows,
            seconds = total.Elapsed.TotalSeconds, warmup = "One fixed-count repeat discarded per operation/history pair; five retained repeats.",
            scope = "Seed 29, fixed synthetic genes, 0 or 32 external contexts. Setup excluded. Decisions include candidate snapshots and completion; births include constructor/recombination/inheritance but no placement. Current-thread allocation only, no forced GC.",
            assemblySha256 = Program.AssemblyIdentities() }));
        return 0;
    }

    public static IEnumerable<TestCase> Cases()
    {
        yield return new("performance: unsorted candidates retain slot ordering, tie choice and draw count", "performance", () =>
        {
            var actor = HistoryActor(0);
            var candidates = actor.DnaSequence.AsEnumerable().Reverse().Select(gene => new ActionCandidate(gene, actor)).ToArray();
            var random = new ScriptedRandomSource(); Common.ConfigureRandom(random, true);
            var decision = actor.Brain.Choose(candidates, new SimulationPolicy());
            Check.Equal((byte)0, decision.Candidate.Identity.Slot);
            Check.Equal("Learned best", decision.ChoiceLabel);
            Check.Equal(0L, random.DrawCount);
            actor.Brain.Complete(decision, new ActionOutcome { ActorPresent = true, ActorAfter = actor.CreateSnapshot() });
            foreach (byte slot in Check.Scores(actor)["Self"].Keys.ToArray()) Check.Scores(actor)["Self"][slot] = -1;
            random = new ScriptedRandomSource(6); Common.ConfigureRandom(random, true);
            decision = actor.Brain.Choose(candidates, new SimulationPolicy());
            Check.Equal((byte)6, decision.Candidate.Identity.Slot);
            Check.Equal("Random all negative", decision.ChoiceLabel);
            Check.Equal(1L, random.DrawCount);
            actor.Brain.Complete(decision, new ActionOutcome { ActorPresent = true, ActorAfter = actor.CreateSnapshot() });
        });
        yield return new("performance: duplicate or foreign candidates fail before random choice", "performance", () =>
        {
            var actor = HistoryActor(0); var other = HistoryActor(0);
            var random = new ScriptedRandomSource(); Common.ConfigureRandom(random, true);
            foreach (var candidates in new[]
            {
                new[] { new ActionCandidate(actor.DnaSequence[0], actor), new ActionCandidate(actor.DnaSequence[0], actor) },
                new[] { new ActionCandidate(actor.DnaSequence[0], actor), new ActionCandidate(other.DnaSequence[1], actor) }
            })
            {
                bool rejected = false;
                try { actor.Brain.Choose(candidates, new SimulationPolicy()); } catch (ArgumentException) { rejected = true; }
                Check.True(rejected, "Invalid candidate list accepted");
                Check.Equal(0L, random.DrawCount);
            }
        });
        yield return new("performance: serial and bounded cell phases preserve full execution state", "performance", () =>
        {
            string serial = CaptureProduction(CellExecutionMode.Serial);
            string parallel = CaptureProduction(CellExecutionMode.BoundedParallel);
            Check.Equal(serial, parallel, "Changing only cell-local scheduling changed execution state or RNG");
        });
    }

    private static string CaptureProduction(CellExecutionMode execution)
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.Production, Seed = 29,
            RandomSource = new SeededRandomSource(29), InitialPopulationPercent = 10, CellExecution = execution });
        for (int season = 0; season < 16; season++) Board.ExecuteSingleSeason(true);
        ScenarioRunner.ValidateWorld();
        // Export at a quiescent boundary using the existing complete graph writer.
        // Both worlds actually ran production semantics. Only mode/scheduler
        // metadata is normalized here, explicitly; model state/RNG are untouched.
        var original = Board.RunOptions;
        var property = typeof(Board).GetProperty(nameof(Board.RunOptions), BindingFlags.Static | BindingFlags.Public);
        try
        {
            property.SetValue(null, new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29,
                RandomSource = Common.RandomSource, InitialPopulationPercent = 10 });
            return SimulationSnapshot.Capture().Sha256;
        }
        finally { property.SetValue(null, original); }
    }

    private sealed class PassiveGene : IDnaElement
    {
        public PassiveGene(Organism owner, byte slot) { Me = owner; DnaSequenceIndex = slot; }
        public int DnaCode => 42 + DnaSequenceIndex;
        public byte DnaSequenceIndex { get; }
        public TargetTypes Target => TargetTypes.Self;
        public DnaTypes DnaType => DnaTypes.Eat;
        public Organism Me { get; }
        public void ExecuteDna(Organism affected) { }
        public IDnaElement CopyToChild(Organism child) => new PassiveGene(child, DnaSequenceIndex);
    }
}
