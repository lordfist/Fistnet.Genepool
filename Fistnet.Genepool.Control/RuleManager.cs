using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control.Rules;

namespace Fistnet.Genepool.Control
{
    public static class RuleManager
    {
        private static List<IBoardSquareRule> ruleList;
        private static int currentRuleIndex;

        public static bool IsLastRule { get { return (currentRuleIndex >= (ruleList.Count - 1)); } }

        static RuleManager()
        {
            ruleList = new List<IBoardSquareRule>();

            ruleList.Add(new ExecuteRefreshRule());             // refresh the board
            ruleList.Add(new ExecuteOgranismSetup());           // setup the organism
            ruleList.Add(new ExecuteOrganismSequenceRule());    // execute single dna sequence
            ruleList.Add(new ExecuteOrganismEffectsRule());     // execute effects of a dna sequence
            ruleList.Add(new ExecuteOrganismDeadRule());        // remove dead organisms
            ruleList.Add(new ExecuteOrganismMoveRule());        // move organisms that requested move
            currentRuleIndex = 0;
        }

        public static bool MoveToNextRule()
        {
            bool allRulesDone = false;

            if (currentRuleIndex >= (ruleList.Count - 1))
            {
                currentRuleIndex = 0;
                allRulesDone = true;
            }
            else
            {
                currentRuleIndex++;
                allRulesDone = false;
            }
            return allRulesDone;
        }

        public static void ExecuteCurrentRule(BoardSquare boardSquare)
        {
            ruleList[currentRuleIndex].Execute(boardSquare);
        }
    }
}