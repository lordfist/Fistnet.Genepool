using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public static void ExecuteSingleSeason(bool addStatistics)
        {
            if (!isInitialized)
                return;

            bool seasonDone = false;

            while (!seasonDone)
            {
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

                seasonDone = RuleManager.MoveToNextRule();
            }

            Board.Season++;
            // Read final occupancy after movement completes, never midway through that pass.
            if (addStatistics) RefreshStatistics();
            else BoardOrganismCount = BoardElement.Cast<BoardSquare>().Count(s => s.IsOccupied);
            SeasonCompleted?.Invoke(Season, BoardOrganismCount);
        }

        public static void ExecuteOneAge()
        {
            for (int i = 0; i < Organism.DNA_SEQUENCE_MAXLENGTH; i++)
            {
                if (i == Organism.DNA_SEQUENCE_MAXLENGTH - 1)
                    Board.ExecuteSingleSeason(true);
                else
                    Board.ExecuteSingleSeason(false);
            }
        }

        #endregion Execute all rules in order.
    }
}
