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
                    byte foodBefore = boardSquare.FoodRemaining;
                    byte requestedDebit = boardSquare.Occupant.GetAndResetTakenFood();
                    boardSquare.ReduceFood(requestedDebit);
                    DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                    if (diagnostics != null)
                    {
                        int appliedDebit = foodBefore - boardSquare.FoodRemaining;
                        diagnostics.Count("food.debit_requested", requestedDebit);
                        diagnostics.Count("food.debit_applied", appliedDebit);
                        if (requestedDebit != appliedDebit)
                        {
                            diagnostics.Count("food.debit_shortfall", requestedDebit - appliedDebit);
                            diagnostics.Event("food.debit_shortfall", boardSquare.Occupant,
                                boardSquare.Position.X, boardSquare.Position.Y,
                                diagnostics.CanSampleEvents ? "requested=" + requestedDebit + ";applied=" + appliedDebit : null);
                        }
                    }
                    boardSquare.Occupant.SetAvailableFood(boardSquare.FoodRemaining);
                    boardSquare.Occupant.Activate();
                }
            }
        }
    }
}