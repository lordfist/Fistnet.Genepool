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
                    remainingFood = (byte)Math.Min(BoardSquare.MAX_FOOD, boardSquare.Occupant.FoodBalance);

                lock (boardSquare)
                {
                    Organism removedOrganism = boardSquare.Occupant;
                    byte foodBefore = boardSquare.FoodRemaining;
                    boardSquare.IncreaseFood(remainingFood);
                    removedOrganism.MoveDone();
                    boardSquare.RemoveOccupant();
                    DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                    if (diagnostics != null)
                    {
                        diagnostics.Count("food.death_return_requested", Math.Max(0, removedOrganism.FoodBalance));
                        diagnostics.Count("food.death_return_applied", boardSquare.FoodRemaining - foodBefore);
                        diagnostics.Count("food.death_return_capped", Math.Max(0, removedOrganism.FoodBalance)
                            - (boardSquare.FoodRemaining - foodBefore));
                        diagnostics.RecordDeath(removedOrganism, boardSquare.Position.X, boardSquare.Position.Y);
                    }
                }
            }
        }
    }
}
