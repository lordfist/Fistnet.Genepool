using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Control.Rules;

namespace Fistnet.Genepool.Control.Gameboard
{
    public static class Board
    {
        public const int BOARD_SIZE = 100;

        #region Properties.

        public static BoardSquare[,] BoardElement { get; private set; }
        public static SimulationRunOptions RunOptions { get; private set; } = new SimulationRunOptions();
        public static event Action<int, int> SeasonCompleted;
        internal static event Action<Organism, ActionDecision, bool, int, int> SeasonActorCompleted;
        internal static event Action<int, int> BirthCompleted;
        internal static void ObserveBirth(int x, int y) => BirthCompleted?.Invoke(x, y);

        #endregion Properties.

        #region Is initialized.

        private static bool isInitialized;

        public static bool IsInitialized
        {
            get { return Board.isInitialized; }
        }

        #endregion Is initialized.

        #region Board age.

        public static int Season { get; private set; }

        public static int Age { get { return Board.Season / Organism.DNA_SEQUENCE_MAXLENGTH; } }

        #endregion Board age.

        #region Constructor.

        static Board()
        {
            Board.BoardElement = new BoardSquare[BOARD_SIZE, BOARD_SIZE];
            Board.isInitialized = false;
            Board.BoardOrganismCount = 0;
            Board.DnaUsageStatistics = new ConcurrentDictionary<DnaTypes, int>();
            Board.OrganismUsageStatistics = new ConcurrentDictionary<string, int>();
        }

        public static void InitalizeBoard(bool restart = false)
        {
            if (isInitialized && !restart)
                return;
            Reset();
        }

        public static void Reset(SimulationRunOptions options = null, Func<int, int, Organism> occupants = null)
        {
            options = options ?? new SimulationRunOptions();
            options.Validate();
            RunOptions = options;
            bool reference = options.Mode == SimulationMode.DeterministicReference;
            Common.ConfigureRandom(options.RandomSource ?? (reference
                ? (IRandomSource)new SeededRandomSource(options.Seed) : new SystemRandomSource(options.Seed)), reference);
            Common.ConfigurePolicy(options.Policy);
            isInitialized = false;
            Season = 0;
            RuleManager.Reset();
            ResetStatistics();
            BoardElement = new BoardSquare[BOARD_SIZE, BOARD_SIZE];
            // Initialization has one owner; never share an unprotected Random across workers.
            for (int x = 0; x < BOARD_SIZE; x++)
                for (int y = 0; y < BOARD_SIZE; y++)
                    BoardElement[x, y] = new BoardSquare(x, y, occupants != null ? occupants(x, y)
                        : (Common.GetRandomIntegerSeed(100) < options.InitialPopulationPercent ? new Organism(options.InitialOrganismFood) : null),
                        (byte)options.InitialCellFood, (byte)options.FoodRegrowthPerAge);
            isInitialized = true;
            RefreshStatistics();
        }

        #endregion Constructor.

        #region Statistical information.

        public static int BoardOrganismCount { get; private set; }

        public static int LongestLiving { get; private set; }

        public static ConcurrentDictionary<DnaTypes, int> DnaUsageStatistics { get; private set; }

        public static ConcurrentDictionary<string, int> OrganismUsageStatistics { get; private set; }

        public static string GetOrganismDnaString(this Organism organism)
        {
            if (organism == null) return string.Empty;
            var dnaSequence = new StringBuilder(organism.DnaSequence.Count * 2);
            foreach (var item in organism.DnaSequence)
                dnaSequence.Append((byte)item.DnaType).Append(',');
            return dnaSequence.ToString();
        }

        private static void ResetStatistics()
        {
            lock (Board.DnaUsageStatistics)
            {
                Board.BoardOrganismCount = 0;
                Board.LongestLiving = 0;
                Board.OrganismUsageStatistics.Clear();

                Board.DnaUsageStatistics.Clear();
            }
        }

        public static void RefreshStatistics()
        {
            ResetStatistics();
            // One simulation owner visits the finished board. Aggregate locally,
            // publishing one update per distinct pattern/type instead of locking
            // and updating concurrent dictionaries for every individual gene.
            var patterns = new Dictionary<string, int>(StringComparer.Ordinal);
            var types = new Dictionary<DnaTypes, int>();
            foreach (BoardSquare square in BoardElement)
            {
                if (square == null || !square.IsOccupied) continue;
                Organism organism = square.Occupant;
                BoardOrganismCount++;
                LongestLiving = Math.Max(LongestLiving, organism.SequenceAge);
                string pattern = organism.GetOrganismDnaString();
                patterns.TryGetValue(pattern, out int patternCount);
                patterns[pattern] = patternCount + 1;
                foreach (IDnaElement gene in organism.DnaSequence)
                {
                    types.TryGetValue(gene.DnaType, out int typeCount);
                    types[gene.DnaType] = typeCount + 1;
                }
            }
            foreach (var pattern in patterns) OrganismUsageStatistics.TryAdd(pattern.Key, pattern.Value);
            foreach (var type in types) DnaUsageStatistics.TryAdd(type.Key, type.Value);
        }

        #endregion Statistical information.

        #region Execute all rules in order.

        // Diagnostics are external to simulation state; their presence never selects a rule or scheduler.
        private static readonly string[] diagnosticPhaseNames =
            { "phase.refresh", "phase.setup", "phase.action", "phase.effects", "phase.death", "phase.movement" };

        private sealed class SeasonActor
        {
            public BoardSquare Origin { get; }
            public Organism Organism { get; }
            public IReadOnlyList<ActionCandidate> Candidates { get; set; }
            public ActionDecision Decision { get; set; }
            public SeasonActor(BoardSquare origin) { Origin = origin; Organism = origin.Occupant; }
        }

        private static void ExecuteCellPhase()
        {
            // These two cell-local phases draw no randomness and share no
            // conflicts. Action choice/resolution and its shuffled order remain
            // serial. Automatic currently selects serial; the bounded alternative
            // remains selectable for explicit matched workload comparisons.
            if (RunOptions.Mode == SimulationMode.DeterministicReference
                || RunOptions.CellExecution != CellExecutionMode.BoundedParallel)
            {
                for (int x = 0; x < BOARD_SIZE; x++)
                    for (int y = 0; y < BOARD_SIZE; y++)
                        RuleManager.ExecuteCurrentRule(BoardElement[x, y]);
            }
            else
            {
                Parallel.For(0, BOARD_SIZE, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, x =>
                {
                    for (int y = 0; y < BOARD_SIZE; y++) RuleManager.ExecuteCurrentRule(BoardElement[x, y]);
                });
            }
        }

        public static void ExecuteSingleSeason(bool addStatistics)
        {
            if (!isInitialized)
                return;
            if (RuleManager.CurrentRuleIndex != 0)
                throw new InvalidOperationException("A season must begin at the refresh boundary.");

            DiagnosticSession diagnostics = SimulationDiagnostics.Current;
            long seasonStarted = 0;
            long allocatedBefore = 0;
            int gen0Before = 0, gen1Before = 0, gen2Before = 0;
            if (diagnostics != null)
            {
                diagnostics.BeginSeason(Season + 1);
                allocatedBefore = GC.GetTotalAllocatedBytes(false);
                gen0Before = GC.CollectionCount(0);
                gen1Before = GC.CollectionCount(1);
                gen2Before = GC.CollectionCount(2);
                seasonStarted = Stopwatch.GetTimestamp();
            }
            bool seasonDone = false;
            var cohort = new List<SeasonActor>();
            var locations = new Dictionary<Organism, BoardSquare>();

            int Gather(Organism recipient, int amount) => locations.TryGetValue(recipient, out BoardSquare source)
                ? source.GatherFood(recipient, amount) : 0;
            bool Birth(Organism parent, Organism other) => locations.TryGetValue(parent, out BoardSquare source)
                && ReferenceEquals(source.Occupant, parent) && other != null
                && locations.TryGetValue(other, out BoardSquare otherSource) && ReferenceEquals(otherSource.Occupant, other)
                && source.TryPlaceChild(other);

            while (!seasonDone)
            {
                int observedRule = RuleManager.CurrentRuleIndex;
                long phaseStarted = diagnostics != null ? Stopwatch.GetTimestamp() : 0;
                switch (observedRule)
                {
                    case 0:
                    case 1:
                        ExecuteCellPhase();
                        break;
                    case 2:
                        // Freeze all targets before any action or per-organism clock advances.
                        // This cohort alone acts, moves and learns; newborns begin next season.
                        foreach (BoardSquare square in BoardElement)
                            if (square.IsOccupied)
                            {
                                var actor = new SeasonActor(square);
                                cohort.Add(actor);
                                locations.Add(actor.Organism, square);
                                actor.Candidates = ExecuteOrganismSequenceRule.BuildCandidates(square);
                            }
                        foreach (SeasonActor actor in cohort)
                        {
                            actor.Decision = actor.Organism.PrepareSeason(actor.Candidates);
                            actor.Organism.Deactivate();
                            actor.Candidates = null;
                        }
                        diagnostics?.Count("actions.season_cohort", cohort.Count);
                        break;
                    case 3:
                        // Contention semantics are explicit in both modes: one seeded,
                        // uniformly shuffled serial priority, retained for movement too.
                        for (int i = cohort.Count - 1; i > 0; i--)
                        {
                            int j = Common.GetRandomIntegerSeed(i + 1);
                            SeasonActor swap = cohort[i]; cohort[i] = cohort[j]; cohort[j] = swap;
                        }
                        foreach (SeasonActor actor in cohort)
                            ExecuteOrganismEffectsRule.Resolve(actor.Organism, actor.Decision, Gather, Birth);
                        break;
                    case 4:
                        var removeDead = new ExecuteOrganismDeadRule();
                        foreach (SeasonActor actor in cohort)
                            if (ReferenceEquals(actor.Origin.Occupant, actor.Organism)) removeDead.Execute(actor.Origin);
                        break;
                    case 5:
                        foreach (SeasonActor actor in cohort)
                            ExecuteOrganismMoveRule.Execute(actor.Origin, actor.Organism, actor.Decision);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown season phase.");
                }

                if (diagnostics != null)
                    diagnostics.Timing(diagnosticPhaseNames[observedRule], Stopwatch.GetTimestamp() - phaseStarted);
                seasonDone = RuleManager.MoveToNextRule();
            }

            long evaluationStarted = diagnostics != null ? Stopwatch.GetTimestamp() : 0;
            var present = new HashSet<Organism>();
            foreach (BoardSquare square in BoardElement)
                if (square.IsOccupied) present.Add(square.Occupant);
            foreach (SeasonActor actor in cohort)
            {
                actor.Organism.FinishSeason(actor.Decision, present.Contains(actor.Organism),
                    actor.Decision?.Candidate.Target != null && present.Contains(actor.Decision.Candidate.Target));
                SeasonActorCompleted?.Invoke(actor.Organism, actor.Decision, present.Contains(actor.Organism),
                    actor.Origin.Position.X, actor.Origin.Position.Y);
            }
            diagnostics?.Timing("phase.evaluation", Stopwatch.GetTimestamp() - evaluationStarted);

            Board.Season++;
            // Read final occupancy after movement completes, never midway through that pass.
            long statisticsStarted = diagnostics != null ? Stopwatch.GetTimestamp() : 0;
            if (addStatistics) RefreshStatistics();
            else BoardOrganismCount = BoardElement.Cast<BoardSquare>().Count(s => s.IsOccupied);
            if (diagnostics != null)
            {
                diagnostics.Timing(addStatistics ? "statistics.full" : "statistics.population_only",
                    Stopwatch.GetTimestamp() - statisticsStarted);
                // Includes enabled in-engine hook overhead, but excludes the observer and extra census below.
                diagnostics.Timing("engine.season", Stopwatch.GetTimestamp() - seasonStarted);
            }
            long observerStarted = diagnostics != null ? Stopwatch.GetTimestamp() : 0;
            SeasonCompleted?.Invoke(Season, BoardOrganismCount);
            if (diagnostics != null)
            {
                diagnostics.Timing("observer.season_completed", Stopwatch.GetTimestamp() - observerStarted);
                long censusStarted = Stopwatch.GetTimestamp();
                RecordDiagnosticCensus(diagnostics);
                diagnostics.Timing("diagnostics.census", Stopwatch.GetTimestamp() - censusStarted);
                // Process-wide, approximate allocation delta includes diagnostic/observer work and other threads.
                diagnostics.Count("process_allocated_bytes", GC.GetTotalAllocatedBytes(false) - allocatedBefore);
                diagnostics.Count("gc.gen0", GC.CollectionCount(0) - gen0Before);
                diagnostics.Count("gc.gen1", GC.CollectionCount(1) - gen1Before);
                diagnostics.Count("gc.gen2", GC.CollectionCount(2) - gen2Before);
                // Excludes BeginSeason/CompleteSeason bookkeeping; caller wall time includes those costs.
                diagnostics.Timing("total.season", Stopwatch.GetTimestamp() - seasonStarted);
                diagnostics.CompleteSeason(Season, BoardOrganismCount);
            }
        }

        private static void RecordDiagnosticCensus(DiagnosticSession diagnostics)
        {
            long cells = 0, population = 0, food = 0, reserves = 0, contexts = 0, entries = 0;
            long pendingChildren = 0, pendingContexts = 0, pendingEntries = 0;
            int maxContexts = 0, maxEntries = 0;
            foreach (BoardSquare square in BoardElement)
            {
                if (square == null) continue;
                cells++;
                food += square.FoodRemaining;
                if (!square.IsOccupied) continue;
                Organism organism = square.Occupant;
                population++;
                reserves += organism.FoodBalance;
                int organismContexts = organism.Brain.LearningContextCount;
                int organismEntries = organism.Brain.LearningEntryCount;
                contexts += organismContexts;
                entries += organismEntries;
                maxContexts = Math.Max(maxContexts, organismContexts);
                maxEntries = Math.Max(maxEntries, organismEntries);
                if (organism.Child != null)
                {
                    pendingChildren++;
                    pendingContexts += organism.Child.Brain.LearningContextCount;
                    pendingEntries += organism.Child.Brain.LearningEntryCount;
                }
            }
            diagnostics.Count("census.cells", cells);
            diagnostics.Count("census.population", population);
            diagnostics.Count("census.food", food);
            diagnostics.Count("census.reserves", reserves);
            diagnostics.Count("census.learning_contexts", contexts);
            diagnostics.Count("census.learning_entries", entries);
            // These are per-season census observations. Run Count totals sum them; they are not run maxima.
            diagnostics.Count("census.max_learning_contexts", maxContexts);
            diagnostics.Count("census.max_learning_entries", maxEntries);
            diagnostics.Count("census.pending_children", pendingChildren);
            diagnostics.Count("census.pending_child_learning_contexts", pendingContexts);
            diagnostics.Count("census.pending_child_learning_entries", pendingEntries);
        }

        public static void ExecuteOneAge()
        {
            DiagnosticSession diagnostics = SimulationDiagnostics.Current;
            long batchStarted = diagnostics != null ? Stopwatch.GetTimestamp() : 0;
            for (int i = 0; i < Organism.DNA_SEQUENCE_MAXLENGTH; i++)
            {
                if (i == Organism.DNA_SEQUENCE_MAXLENGTH - 1)
                    Board.ExecuteSingleSeason(true);
                else
                    Board.ExecuteSingleSeason(false);
            }
            if (diagnostics != null)
                diagnostics.Timing("total.age_batch", Stopwatch.GetTimestamp() - batchStarted);
        }

        #endregion Execute all rules in order.
    }
}
