using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismSequenceRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.IsOccupied)
            {
                TargetTypes target = boardSquare.Occupant.GetNextDnaSequenceTarget();
                BoardSquare targetSquare = boardSquare.GetNeightbor(target);

                lock (boardSquare)
                {
                    if (targetSquare == null)
                        boardSquare.Occupant.ExecuteNextDnaSequence(null);
                    else
                    {
                        boardSquare.Occupant.ExecuteNextDnaSequence(targetSquare.Occupant);
                    }

                    boardSquare.Occupant.Deactivate();
                }
            }
        }
    }
}