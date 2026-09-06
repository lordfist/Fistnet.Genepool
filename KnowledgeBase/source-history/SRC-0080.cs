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

namespace Fistnet.Genepool.Control.Gameboard
{
    public static class Board
    {
        public const int BOARD_SIZE = 100;

        #region Properties.

        public static BoardSquare[,] BoardElement { get; private set; }
        public static SimulationRunOptions RunOptions { get; private set; } = new SimulationRunOptions();
        public static event Action<int, int> SeasonCompleted;

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
            if (options.InitialPopulationPercent < 0 || options.InitialPopulationPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(options.InitialPopulationPercent));
            RunOptions = options;
            bool reference = options.Mode == SimulationMode.DeterministicReference;
            Common.ConfigureRandom(options.RandomSource ?? (reference
                ? (IRandomSource)new SeededRandomSource(options.Seed) : new SystemRandomSource(options.Seed)), reference);
            isInitialized = false;
            Season = 0;
            RuleManager.Reset();
            ResetStatistics();
            BoardElement = new BoardSquare[BOARD_SIZE, BOARD_SIZE];
            // Initialization has one owner; never share an unprotected Random across workers.
            for (int x = 0; x < BOARD_SIZE; x++)
                for (int y = 0; y < BOARD_SIZE; y++)
                    BoardElement[x, y] = new BoardSquare(x, y, occupants != null ? occupants(x, y)
                        : (Common.GetRandomIntegerSeed(100) < options.InitialPopulationPercent ? new Organism() : null));
            isInitialized = true;
            RefreshStatistics();
        }

        #endregion Constructor.

        #region Statistical information.

        private static object syncObject = new object();

        public static int BoardOrganismCount { get; private set; }

        public static int LongestLiving { get; private set; }

        public static ConcurrentDictionary<DnaTypes, int> DnaUsageStatistics { get; private set; }

        public static ConcurrentDictionary<string, int> OrganismUsageStatistics { get; private set; }

        public static string GetOrganismDnaString(this Organism organism)
        {
            string dnaSequence = "";

            if (organism != null)
            {
                foreach (var item in organism.DnaSequence)
                {
                    dnaSequence += ((byte)item.DnaType).ToString() + ",";
                }
            }

            return dnaSequence;
        }

        private static void GetStatisticalInfo(Organism organism)
        {
            lock (syncObject)
            {
                Board.BoardOrganismCount++;

                if (organism.SequenceAge > Board.LongestLiving)
                    Board.LongestLiving = organism.SequenceAge;

                string dnaString = organism.GetOrganismDnaString();

                if (!Board.OrganismUsageStatistics.ContainsKey(dnaString))
                    Board.OrganismUsageStatistics.TryAdd(dnaString, 1);
                else
                    Board.OrganismUsageStatistics[dnaString]++;

                foreach (IDnaElement item in organism.DnaSequence)
                {
                    if (!Board.DnaUsageStatistics.ContainsKey(item.DnaType))
                        Board.DnaUsageStatistics.TryAdd(item.DnaType, 1);
                    else
                        Board.DnaUsageStatistics[item.DnaType]++;
                }
            }
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
            foreach (BoardSquare square in BoardElement)
                if (square != null && square.IsOccupied) GetStatisticalInfo(square.Occupant);
        }

        #endregion Statistical information.

        #region Execute all rules in order.

        // Diagnostics are external to simulation state; their presence never selects a rule or scheduler.
        private static readonly string[] diagnosticPhaseNames =
            { "phase.refresh", "phase.setup", "phase.action", "phase.effects", "phase.death", "phase.movement" };

        public static void ExecuteSingleSeason(bool addStatistics)
        {
            if (!isInitialized)
                return;

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

            while (!seasonDone)
            {
                int observedRule = RuleManager.CurrentRuleIndex;
                long phaseStarted = diagnostics != null ? Stopwatch.GetTimestamp() : 0;
                if (RunOptions.Mode == SimulationMode.DeterministicReference)
                {
                    for (int x = 0; x < BOARD_SIZE; x++)
                        for (int y = 0; y < BOARD_SIZE; y++)
                            RuleManager.ExecuteCurrentRule(BoardElement[x, y]);
                }
                else
                {
                    Parallel.For(0, BOARD_SIZE, x =>
                    {
                        Parallel.For(0, BOARD_SIZE, y => RuleManager.ExecuteCurrentRule(BoardElement[x, y]));
                    });
                }

                if (diagnostics != null)
                    diagnostics.Timing(diagnosticPhaseNames[observedRule], Stopwatch.GetTimestamp() - phaseStarted);
                seasonDone = RuleManager.MoveToNextRule();
            }

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
