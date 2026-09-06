using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismEffectsRule : IBoardSquareRule
    {
        private static readonly string[] birthDirectionCounters =
            { "birth.placed.TopLeft", "birth.placed.TopCenter", "birth.placed.TopRight",
              "birth.placed.MiddleLeft", "birth.placed.Self", "birth.placed.MiddleRight",
              "birth.placed.BottomLeft", "birth.placed.BottomCenter", "birth.placed.BottomRight" };

        public void Execute(BoardSquare boardSquare)
        {
            if (boardSquare.IsOccupied)
            {
                lock (Board.BoardElement)
                {
                    boardSquare.Occupant.ExecuteEffectStack();
                    if (boardSquare.Occupant.HasChild)
                    {
                        DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                        diagnostics?.Count("birth.placement_attempted");
                        BoardSquare freeSquare = boardSquare.GetFirstEmptySquare();
                        if (freeSquare != null && !freeSquare.IsOccupied)
                        {
                            Organism placedChild = boardSquare.Occupant.Child;
                            freeSquare.AddOccupant(boardSquare.Occupant.Child);
                            boardSquare.Occupant.ReleaseChild();
                            if (diagnostics != null)
                            {
                                diagnostics.Count("birth.placed");
                                int dx = freeSquare.Position.X - boardSquare.Position.X;
                                int dy = freeSquare.Position.Y - boardSquare.Position.Y;
                                int direction = (dy + 1) * 3 + dx + 1;
                                if (direction >= 0 && direction < birthDirectionCounters.Length)
                                    diagnostics.Count(birthDirectionCounters[direction]);
                                diagnostics.Event("birth.placed", placedChild, freeSquare.Position.X, freeSquare.Position.Y,
                                    diagnostics.CanSampleEvents ? "parentId=" + diagnostics.OrganismId(boardSquare.Occupant)
                                    + ";parentCell=" + boardSquare.Position.X + "," + boardSquare.Position.Y
                                    + ";parentDnaCode=" + boardSquare.Occupant.DnaCode : null);
                            }
                        }
                        else diagnostics?.Count("birth.placement_blocked");
                    }
                }
            }
        }
    }
}