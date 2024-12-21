using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOgranismSetup : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.FoodRemaining > BoardSquare.MAX_FOOD)
                boardSquare.SetFoodToMax();

            if (boardSquare.IsOccupied)
            {
                lock (boardSquare)
                {
                    boardSquare.ReduceFood(boardSquare.Occupant.GetAndResetTakenFood());
                    boardSquare.Occupant.SetAvailableFood(boardSquare.FoodRemaining);
                    boardSquare.Occupant.Activate();
                }
            }
        }
    }
}