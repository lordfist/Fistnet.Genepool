using System.Drawing;
using System.Reflection;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

internal static class RegressionTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("mutation changes at most two distinct unprotected positions", "regression", () =>
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var organism = new Organism();
                var before = organism.DnaSequence.ToArray();
                typeof(Organism).GetMethod("MutateMe", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(organism, new object[] { seed, (byte)3 });
                int changed = before.Where((gene, i) => !ReferenceEquals(gene, organism.DnaSequence[i])).Count();
                Check.True(changed <= 2, $"seed {seed} replaced {changed} positions");
                Check.True(ReferenceEquals(before[3], organism.DnaSequence[3]), "protected gene replaced");
            }
        });
        yield return new("parent history copies matching gene scores independently", "regression", () =>
        {
            var parent = new FixtureOrganism();
            var child = new FixtureOrganism(); child.CopyGenes(parent);
            Check.Scores(parent)["E:123"] = new() { [0] = 6 };
            child.Brain.LearnFromParent(parent);
            Check.True(Check.Scores(child).TryGetValue("E:123", out var values) && values.TryGetValue(0, out var value) && value == 6,
                "matching parent score was not inherited");
            values[0] = 99;
            Check.Equal(6f, Check.Scores(parent)["E:123"][0], "parent dictionary was shared");
        });
        yield return new("zero-baseline learning arithmetic stays finite", "regression", () =>
        {
            Check.Equal(0f, Common.CalculateChange(0, 0));
            Check.Equal(3f, Common.CalculateChange(3, 0));
            Check.Equal(.5f, Common.CalculateChange(3, 2));
        });
        yield return new("completed occupied choice cannot leak into a later empty choice", "regression", () =>
        {
            var organism = new Organism();
            var target = new Organism();
            var occupied = organism.Brain.Choose(new[] { new ActionCandidate(organism.DnaSequence[0], target) }, Common.Policy);
            organism.Brain.Complete(occupied, new ActionOutcome { ActorPresent = true, ActorAfter = organism.CreateSnapshot() });
            var empty = organism.Brain.Choose(new[] { new ActionCandidate(organism.DnaSequence[0], null) }, Common.Policy);
            Check.Equal("Empty", empty.Candidate.Context);
            Check.True(empty.Candidate.TargetBefore == null && empty.Candidate.Target == null, "stale occupied target retained");
            organism.Brain.Complete(empty, new ActionOutcome { ActorPresent = true, ActorAfter = organism.CreateSnapshot() });
        });
        yield return new("origin pixel selects origin cell", "regression", () =>
        {
            Check.EmptyCells();
            using var renderer = new GameboardBitmap(600);
            Check.True(ReferenceEquals(Board.BoardElement[0, 0], renderer.GetSquareFromLocation(Point.Empty)), "origin mapping wrong");
        });
        yield return new("maximum food increase saturates without byte overflow", "regression", () =>
        {
            var square = new BoardSquare(0, 0, null); square.IncreaseFood(byte.MaxValue);
            Check.Equal(BoardSquare.MAX_FOOD, square.FoodRemaining);
        });
    }
}
