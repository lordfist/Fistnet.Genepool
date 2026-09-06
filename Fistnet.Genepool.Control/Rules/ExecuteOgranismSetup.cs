using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOgranismSetup : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.FoodRemaining > BoardSquare.MAX_FOOD)
            {
                byte foodBeforeCap = boardSquare.FoodRemaining;
                boardSquare.SetFoodToMax();
                SimulationDiagnostics.Current?.Count("food.cap_removed", foodBeforeCap - boardSquare.FoodRemaining);
            }

            if (boardSquare.IsOccupied)
            {
                lock (boardSquare)
                {
                    // Uptake already debited its actual source cell during resolution.
                    boardSquare.Occupant.GetAndResetTakenFood();
                    boardSquare.Occupant.SetAvailableFood(boardSquare.FoodRemaining);
                    boardSquare.Occupant.Activate();
                }
            }
        }
    }
}
