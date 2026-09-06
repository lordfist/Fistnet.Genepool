using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Rules
{
    public class ExecuteOrganismSequenceRule : IBoardSquareRule
    {
        public void Execute(BoardSquare boardSquare)
        {
            throw new InvalidOperationException("Action selection requires Board's retained season cohort; call Prepare and retain its decision.");
        }

        public static ActionDecision Prepare(BoardSquare boardSquare)
        {
            if (!boardSquare.IsOccupied) return null;
            Organism organism = boardSquare.Occupant;
            ActionDecision decision = organism.PrepareSeason(BuildCandidates(boardSquare));
            organism.Deactivate();
            return decision;
        }

        public static IReadOnlyList<ActionCandidate> BuildCandidates(BoardSquare boardSquare)
        {
            Organism actor = boardSquare.Occupant;
            if (actor == null) return Array.Empty<ActionCandidate>();
            var candidates = new List<ActionCandidate>(actor.DnaSequence.Count);
            foreach (var gene in actor.DnaSequence)
            {
                BoardSquare target = boardSquare.GetNeightbor(gene.Target);
                Organism affected = gene.Target == TargetTypes.Self ? actor : target?.Occupant;
                candidates.Add(new ActionCandidate(gene, affected, boardSquare.Position.X, boardSquare.Position.Y,
                    target?.Position.X ?? -1, target?.Position.Y ?? -1, target != null,
                    actor.IsActionEligible(gene, affected)));
            }
            return candidates;
        }
    }
}
