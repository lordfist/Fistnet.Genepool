using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismDeadRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.IsOccupied && boardSquare.Occupant.IsDead)
            {
                byte remainingFood = 0;
                if (boardSquare.Occupant.FoodBalance > 0)
                    remainingFood = (byte)boardSquare.Occupant.FoodBalance;

                lock (boardSquare)
                {
                    boardSquare.IncreaseFood(remainingFood);
                    boardSquare.RemoveOccupant();
                }
            }
        }
    }
}