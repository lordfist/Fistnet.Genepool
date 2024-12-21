using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismMoveRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.IsOccupied)
            {
                lock (Board.BoardElement)
                {
                    if (boardSquare.Occupant.NextRequestedPosition.HasValue)
                    {
                        boardSquare.TryMove(boardSquare.Occupant.NextRequestedPosition.Value);
                    }
                }
            }
        }
    }
}