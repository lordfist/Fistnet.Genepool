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
                    Organism removedOrganism = boardSquare.Occupant;
                    byte foodBefore = boardSquare.FoodRemaining;
                    boardSquare.IncreaseFood(remainingFood);
                    boardSquare.RemoveOccupant();
                    DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                    if (diagnostics != null)
                    {
                        diagnostics.Count("food.death_return_requested", remainingFood);
                        diagnostics.Count("food.death_return_applied", boardSquare.FoodRemaining - foodBefore);
                        diagnostics.Count("food.pending_debit_at_death", removedOrganism.TakenFood);
                        diagnostics.RecordDeath(removedOrganism, boardSquare.Position.X, boardSquare.Position.Y);
                    }
                }
            }
        }
    }
}