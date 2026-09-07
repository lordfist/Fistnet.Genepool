using System.Diagnostics;
using System.Numerics;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

// Opt-in trial observation only. The board owner calls these hooks; no retained
// Organism, gene, decision, or RNG references enter the exported report.
internal sealed class EcologyMetrics : IDisposable
{
    private readonly HashSet<long> initialIds = new();
    private readonly Dictionary<ulong, int> patterns = new();
    private readonly EcologyAccumulator accumulator;
    private bool disposed;

    public EcologyMetrics()
    {
        if (!Board.IsInitialized || Board.Season != 0)
            throw new InvalidOperationException("Attach ecology observation immediately after reset, at season zero.");
        foreach (BoardSquare cell in Board.BoardElement)
            if (cell.IsOccupied) initialIds.Add(cell.Occupant.Id);
        long started = Stopwatch.GetTimestamp();
        accumulator = new EcologyAccumulator(Census());
        ObservationTicks += Stopwatch.GetTimestamp() - started;
        Board.BirthCompleted += Born;
        Board.SeasonActorCompleted += ActorCompleted;
        Board.SeasonCompleted += SeasonCompleted;
    }

    public EcologySample Latest => accumulator.Latest;
    public int CompletedSeasons => Latest.Season;
    public long ObservationTicks { get; private set; }
    public long CensusTicks { get; private set; }
    public EcologyReport Export() => accumulator.Export();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Board.BirthCompleted -= Born;
        Board.SeasonActorCompleted -= ActorCompleted;
        Board.SeasonCompleted -= SeasonCompleted;
    }

    private void Born(int x, int y)
    {
        long started = Stopwatch.GetTimestamp();
        Organism child = Board.BoardElement[x, y].Occupant;
        if (child == null) throw new InvalidOperationException("Birth notification has no placed child.");
        accumulator.RecordBirth(child.Generation);
        ObservationTicks += Stopwatch.GetTimestamp() - started;
    }

    private void ActorCompleted(Organism actor, ActionDecision decision, bool present, int x, int y)
    {
        long started = Stopwatch.GetTimestamp();
        if (!present) accumulator.RecordDeath(actor.Generation == 0, initialIds.Contains(actor.Id));
        if (decision != null) accumulator.RecordAction(decision.Candidate.Identity.Type, decision.Outcome);
        ObservationTicks += Stopwatch.GetTimestamp() - started;
    }

    private void SeasonCompleted(int season, int population)
    {
        long started = Stopwatch.GetTimestamp();
        accumulator.TakeSample(season, Census());
        ObservationTicks += Stopwatch.GetTimestamp() - started;
    }

    private EcologyCensus Census()
    {
        long started = Stopwatch.GetTimestamp();
        patterns.Clear();
        int population = 0, initialLiving = 0, founders = 0, descendants = 0, maximumGeneration = 0;
        long food = 0, reserves = 0, generationSum = 0;
        foreach (BoardSquare cell in Board.BoardElement)
        {
            food += cell.FoodRemaining;
            if (!cell.IsOccupied) continue;
            Organism organism = cell.Occupant;
            population++;
            if (initialIds.Contains(organism.Id)) initialLiving++;
            if (organism.Generation == 0) founders++; else descendants++;
            reserves += organism.FoodBalance;
            maximumGeneration = Math.Max(maximumGeneration, organism.Generation);
            generationSum += organism.Generation;
            // Targets can change during Move/Infect themselves: never cache by ID.
            ulong key = ExactPattern(organism.DnaSequence);
            patterns[key] = patterns.GetValueOrDefault(key) + 1;
        }
        ulong largestKey = 0;
        int largestCount = 0;
        foreach (var pair in patterns)
            if (pair.Value > largestCount || pair.Value == largestCount && pair.Key < largestKey)
            { largestKey = pair.Key; largestCount = pair.Value; }
        var census = new EcologyCensus(population, initialLiving, founders, descendants, maximumGeneration, generationSum,
            food, reserves, patterns.Count, largestKey, largestCount);
        CensusTicks += Stopwatch.GetTimestamp() - started;
        return census;
    }

    // Exactly eight ordered pairs; three action bits and four target bits per
    // pair fit in 56 bits. DnaCode, ancestry and learned scores are not identity.
    internal static ulong ExactPattern(IReadOnlyList<IDnaElement> genes)
    {
        if (genes.Count != Organism.DNA_SEQUENCE_MAXLENGTH)
            throw new ArgumentException("Ecological patterns require exactly eight ordered genes.");
        ulong key = 0;
        foreach (IDnaElement gene in genes)
        {
            int type = (int)gene.DnaType, target = (int)gene.Target;
            if (type is < 0 or > 7 || target is < 0 or > 8)
                throw new ArgumentException("Unknown ecological action or target.");
            key = (key << 7) | (uint)((type << 4) | target);
        }
        return key;
    }
}

internal readonly record struct EcologyCensus(int Population, int InitialCohortLiving, int FoundersLiving,
    int DescendantsLiving, int MaximumGeneration, long GenerationSum, long BoardFood, long OrganismReserves,
    int ExactPatternCount, ulong LargestPatternKey, int LargestPatternCount);

internal record EcologySample(int Season, int Population, double OccupancyFraction, double? InitialPopulationFraction,
    double? PeakPopulationFraction, double? PeakDrawdownFraction, int InitialCohortLiving, int FoundersLiving,
    int DescendantsLiving, int MaximumLivingGeneration, double? MeanLivingGeneration, int HighestGenerationObserved,
    long BoardFood, long OrganismReserves, int ExactPatternCount, string LargestPatternKey,
    int LargestPatternCount, double LargestPatternFraction, int SamePatternDominationStreak,
    long Births, long Deaths, long AttemptedActions, long CommittedActions, long EffectiveActions,
    long Moves, long GatheringActions, long DamagingAttacks, long PredationTransfers,
    long FoodGathered, long FoodSpent, long FoodTransferred, bool PopulationAccountingValid);

internal record EcologyActivityWindow(int FirstSeason, int LastSeason, long Births,
    int EffectiveActionTypeCount, string[] EffectiveActionTypes, bool HasBirthAndVariedActions);

internal record EcologyActionCounts(string Action, long Attempted, long Committed, long Effective);

internal record EcologyReport(string Schema, string PatternDefinition, string ActivityDefinition,
    int CompletedSeasons, int InitialPopulation, int MinimumPopulation, int PeakPopulation,
    double MaximumLargestPatternFraction, int? MaximumLargestPatternSeason,
    double? MaximumPeakDrawdownFraction, int? MaximumPeakDrawdownSeason,
    int? FirstCollapseSeason, int? FirstExtinctionSeason, int? FirstDominationSeason,
    int LongestSamePatternDominationStreak, bool? PopulationCriterionSatisfied,
    bool DominationCriterionSatisfied, bool? CompletedActivityWindowsSatisfied,
    int IncompleteActivityWindowSeasons, bool PopulationAccountingValid,
    long FounderDeaths, long DescendantDeaths, long InitialCohortDeaths,
    EcologySample Latest, EcologyActionCounts[] Actions, EcologyActivityWindow[] ActivityWindows,
    EcologySample[] Trajectory);

// Pure scalar accumulator enables boundary tests without manufacturing a long
// ecological world. Bounds are the frozen 2,048-season comparison horizon.
internal sealed class EcologyAccumulator
{
    internal const int MaximumSeasons = 2048;
    internal const int WindowSeasons = 128;
    internal const int WarmupSeasons = 128;
    private readonly List<EcologySample> trajectory = new(MaximumSeasons + 1);
    private readonly List<EcologyActivityWindow> windows = new(MaximumSeasons / WindowSeasons);
    private readonly long[] attempted = new long[8], committed = new long[8], effective = new long[8];
    private readonly int initialPopulation;
    private int minimumPopulation, peakPopulation, highestGeneration, streak, longestStreak;
    private int maximumPatternNumerator, maximumPatternDenominator, maximumDrawdownNumerator, maximumDrawdownDenominator;
    private int? maximumPatternSeason, maximumDrawdownSeason;
    private int? firstCollapse, firstExtinction, firstDomination;
    private ulong? streakPattern;
    private uint windowActions;
    private long births, deaths, founderDeaths, descendantDeaths, initialCohortDeaths, windowStartBirths;
    private long attemptedCount, committedCount, effectiveCount, moves, gathers, damagingAttacks, transfers;
    private long foodGathered, foodSpent, foodTransferred;
    private bool accountingValid = true;

    public EcologyAccumulator(EcologyCensus initial)
    {
        initialPopulation = minimumPopulation = peakPopulation = initial.Population;
        highestGeneration = initial.MaximumGeneration;
        if (initial.Population == 0) firstExtinction = 0;
        ObserveExtrema(0, initial);
        trajectory.Add(Sample(0, initial));
    }

    public EcologySample Latest => trajectory[^1];

    internal void RecordBirth(int generation)
    { births++; highestGeneration = Math.Max(highestGeneration, generation); }

    internal void RecordDeath(bool founder, bool initialCohort)
    {
        deaths++;
        if (founder) founderDeaths++; else descendantDeaths++;
        if (initialCohort) initialCohortDeaths++;
    }

    internal void RecordAction(DnaTypes type, ActionOutcome outcome)
    {
        int index = (int)type;
        if (index is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(type));
        if (outcome.Attempted) { attempted[index]++; attemptedCount++; }
        if (outcome.Committed) { committed[index]++; committedCount++; }
        // Count an observed state/resource effect, not merely a queued request.
        // Reserve expenditure is a real effect (including Eat at capped health).
        bool actual = outcome.Moved || outcome.BirthPlaced || outcome.FoodGathered > 0
            || outcome.FoodSpent > 0 || outcome.FoodTransferred > 0 || outcome.Damage > 0
            || outcome.Healing > 0 || outcome.Mutations > 0;
        if (outcome.Committed && actual)
        { effective[index]++; effectiveCount++; windowActions |= 1u << index; }
        if (outcome.Moved) moves++;
        if (outcome.FoodGathered > 0) gathers++;
        if (type == DnaTypes.Kill && outcome.Damage > 0) damagingAttacks++;
        if (outcome.FoodTransferred > 0) transfers++;
        foodGathered += outcome.FoodGathered;
        foodSpent += outcome.FoodSpent;
        foodTransferred += outcome.FoodTransferred;
    }

    internal void TakeSample(int season, EcologyCensus census)
    {
        if (season != Latest.Season + 1 || season > MaximumSeasons)
            throw new InvalidOperationException("Ecology samples must cover consecutive seasons 1..2048 without resets.");
        minimumPopulation = Math.Min(minimumPopulation, census.Population);
        peakPopulation = Math.Max(peakPopulation, census.Population);
        highestGeneration = Math.Max(highestGeneration, census.MaximumGeneration);
        ObserveExtrema(season, census);
        if (initialPopulation > 0 && (long)census.Population * 10 < initialPopulation)
            firstCollapse ??= season;
        if (census.Population == 0) firstExtinction ??= season;
        bool dominates = census.Population > 0 && (long)census.LargestPatternCount * 10 > (long)census.Population * 9;
        if (dominates)
        {
            streak = streakPattern == census.LargestPatternKey ? streak + 1 : 1;
            streakPattern = census.LargestPatternKey;
            longestStreak = Math.Max(longestStreak, streak);
            if (streak >= WindowSeasons) firstDomination ??= season;
        }
        else { streak = 0; streakPattern = null; }
        accountingValid &= initialPopulation + births - deaths == census.Population;
        if (season == WarmupSeasons)
        { windowActions = 0; windowStartBirths = births; }
        else if (season > WarmupSeasons && (season - WarmupSeasons) % WindowSeasons == 0)
        {
            int kinds = BitOperations.PopCount(windowActions);
            long newBirths = births - windowStartBirths;
            windows.Add(new(season - WindowSeasons + 1, season, newBirths, kinds,
                Enumerable.Range(0, 8).Where(i => (windowActions & 1u << i) != 0)
                    .Select(i => ((DnaTypes)i).ToString()).ToArray(), newBirths >= 1 && kinds >= 2));
            windowStartBirths = births; windowActions = 0;
        }
        trajectory.Add(Sample(season, census));
    }

    private void ObserveExtrema(int season, EcologyCensus census)
    {
        // Exact rational comparisons preserve intermediate extrema even when a
        // caller exports a sampled display trajectory. Equal maxima keep their
        // earliest season; zero-population pattern shares are not observations.
        if (census.Population > 0 && (maximumPatternDenominator == 0
            || (long)census.LargestPatternCount * maximumPatternDenominator
                > (long)maximumPatternNumerator * census.Population))
        {
            maximumPatternNumerator = census.LargestPatternCount;
            maximumPatternDenominator = census.Population;
            maximumPatternSeason = season;
        }
        int drawdown = peakPopulation - census.Population;
        if (peakPopulation > 0 && (maximumDrawdownDenominator == 0
            || (long)drawdown * maximumDrawdownDenominator > (long)maximumDrawdownNumerator * peakPopulation))
        {
            maximumDrawdownNumerator = drawdown;
            maximumDrawdownDenominator = peakPopulation;
            maximumDrawdownSeason = season;
        }
    }

    private EcologySample Sample(int season, EcologyCensus census) => new(season, census.Population,
        census.Population / (double)(Board.BOARD_SIZE * Board.BOARD_SIZE),
        initialPopulation == 0 ? null : census.Population / (double)initialPopulation,
        peakPopulation == 0 ? null : census.Population / (double)peakPopulation,
        peakPopulation == 0 ? null : 1 - census.Population / (double)peakPopulation,
        census.InitialCohortLiving, census.FoundersLiving, census.DescendantsLiving, census.MaximumGeneration,
        census.Population == 0 ? null : census.GenerationSum / (double)census.Population,
        highestGeneration, census.BoardFood, census.OrganismReserves, census.ExactPatternCount,
        census.Population == 0 ? null : census.LargestPatternKey.ToString("X14"), census.LargestPatternCount,
        census.Population == 0 ? 0 : census.LargestPatternCount / (double)census.Population, streak,
        births, deaths, attemptedCount, committedCount, effectiveCount, moves, gathers, damagingAttacks,
        transfers, foodGathered, foodSpent, foodTransferred,
        initialPopulation + births - deaths == census.Population);

    public EcologyReport Export() => new("genepool.ecology.v1",
        "Exactly eight ordered (DnaType, Target) pairs packed in 56 bits; excludes legacy DnaCode, ancestry and learned preferences.",
        "Analyzer-selected operationalization: ignore seasons 1..128 for activity; each complete nonoverlapping 128-season window thereafter needs >=1 placed birth and >=2 action types with a committed actual state/resource effect. No predator quota. Partial windows are unclassified.",
        Latest.Season, initialPopulation, minimumPopulation, peakPopulation,
        maximumPatternDenominator == 0 ? 0 : maximumPatternNumerator / (double)maximumPatternDenominator,
        maximumPatternSeason,
        maximumDrawdownDenominator == 0 ? null : maximumDrawdownNumerator / (double)maximumDrawdownDenominator,
        maximumDrawdownSeason, firstCollapse, firstExtinction,
        firstDomination, longestStreak, initialPopulation == 0 ? null : firstCollapse == null,
        firstDomination == null, windows.Count == 0 ? null : windows.All(w => w.HasBirthAndVariedActions),
        Math.Max(0, Latest.Season - WarmupSeasons) % WindowSeasons, accountingValid,
        founderDeaths, descendantDeaths, initialCohortDeaths, Latest,
        Enumerable.Range(0, 8).Select(i => new EcologyActionCounts(((DnaTypes)i).ToString(), attempted[i], committed[i], effective[i])).ToArray(),
        windows.Select(w => w with { EffectiveActionTypes = (string[])w.EffectiveActionTypes.Clone() }).ToArray(), trajectory.ToArray());
}
