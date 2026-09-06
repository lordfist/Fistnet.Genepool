using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class MechanicsTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("mutation handles duplicate draws and protected positions", "mechanics", () =>
        {
            var organism = new Organism(); var before = organism.DnaSequence.ToArray();
            Common.ConfigureRandom(new ScriptedRandomSource(3, 1, 1, 3, 6, 6, 7, 7), true);
            Check.Invoke(organism, "MutateMe", 0, (byte)3);
            for (int i = 0; i < before.Length; i++)
                Check.Equal(i != 1 && i != 6, ReferenceEquals(before[i], organism.DnaSequence[i]), "replacement index " + i);
        });
        yield return new("two matching parents contribute an arithmetic mean", "mechanics", () =>
        {
            var first = new FixtureOrganism(); first.SetGenes((me, i) => new EatDnaElement(me, i));
            var second = new FixtureOrganism(); second.CopyGenes(first);
            var child = new FixtureOrganism(); child.CopyGenes(first);
            Check.Scores(first)[99] = new() { [0] = 2, [1] = 3 };
            Check.Scores(second)[99] = new() { [0] = 6 };
            child.Brain.LearnFromParent(first); child.Brain.LearnFromParent(second);
            Check.Equal(4f, Check.Scores(child)[99][0]);
            Check.Equal(3f, Check.Scores(child)[99][1]);
            Check.Equal(2f, Check.Scores(first)[99][0]);
            Common.ConfigureRandom(new ScriptedRandomSource(), true);
            var born = new Organism(first, second);
            Check.Equal(4f, Check.Scores(born)[99][0], "actual birth constructor history");
        });
        yield return new("parent transfer rejects mismatched genes and accepts no history", "mechanics", () =>
        {
            var parent = new FixtureOrganism(); parent.SetGenes((me, i) => new EatDnaElement(me, i));
            var child = new FixtureOrganism(); child.CopyGenes(parent);
            child.Brain.LearnFromParent(parent); child.Brain.LearnFromParent(null);
            Check.Equal(0, Check.Scores(child).Count);
            Check.Scores(parent)[77] = new() { [0] = 8, [1] = 4, [2] = 3, [255] = 2 };
            Check.Property(child.DnaSequence[0], "DnaCode", -123);
            Check.Property(child.DnaSequence[1], "DnaSequenceIndex", (byte)7);
            var replacement = new MoveDnaElement(child, 2);
            Check.Property(replacement, "DnaCode", parent.DnaSequence[2].DnaCode);
            child.DnaSequence[2] = replacement;
            child.Brain.LearnFromParent(parent);
            Check.Equal(0, Check.Scores(child).Count, "mismatched genes transferred");
        });
        yield return new("matching DNA code with a different target is not inherited", "mechanics", () =>
        {
            var parent = new FixtureOrganism(); parent.SetGenes((me, i) => new MoveDnaElement(me, i));
            var child = new FixtureOrganism(); child.CopyGenes(parent);
            Check.Scores(parent)[7] = new() { [0] = 9 };
            var target = parent.DnaSequence[0].Target == TargetTypes.Self ? TargetTypes.TopLeft : TargetTypes.Self;
            Check.Property(child.DnaSequence[0], "Target", target);
            child.Brain.LearnFromParent(parent);
            Check.Equal(0, Check.Scores(child).Count);
        });
        yield return new("learning scores remain finite when available food starts at zero", "mechanics", () =>
        {
            var organism = new Organism(); organism.SetAvailableFood(0);
            organism.Brain.ChooseOutput(null, out byte choice);
            organism.SetAvailableFood(3); organism.Brain.EvaluateResult(choice, null);
            Check.True(Check.Scores(organism).SelectMany(pair => pair.Value.Values).All(float.IsFinite), "non-finite learned score");
            Check.Equal(1.2f, Check.Scores(organism)[0][choice]);
            Check.True(float.IsFinite(Common.CalculateChange(int.MaxValue, int.MinValue)), "integer subtraction overflow");
            Check.True(float.IsFinite(Common.CalculateDifferenceFromValue(int.MinValue, int.MaxValue)), "absolute value overflow");
        });
        yield return new("reference effects execute in enqueue order", "mechanics", () =>
        {
            Common.ConfigureRandom(new SeededRandomSource(11), true);
            var organism = new Organism();
            organism.AddStackedEffect(new ChangeAgeEffect(organism, 2, 0));
            organism.AddStackedEffect(new ChangeAgeEffect(organism, -3, 0));
            Check.Equal(2, organism.PendingEffects.Count);
            organism.ExecuteEffectStack();
            Check.Equal(0, organism.Age, "noncommuting age effects were reversed");
            Check.Equal(0, organism.PendingEffects.Count);
        });
        yield return new("seeded random state and draw counts repeat", "mechanics", () =>
        {
            foreach (int seed in new[] { 0, 11, -29, 83 })
            {
                var first = new SeededRandomSource(seed); var second = new SeededRandomSource(seed);
                for (int i = 0; i < 100; i++) Check.Equal(first.Next(1000), second.Next(1000));
                Check.Equal(100L, first.DrawCount); Check.Equal(first.State, second.State);
            }
        });
    }
}
