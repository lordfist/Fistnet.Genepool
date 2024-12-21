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

            Board.Season = 0;

            int random = Common.GetRandomIntegerSeed();

            Random rand = new Random(random);

            Parallel.For(0, BOARD_SIZE, (x) =>
                {
                    Parallel.For(0, BOARD_SIZE, (y) =>
                    {
                        BoardSquare square;

                        if (rand.Next(10000) % 10 == 0)
                            square = new BoardSquare(x, y, new Organism());
                        else
                            square = new BoardSquare(x, y, null);

                        Board.BoardElement[x, y] = square;
                    });
                });

            isInitialized = true;
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

                foreach (var item in Board.DnaUsageStatistics.Keys)
                {
                    Board.DnaUsageStatistics[item] = 0;
                }
            }
        }

        #endregion Statistical information.

        #region Execute all rules in order.

        public static void ExecuteSingleSeason(bool addStatistics)
        {
            if (!isInitialized)
                return;

            if (addStatistics)
                Board.ResetStatistics();

            bool seasonDone = false;

            while (!seasonDone)
            {
                Parallel.For(0, BOARD_SIZE, (x) =>
                {
                    Parallel.For(0, BOARD_SIZE, (y) =>
                    {
                        RuleManager.ExecuteCurrentRule(Board.BoardElement[x, y]);
                        if (addStatistics && RuleManager.IsLastRule && Board.BoardElement[x, y].IsOccupied)
                            Board.GetStatisticalInfo(Board.BoardElement[x, y].Occupant);
                    });
                });

                //for (int x = 0; x < BOARD_SIZE; x++)
                //{
                //    for (int y = 0; y < BOARD_SIZE; y++)
                //    {
                //        RuleManager.ExecuteCurrentRule(Board.BoardElement[x, y]);
                //        if (addStatistics && RuleManager.IsLastRule && Board.BoardElement[x, y].IsOccupied)
                //            Board.GetStatisticalInfo(Board.BoardElement[x, y].Occupant);
                //    }
                //}

                seasonDone = RuleManager.MoveToNextRule();
            }

            Board.Season++;

            GC.Collect();
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