using System.Reflection;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Dna.Elements.Brain;
using Fistnet.Genepool.Control.Gameboard;

namespace Fistnet.Genepool.Tests;

internal static class Check
{
    public static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Equal<T>(T expected, T actual, string message = "") =>
        True(EqualityComparer<T>.Default.Equals(expected, actual), $"{message} expected={expected}, actual={actual}");
    public static T Field<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    public static Dictionary<long, Dictionary<byte, float>> Scores(Organism organism) =>
        Field<Dictionary<long, Dictionary<byte, float>>>(organism.Brain, "mesh");
    public static void Property(object instance, string name, object value) =>
        instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(instance, value);
    public static void Invoke(object instance, string name, params object[] args) => instance.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    public static void EmptyCells()
    {
        for (int x = 0; x < Board.BOARD_SIZE; x++)
            for (int y = 0; y < Board.BOARD_SIZE; y++)
                Board.BoardElement[x, y] = new BoardSquare(x, y, null);
    }
}

internal sealed class ScriptedRandomSource : IRandomSource
{
    private readonly Queue<int> values;
    public long DrawCount { get; private set; }
    public string Algorithm => "scripted-fixture-v1";
    public string State => string.Join(",", values);
    public ScriptedRandomSource(params int[] values) { this.values = new Queue<int>(values); }
    public int Next(int max)
    {
        DrawCount++;
        int value = values.Count > 0 ? values.Dequeue() : 0;
        Check.True(value >= 0 && value < max, "scripted draw outside requested range");
        return value;
    }
}

internal sealed class FixtureOrganism : Organism
{
    public void SetGenes(Func<Organism, byte, IDnaElement> factory)
    {
        DnaSequence = Enumerable.Range(0, DNA_SEQUENCE_MAXLENGTH).Select(i => factory(this, (byte)i)).ToList();
        DnaCode = Common.CalculateOrganismDnaCode(DnaSequence);
    }
    public void CopyGenes(Organism source) { DnaSequence = source.DnaSequence.Select(g => g.CopyToChild(this)).ToList(); DnaCode = source.DnaCode; }
    public void SetState(sbyte health = 10, sbyte food = 5, int age = 0, int sequenceAge = 0)
    { Health = health; FoodBalance = food; Age = age; SequenceAge = sequenceAge; }
}
