using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class EcologyTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("ecology: ordered action/target patterns ignore legacy codes and distinguish directions", "ecology", ExactPatterns);
        yield return new("ecology: collapse uses actual initial population, with exact equality and separate peak drawdown", "ecology", CollapseBoundaries);
        yield return new("ecology: unsampled diversity and peak drawdown extrema survive later recovery", "ecology", IntermediateExtrema);
        yield return new("ecology: empty initialization has no defined initial-relative survival criterion", "ecology", EmptyInitialization);
        yield return new("ecology: domination requires the same pattern strictly above 90 percent for 128 seasons", "ecology", DominationContinuity);
        yield return new("ecology: activity excludes warmup and requires births plus effective variety in each full window", "ecology", ActivityWindows);
        yield return new("ecology: attempts, commits and actual resource or movement effects remain distinct", "ecology", ActionOutcomes);
        yield return new("ecology: founder replacement and generation turnover preserve population accounting", "ecology", Turnover);
        yield return new("ecology: actual birth/death hooks reconcile the completed board and report generations", "ecology", ActualBoardEvents);
        yield return new("ecology: observation and age batching preserve small reference state and randomness", "ecology", PassiveReference);
        yield return new("ecology: disposal and detached exports prevent stale observers or mutable report state", "ecology", DisposalAndExports);
    }

    private static EcologyCensus Census(int population, int largest = 1, ulong key = 11,
        int initialLiving = -1, int founders = -1, int generation = 0) => new(population,
        initialLiving < 0 ? population : initialLiving, founders < 0 ? population : founders,
        founders < 0 ? 0 : population - founders, generation, (long)population * generation,
        30000, population * 5L, population == 0 ? 0 : population,
        key, population == 0 ? 0 : largest);

    private static void ExactPatterns()
    {
        var owner = new Organism();
        IDnaElement[] a = Enumerable.Range(0, 8).Select(i => (IDnaElement)new PatternGene(owner, (byte)i,
            (DnaTypes)i, (TargetTypes)i, 100 + i)).ToArray();
        IDnaElement[] equivalent = a.Select(g => (IDnaElement)new PatternGene(owner, g.DnaSequenceIndex,
            g.DnaType, g.Target, -700)).ToArray();
        ulong original = EcologyMetrics.ExactPattern(a);
        Check.Equal(original, EcologyMetrics.ExactPattern(equivalent), "Legacy DNA codes entered pattern identity");
        (a[0], a[1]) = (a[1], a[0]);
        Check.True(original != EcologyMetrics.ExactPattern(a), "Gene order was discarded");
        (a[0], a[1]) = (a[1], a[0]);
        ((PatternGene)a[1]).Target = TargetTypes.BottomRight;
        Check.True(original != EcologyMetrics.ExactPattern(a), "A changed target reused a stale pattern key");
        ((PatternGene)a[1]).Target = TargetTypes.TopCenter;
        Check.Equal(original, EcologyMetrics.ExactPattern(a));
        bool rejected = false;
        try { EcologyMetrics.ExactPattern(a.Take(7).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check.True(rejected, "A truncated genome silently collided with a full pattern");
    }

    private static void CollapseBoundaries()
    {
        var metrics = new EcologyAccumulator(Census(1000));
        for (int i = 0; i < 900; i++) metrics.RecordDeath(true, true);
        metrics.TakeSample(1, Census(100));
        Check.Equal(true, metrics.Export().PopulationCriterionSatisfied);
        Check.Equal(.1, metrics.Latest.InitialPopulationFraction);
        Check.Equal(.01, metrics.Latest.OccupancyFraction);
        metrics.RecordDeath(true, true);
        metrics.TakeSample(2, Census(99));
        Check.Equal(2, metrics.Export().FirstCollapseSeason);
        Check.Equal(false, metrics.Export().PopulationCriterionSatisfied);
        Check.True(metrics.Export().PopulationAccountingValid, "Known deaths failed population accounting");

        var peak = new EcologyAccumulator(Census(100));
        for (int i = 0; i < 900; i++) peak.RecordBirth(1);
        peak.TakeSample(1, Census(1000));
        for (int i = 0; i < 950; i++) peak.RecordDeath(false, false);
        peak.TakeSample(2, Census(50));
        Check.Equal(true, peak.Export().PopulationCriterionSatisfied, "Peak drawdown was treated as initial collapse");
        Check.Equal(.95, peak.Latest.PeakDrawdownFraction);
        Check.Equal(1000, peak.Export().PeakPopulation);
    }

    private static void EmptyInitialization()
    {
        var metrics = new EcologyAccumulator(Census(0));
        metrics.TakeSample(1, Census(0));
        var report = metrics.Export();
        Check.Equal<bool?>(null, report.PopulationCriterionSatisfied);
        Check.Equal<double?>(null, report.Latest.InitialPopulationFraction);
        Check.Equal<double?>(null, report.Latest.PeakDrawdownFraction);
        Check.Equal(0, report.FirstExtinctionSeason);
        Check.Equal<int?>(null, report.FirstDominationSeason);
        Check.Equal<string>(null, report.Latest.LargestPatternKey);
        Check.Equal<double?>(null, report.MaximumPeakDrawdownFraction);
        Check.Equal<int?>(null, report.MaximumLargestPatternSeason);
    }

    private static void IntermediateExtrema()
    {
        var metrics = new EcologyAccumulator(Census(100, largest: 50));
        for (int i = 0; i < 900; i++) metrics.RecordBirth(1);
        metrics.TakeSample(1, Census(1000, largest: 100));
        for (int i = 0; i < 980; i++) metrics.RecordDeath(false, false);
        metrics.TakeSample(2, Census(20, largest: 19));
        for (int i = 0; i < 180; i++) metrics.RecordBirth(2);
        for (int season = 3; season <= 8; season++) metrics.TakeSample(season, Census(200, largest: 100));
        var report = metrics.Export();
        Check.Equal(.95, report.MaximumLargestPatternFraction);
        Check.Equal<int?>(2, report.MaximumLargestPatternSeason);
        Check.Equal<double?>(.98, report.MaximumPeakDrawdownFraction);
        Check.Equal<int?>(2, report.MaximumPeakDrawdownSeason);
        Check.Equal(.5, report.Latest.LargestPatternFraction);
        Check.Equal<double?>(.8, report.Latest.PeakDrawdownFraction);
        var sampled = report with { Trajectory = report.Trajectory.Where(s => s.Season % 8 == 0).ToArray() };
        Check.True(sampled.Trajectory.All(s => s.Season != 2), "Fixture extremum was not between display samples");
        Check.Equal(.95, sampled.MaximumLargestPatternFraction);
        Check.Equal<double?>(.98, sampled.MaximumPeakDrawdownFraction);
        Check.True(report.PopulationAccountingValid, "Recovery fixture accounting failed");
        Check.Equal<bool?>(true, report.PopulationCriterionSatisfied, "Peak-relative decline was mislabeled as initial collapse");
    }

    private static void DominationContinuity()
    {
        var metrics = new EcologyAccumulator(Census(10, 10));
        for (int season = 1; season <= 127; season++) metrics.TakeSample(season, Census(10, 10));
        Check.Equal<int?>(null, metrics.Export().FirstDominationSeason);
        metrics.TakeSample(128, Census(10, 10, key: 22));
        Check.Equal(1, metrics.Latest.SamePatternDominationStreak, "Changed leader continued the preceding pattern's streak");
        for (int season = 129; season <= 255; season++) metrics.TakeSample(season, Census(10, 10, key: 22));
        Check.Equal(255, metrics.Export().FirstDominationSeason);
        metrics.TakeSample(256, Census(10, 9, key: 22));
        Check.Equal(0, metrics.Latest.SamePatternDominationStreak, "Exactly 90 percent was counted as greater than 90 percent");
        metrics.TakeSample(257, Census(10, 10, key: 22));
        Check.Equal(1, metrics.Latest.SamePatternDominationStreak);
        metrics.TakeSample(258, Census(0));
        Check.Equal(0, metrics.Latest.SamePatternDominationStreak);
        Check.Equal(128, metrics.Export().LongestSamePatternDominationStreak);
    }

    private static void ActivityWindows()
    {
        var metrics = new EcologyAccumulator(Census(100));
        int population = 100;
        for (int season = 1; season <= 512; season++)
        {
            if (season is 1 or 129 or 385)
            {
                metrics.RecordBirth(1); population++;
                metrics.RecordAction(DnaTypes.CombineDna, new() { Attempted = true, Committed = true, BirthPlaced = true });
                metrics.RecordAction(DnaTypes.GenerateFood, new() { Attempted = true, Committed = true, FoodGathered = 2 });
            }
            // A committed but blocked request must not supply activity variety.
            if (season == 257)
                metrics.RecordAction(DnaTypes.Move, new() { Attempted = true, Committed = true });
            metrics.TakeSample(season, Census(population));
            if (season == 128) Check.Equal(0, metrics.Export().ActivityWindows.Length, "Warmup was treated as an activity window");
            if (season == 255) Check.Equal(127, metrics.Export().IncompleteActivityWindowSeasons);
        }
        var report = metrics.Export();
        Check.Equal(3, report.ActivityWindows.Length);
        Check.Equal(129, report.ActivityWindows[0].FirstSeason);
        Check.Equal(256, report.ActivityWindows[0].LastSeason);
        Check.True(report.ActivityWindows[0].HasBirthAndVariedActions, "A valid first activity window failed");
        Check.Equal(1L, report.ActivityWindows[0].Births, "Warmup births leaked into a later window");
        Check.True(!report.ActivityWindows[1].HasBirthAndVariedActions, "Earlier activity rescued a later inactive window");
        Check.Equal(0, report.ActivityWindows[1].EffectiveActionTypeCount);
        Check.True(report.ActivityWindows[2].HasBirthAndVariedActions, "Independent later activity was lost");
        Check.Equal(false, report.CompletedActivityWindowsSatisfied);
        Check.True(report.PopulationAccountingValid, "Window fixture birth bookkeeping failed");
    }

    private static void ActionOutcomes()
    {
        var metrics = new EcologyAccumulator(Census(10));
        metrics.RecordAction(DnaTypes.Move, new() { Attempted = true });
        metrics.RecordAction(DnaTypes.Move, new() { Attempted = true, Committed = true });
        metrics.RecordAction(DnaTypes.Move, new() { Attempted = true, Committed = true, Moved = true });
        metrics.RecordAction(DnaTypes.GenerateFood, new() { Attempted = true, Committed = true, FoodGathered = 3 });
        metrics.RecordAction(DnaTypes.Kill, new() { Attempted = true, Committed = true, Damage = 4, FoodSpent = 1, FoodTransferred = 2 });
        metrics.RecordAction(DnaTypes.CombineDna, new() { Attempted = true });
        metrics.RecordAction(DnaTypes.Eat, new() { Attempted = true, Committed = true, FoodSpent = 2 });
        metrics.TakeSample(1, Census(10));
        var report = metrics.Export();
        var move = report.Actions.Single(a => a.Action == nameof(DnaTypes.Move));
        Check.Equal(3L, move.Attempted); Check.Equal(2L, move.Committed); Check.Equal(1L, move.Effective);
        Check.Equal(0L, report.Latest.Births, "An attempted birth became a placed birth");
        Check.Equal(4L, report.Latest.EffectiveActions);
        Check.Equal(1L, report.Latest.Moves); Check.Equal(1L, report.Latest.GatheringActions);
        Check.Equal(1L, report.Latest.DamagingAttacks); Check.Equal(1L, report.Latest.PredationTransfers);
        Check.Equal(3L, report.Latest.FoodGathered); Check.Equal(3L, report.Latest.FoodSpent);
        Check.Equal(2L, report.Latest.FoodTransferred);
    }

    private static void Turnover()
    {
        var metrics = new EcologyAccumulator(Census(10));
        for (int i = 0; i < 10; i++) { metrics.RecordBirth(1); metrics.RecordDeath(true, true); }
        metrics.TakeSample(1, Census(10, initialLiving: 0, founders: 0, generation: 1));
        for (int i = 0; i < 10; i++) { metrics.RecordBirth(2); metrics.RecordDeath(false, false); }
        metrics.TakeSample(2, Census(10, initialLiving: 0, founders: 0, generation: 2));
        var report = metrics.Export();
        Check.Equal(20L, report.Latest.Deaths);
        Check.Equal(10L, report.FounderDeaths); Check.Equal(10L, report.DescendantDeaths);
        Check.Equal(10L, report.InitialCohortDeaths);
        Check.Equal(0, report.Latest.FoundersLiving); Check.Equal(10, report.Latest.DescendantsLiving);
        Check.Equal(2, report.Latest.HighestGenerationObserved); Check.Equal(2.0, report.Latest.MeanLivingGeneration);
        Check.Equal(true, report.PopulationCriterionSatisfied, "Generational turnover was labeled collapse");
        Check.True(report.PopulationAccountingValid, "Births minus deaths did not explain population");
    }

    private static void ActualBoardEvents()
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 71, InitialPopulationPercent = 0 });
        var parent = new FixtureOrganism(); parent.SetState(food: 10, age: 1);
        parent.SetGenes((owner, slot) =>
        {
            var gene = new CombineDnaElement(owner, slot);
            Check.Property(gene, "Target", TargetTypes.Self);
            return gene;
        });
        var old = new FixtureOrganism(); old.SetState(age: Organism.MAX_AGE);
        Board.BoardElement[10, 10].AddOccupant(parent);
        Board.BoardElement[80, 80].AddOccupant(old);
        using var metrics = new EcologyMetrics();
        Board.ExecuteSingleSeason(true);
        var report = metrics.Export();
        Check.Equal(2, report.InitialPopulation); Check.Equal(1L, report.Latest.Births);
        Check.Equal(1L, report.Latest.Deaths); Check.Equal(2, report.Latest.Population);
        Check.Equal(1, report.Latest.InitialCohortLiving); Check.Equal(1, report.Latest.DescendantsLiving);
        Check.Equal(1, report.Latest.MaximumLivingGeneration);
        Check.True(report.PopulationAccountingValid, "Actual hooks did not reconcile completed occupancy");
        Check.True(metrics.CensusTicks > 0 && metrics.ObservationTicks >= metrics.CensusTicks, "Observation timing omitted its census");
    }

    private static void PassiveReference()
    {
        (string State, long Draws, string Metrics) Run(bool observe, bool ageBatch)
        {
            SimulationDiagnostics.Detach();
            Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 73, InitialPopulationPercent = 1 });
            using var metrics = observe ? new EcologyMetrics() : null;
            for (int i = 0; i < (ageBatch ? 2 : 16); i++)
                if (ageBatch) Board.ExecuteOneAge(); else Board.ExecuteSingleSeason(true);
            ScenarioRunner.ValidateWorld();
            return (SimulationSnapshot.Capture().Sha256, Common.RandomSource.DrawCount,
                metrics == null ? null : JsonSerializer.Serialize(metrics.Export()));
        }
        var plain = Run(false, false);
        var observed = Run(true, false);
        var batch = Run(true, true);
        Check.Equal(plain.State, observed.State, "Ecological observation changed complete reference state");
        Check.Equal(plain.Draws, observed.Draws, "Ecological observation consumed randomness");
        Check.Equal(observed.State, batch.State); Check.Equal(observed.Draws, batch.Draws);
        Check.Equal(observed.Metrics, batch.Metrics, "Age batching omitted intermediate ecology observations");
    }

    private static void DisposalAndExports()
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 71, InitialPopulationPercent = 0 });
        var metrics = new EcologyMetrics();
        var original = metrics.Export();
        original.Trajectory[0] = null;
        original.Actions[0] = new("fabricated", 99, 99, 99);
        Check.True(metrics.Export().Trajectory[0] != null, "An exported array mutated collector storage");
        Check.Equal(0L, metrics.Export().Actions[0].Attempted);
        metrics.Dispose(); metrics.Dispose();
        Board.ExecuteSingleSeason(true);
        Check.Equal(0, metrics.CompletedSeasons, "Disposed observer still sampled seasons");
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 79, InitialPopulationPercent = 0 });
        using var fresh = new EcologyMetrics();
        Board.ExecuteOneAge();
        Check.Equal(8, fresh.CompletedSeasons); Check.Equal(9, fresh.Export().Trajectory.Length);
        Check.Equal(0L, fresh.Latest.Births); Check.Equal(0L, fresh.Latest.CommittedActions);
        Check.Equal(0, metrics.CompletedSeasons, "A previous collector survived a reset");
    }

    private sealed class PatternGene : IDnaElement
    {
        public PatternGene(Organism owner, byte slot, DnaTypes type, TargetTypes target, int code)
        { Me = owner; DnaSequenceIndex = slot; DnaType = type; Target = target; DnaCode = code; }
        public int DnaCode { get; }
        public byte DnaSequenceIndex { get; }
        public TargetTypes Target { get; set; }
        public DnaTypes DnaType { get; }
        public Organism Me { get; }
        public void ExecuteDna(Organism organismAffected) => throw new InvalidOperationException("Pattern fixture is not executable.");
        public IDnaElement CopyToChild(Organism child) => new PatternGene(child, DnaSequenceIndex, DnaType, Target, DnaCode);
    }
}
