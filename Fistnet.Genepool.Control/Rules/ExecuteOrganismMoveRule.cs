using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismMoveRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            Execute(boardSquare, boardSquare.Occupant, null);
        }

        public static void Execute(BoardSquare boardSquare, Organism originalOccupant, ActionDecision decision)
        {
            if (originalOccupant == null) return;
            if (ReferenceEquals(boardSquare.Occupant, originalOccupant) && !originalOccupant.IsDead)
            {
                lock (Board.BoardElement)
                {
                    if (originalOccupant.NextRequestedPosition.HasValue)
                    {
                        boardSquare.TryMove(originalOccupant.NextRequestedPosition.Value, decision);
                    }
                }
            }
            else if (decision?.Candidate.Identity.Type == DnaTypes.Move)
            {
                decision.Outcome.Committed = false;
                decision.Outcome.Status = "movement.cancelled";
            }
            originalOccupant.MoveDone();
        }
    }
}
