using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteRefreshRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            boardSquare.Refresh();
        }
    }
}