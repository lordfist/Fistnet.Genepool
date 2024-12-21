using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismEffectsRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.IsOccupied)
            {
                lock (Board.BoardElement)
                {
                    boardSquare.Occupant.ExecuteEffectStack();
                    if (boardSquare.Occupant.HasChild)
                    {
                        BoardSquare freeSquare = boardSquare.GetFirstEmptySquare();
                        if (freeSquare != null && !freeSquare.IsOccupied)
                        {
                            freeSquare.AddOccupant(boardSquare.Occupant.Child);
                            boardSquare.Occupant.ReleaseChild();
                        }
                    }
                }
            }
        }
    }
}