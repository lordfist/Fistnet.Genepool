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
        public void Execute(BoardSquare boardSquare)
        {
            throw new InvalidOperationException("Shared action transactions require Board's retained decisions and source-cell callbacks.");
        }

        public static void Resolve(Organism actor, ActionDecision decision,
            Func<Organism, int, int> gather, Func<Organism, Organism, bool> birth)
        {
            actor.ResolveDecision(decision, gather, birth);
        }
    }
}
