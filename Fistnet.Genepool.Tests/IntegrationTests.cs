using System.Drawing;
using System.Reflection;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control.Rules;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class IntegrationTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("neighbors: interior, edges and corners do not wrap", "integration", Neighbors);
        yield return new("food: setup debits taken food exactly once", "integration", FoodSetup);
        yield return new("food: replenishment on eighth refresh and bounded arithmetic", "integration", FoodRefresh);
        yield return new("lifecycle: removal pass removes dead occupant and returns food", "integration", DeadRemoval);
        yield return new("movement: identity, occupied destination and boundary fallback", "integration", Movement);
        yield return new("movement: competing destinations retain unique occupancy", "integration", CompetingMoves);
        yield return new("birth: placement, occupancy and deferred placement cost", "integration", BirthPlacement);
        yield return new("birth: crowded board retains pending child", "integration", PendingChild);
        yield return new("statistics: final occupancy and action counts after movement", "integration", CompletedStatistics);
        yield return new("reset: dirty world, rule cursor, statistics and RNG are cleared", "integration", DirtyReset);
        yield return new("reference: reset repeats execution state and random draws", "integration", Repeatability);
        yield return new("snapshot: private counters, cell clocks, scores and effect order", "integration", SnapshotCoverage);
        yield return new("snapshot: production mode is rejected", "integration", RejectProductionSnapshot);
        yield return new("characterization: organism age advances on ninth action tick", "integration", AgeCharacterization);
        yield return new("characterization: one birth increments reproduction counter twice", "integration", ReproductionCharacterization);
        yield return new("learning: aging tick never reevaluates a previous occupied target", "integration", AgingLearning);
    }

    private static void Reset(int seed = 11, Func<int, int, Organism> factory = null) =>
        Board.Reset(new SimulationRunOptions
        { Mode = SimulationMode.DeterministicReference, Seed = seed, InitialPopulationPercent = 0 },
            factory ?? ((_, _) => null));

    private static void Neighbors()
    {
        Reset();
        (int dx, int dy)[] offsets = { (-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };
        foreach (int x in new[] { 0, 1, 50, 98, 99 })
            foreach (int y in new[] { 0, 1, 50, 98, 99 })
                for (int target = 0; target < offsets.Length; target++)
                {
                    int nx = x + offsets[target].dx, ny = y + offsets[target].dy;
                    BoardSquare expected = nx < 0 || nx >= 100 || ny < 0 || ny >= 100 ? null : Board.BoardElement[nx, ny];
                    Check.True(ReferenceEquals(expected, Board.BoardElement[x, y].GetNeightbor((TargetTypes)target)),
                        $"Wrong neighbor at ({x},{y}), target {target}");
                }
        Check.True(!Board.BoardElement[0, 0].IsOccupied, "Empty fixture unexpectedly occupied");
        Board.BoardElement[0, 0].AddOccupant(new Organism());
        Check.True(Board.BoardElement[0, 0].IsOccupied, "Placed occupant not visible");
    }

    private static void FoodSetup()
    {
        Reset();
        var organism = new Organism();
        var square = Board.BoardElement[5, 5];
        square.AddOccupant(organism);
        organism.SetAvailableFood(3);
        organism.AddStackedEffect(new ChangeFoodEffect(organism, 2, 0));
        organism.ExecuteEffectStack();
        Check.Equal((byte)2, organism.TakenFood);
        var setup = new ExecuteOgranismSetup();
        setup.Execute(square);
        Check.Equal((byte)1, square.FoodRemaining);
        Check.Equal((byte)1, organism.AvailableFood);
        Check.Equal((byte)0, organism.TakenFood);
        setup.Execute(square);
        Check.Equal((byte)1, square.FoodRemaining, "Taken food was debited twice");
    }

    private static void FoodRefresh()
    {
        var square = new BoardSquare(5, 5, null);
        square.ReduceFood(255);
        Check.Equal((byte)0, square.FoodRemaining);
        for (int i = 0; i < 7; i++) square.Refresh();
        Check.Equal((byte)0, square.FoodRemaining);
        Check.True(!square.IsNextAge, "Food age arrived early");
        square.Refresh();
        Check.Equal((byte)1, square.FoodRemaining);
        Check.True(square.IsNextAge, "Eighth refresh did not mark age");
        square.Refresh();
        Check.True(!square.IsNextAge, "Age flag was not cleared");
        square.SetFoodToMax();
        square.IncreaseFood(255);
        Check.Equal(BoardSquare.MAX_FOOD, square.FoodRemaining, "Replenishment must saturate before byte conversion");
        square.ReduceFood(BoardSquare.MAX_FOOD);
        Check.Equal((byte)0, square.FoodRemaining);
    }

    private static void DeadRemoval()
    {
        Reset();
        var organism = new FixtureOrganism();
        organism.SetState(health: 0, food: 5);
        var square = Board.BoardElement[5, 5];
        square.AddOccupant(organism);
        Check.True(square.IsOccupied && organism.IsDead, "Death must await removal pass");
        new ExecuteOrganismDeadRule().Execute(square);
        Check.True(!square.IsOccupied, "Removal pass retained dead organism");
        Check.Equal((byte)8, square.FoodRemaining);
        new ExecuteOrganismDeadRule().Execute(square);
        Check.Equal((byte)8, square.FoodRemaining, "Dead food returned more than once");
    }

    private static void RequestMove(Organism organism, TargetTypes target)
    {
        organism.AddStackedEffect(new ChangePositionEffect(organism, target, 0));
        organism.ExecuteEffectStack();
    }

    private static void Movement()
    {
        Reset();
        var moving = new Organism();
        var source = Board.BoardElement[5, 5];
        var destination = Board.BoardElement[6, 5];
        source.AddOccupant(moving);
        RequestMove(moving, TargetTypes.MiddleRight);
        Check.True(source.TryMove(TargetTypes.MiddleRight), "Empty destination rejected");
        Check.True(!source.IsOccupied && ReferenceEquals(destination.Occupant, moving), "Move changed identity or retained origin");
        Check.True(!moving.NextRequestedPosition.HasValue, "Successful move retained request");
        var blocker = new Organism();
        source.AddOccupant(blocker);
        Check.True(!destination.TryMove(TargetTypes.MiddleLeft), "Occupied destination accepted");
        Check.True(ReferenceEquals(source.Occupant, blocker) && ReferenceEquals(destination.Occupant, moving), "Blocked move changed occupants");
        var edge = Board.BoardElement[0, 0];
        var edgeOrganism = new Organism();
        edge.AddOccupant(edgeOrganism);
        RequestMove(edgeOrganism, TargetTypes.TopLeft);
        Check.True(edge.TryMove(TargetTypes.TopLeft), "Existing opposite-neighbor fallback failed");
        Check.True(ReferenceEquals(Board.BoardElement[1, 1].Occupant, edgeOrganism), "Boundary fallback moved to wrong cell");
        Check.True(!Board.BoardElement[99, 99].IsOccupied, "Boundary move wrapped");
    }

    private static void CompetingMoves()
    {
        Reset();
        var left = new Organism();
        var right = new Organism();
        Board.BoardElement[49, 50].AddOccupant(left);
        Board.BoardElement[51, 50].AddOccupant(right);
        Check.True(Board.BoardElement[49, 50].TryMove(TargetTypes.MiddleRight), "First contender failed");
        Check.True(!Board.BoardElement[51, 50].TryMove(TargetTypes.MiddleLeft), "Second contender overwrote destination");
        Check.True(ReferenceEquals(Board.BoardElement[50, 50].Occupant, left), "Wrong winner");
        Check.True(ReferenceEquals(Board.BoardElement[51, 50].Occupant, right), "Losing contender disappeared");
        var occupants = Board.BoardElement.Cast<BoardSquare>().Where(s => s.IsOccupied).Select(s => s.Occupant).ToArray();
        Check.Equal(2, occupants.Length);
        Check.Equal(2, occupants.Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    private static Organism CrowdedParent(bool leaveGap)
    {
        Reset();
        for (int x = 9; x <= 11; x++)
            for (int y = 9; y <= 11; y++)
                if (!leaveGap || x != 9 || y != 10) Board.BoardElement[x, y].AddOccupant(new Organism());
        Organism parent = Board.BoardElement[10, 10].Occupant;
        parent.AddStackedEffect(new BirthEffect(parent, Board.BoardElement[11, 11].Occupant, 0));
        parent.ExecuteEffectStack();
        Check.True(parent.HasChild, "Constructed parents did not produce pending child");
        return parent;
    }

    private static void BirthPlacement()
    {
        Organism parent = CrowdedParent(true);
        Organism child = parent.Child;
        new ExecuteOrganismEffectsRule().Execute(Board.BoardElement[10, 10]);
        Check.True(ReferenceEquals(Board.BoardElement[9, 10].Occupant, child), "Child did not occupy only empty neighbor");
        Check.True(!parent.HasChild && parent.Child == null, "Placed child retained by parent");
        Check.Equal((sbyte)5, parent.FoodBalance, "Placement cost applied before following effects pass");
        Check.Equal(1, parent.PendingEffects.Count);
        Check.Equal(EffectTypes.FoodChange, parent.PendingEffects[0].Effect);
        Check.Equal((sbyte)-2, (sbyte)parent.PendingEffects[0].Value);
        parent.ExecuteEffectStack();
        Check.Equal((sbyte)3, parent.FoodBalance);
        Check.Equal(0, parent.PendingEffects.Count);
    }

    private static void PendingChild()
    {
        Organism parent = CrowdedParent(false);
        Organism child = parent.Child;
        new ExecuteOrganismEffectsRule().Execute(Board.BoardElement[10, 10]);
        Check.True(ReferenceEquals(parent.Child, child), "Crowding discarded the pending child");
        Check.Equal(0, parent.PendingEffects.Count, "Failed placement charged food");
        Check.Equal(9, Board.BoardElement.Cast<BoardSquare>().Count(s => s.IsOccupied));
        Board.BoardElement[9, 9].RemoveOccupant();
        new ExecuteOrganismEffectsRule().Execute(Board.BoardElement[10, 10]);
        Check.True(ReferenceEquals(Board.BoardElement[9, 9].Occupant, child), "Pending child was not placed when room became available");
    }

    private static FixtureOrganism Mover(TargetTypes target, int sequenceAge)
    {
        var organism = new FixtureOrganism();
        organism.SetGenes((owner, index) =>
        {
            var gene = new MoveDnaElement(owner, index);
            SetField(gene, "<Target>k__BackingField", target);
            return gene;
        });
        organism.SetState(age: 3, sequenceAge: sequenceAge);
        return organism;
    }

    private static void CompletedStatistics()
    {
        Reset();
        Board.BoardElement[10, 10].AddOccupant(Mover(TargetTypes.MiddleLeft, 27));
        Board.BoardElement[20, 20].AddOccupant(Mover(TargetTypes.MiddleRight, 12));
        var eater = new FixtureOrganism();
        eater.SetGenes((owner, index) => new EatDnaElement(owner, index));
        eater.SetState(age: 2, sequenceAge: 8);
        Board.BoardElement[30, 30].AddOccupant(eater);
        int callbackSeason = -1, callbackPopulation = -1;
        void Observe(int season, int population) { callbackSeason = season; callbackPopulation = population; }
        Board.SeasonCompleted += Observe;
        try { Board.ExecuteSingleSeason(true); }
        finally { Board.SeasonCompleted -= Observe; }
        Check.True(Board.BoardElement[9, 10].IsOccupied && Board.BoardElement[21, 20].IsOccupied, "Scripted movers missed expected destinations");
        var occupants = Board.BoardElement.Cast<BoardSquare>().Where(s => s.IsOccupied).Select(s => s.Occupant).ToArray();
        Check.Equal(3, occupants.Length);
        Check.Equal(3, occupants.Distinct(ReferenceEqualityComparer.Instance).Count());
        Check.Equal(3, Board.BoardOrganismCount);
        Check.Equal(27, Board.LongestLiving, "Statistic must describe SequenceAge, not individual age");
        Check.Equal(16, Board.DnaUsageStatistics[DnaTypes.Move]);
        Check.Equal(8, Board.DnaUsageStatistics[DnaTypes.Eat]);
        Check.Equal(2, Board.OrganismUsageStatistics["1,1,1,1,1,1,1,1,"]);
        Check.Equal(1, Board.OrganismUsageStatistics["0,0,0,0,0,0,0,0,"]);
        Check.Equal(1, callbackSeason);
        Check.Equal(3, callbackPopulation);
    }

    private static void DirtyReset()
    {
        Reset();
        var organism = new Organism();
        var previousBoard = Board.BoardElement;
        Board.BoardElement[5, 5].AddOccupant(organism);
        Board.ExecuteSingleSeason(true);
        organism.AddStackedEffect(new ChangeFoodEffect(organism, -2, 0));
        Check.Scores(organism)[123] = new Dictionary<byte, float> { [2] = 1.25f };
        RuleManager.MoveToNextRule();
        Common.GetRandomIntegerSeed();
        Reset();
        Check.True(!ReferenceEquals(previousBoard, Board.BoardElement), "Reset retained previous board");
        Check.Equal(0, Board.Season);
        Check.Equal(0, RuleManager.CurrentRuleIndex);
        Check.Equal(0, Board.BoardOrganismCount);
        Check.Equal(0, Board.LongestLiving);
        Check.Equal(0, Board.DnaUsageStatistics.Count);
        Check.Equal(0, Board.OrganismUsageStatistics.Count);
        Check.Equal(0L, Common.RandomSource.DrawCount);
        Check.Equal(new SeededRandomSource(11).State, Common.RandomSource.State);
        foreach (BoardSquare square in Board.BoardElement)
        {
            Check.True(!square.IsOccupied, "Reset retained an occupant or its pending state");
            Check.Equal(BoardSquare.STARTING_FOOD, square.FoodRemaining);
            Check.Equal(0, Check.Field<int>(square, "_currentSeason"));
        }
    }

    private static void Repeatability()
    {
        string Run()
        {
            Reset(29, (x, y) => x == 4 && y == 4 ? new Organism() : null);
            for (int i = 0; i < 5; i++) Board.ExecuteSingleSeason(true);
            return SimulationSnapshot.Capture().Json;
        }
        string first = Run();
        long firstDraws = Common.RandomSource.DrawCount;
        Common.GetRandomIntegerSeed();
        RuleManager.MoveToNextRule();
        Check.True(first == Run(), "Reset reference trajectory differs");
        Check.Equal(firstDraws, Common.RandomSource.DrawCount);
    }

    private static void SnapshotCoverage()
    {
        Reset(47, (x, y) => x == 4 && y == 4 ? new Organism() : null);
        Organism organism = Board.BoardElement[4, 4].Occupant;
        organism.DnaSequence[0] = new MoveDnaElement(organism, 0);
        SetField(organism, "<DnaCode>k__BackingField", Common.CalculateOrganismDnaCode(organism.DnaSequence));
        SimulationSnapshot baseline = SimulationSnapshot.Capture();
        long draws = Common.RandomSource.DrawCount;
        SetField(organism, "currentSequenceIndex", (byte)3);
        SetField(organism.DnaSequence[0], "_executionNumber", 9);
        SetField(Board.BoardElement[4, 4], "_currentSeason", 4);
        Check.Scores(organism)[123] = new Dictionary<byte, float> { [2] = 1.25f };
        organism.AddStackedEffect(new ChangeFoodEffect(organism, -2, 0));
        organism.AddStackedEffect(new ChangeAgeEffect(organism, 4, 0));
        SimulationSnapshot changed = SimulationSnapshot.Capture();
        Check.True(baseline.Sha256 != changed.Sha256, "Private state did not affect snapshot");
        Check.Equal(draws, Common.RandomSource.DrawCount, "Snapshot consumed randomness");
        using var before = JsonDocument.Parse(baseline.Json);
        using var after = JsonDocument.Parse(changed.Json);
        var nodes = after.RootElement.GetProperty("objects").EnumerateArray().ToArray();
        Check.True(HasNumericField(nodes, ".currentSequenceIndex", 3), "Missing organism private counter");
        Check.True(HasNumericField(nodes, "._executionNumber", 9), "Missing gene execution counter");
        Check.True(HasNumericField(nodes, "._currentSeason", 4), "Missing cell clock");
        Check.True(!HasNumericField(before.RootElement.GetProperty("objects").EnumerateArray(), ".currentSequenceIndex", 3), "Baseline snapshot was not detached");
        Check.True(nodes.Any(node => node.TryGetProperty("entries", out var entries) && entries.EnumerateArray().Any(entry =>
            entry.GetProperty("key").ValueKind == JsonValueKind.Number && entry.GetProperty("key").GetInt32() == 2 &&
            entry.GetProperty("value").ValueKind == JsonValueKind.Number && entry.GetProperty("value").GetDouble() == 1.25)), "Missing learned score");
        var organismNode = nodes.Single(node => node.GetProperty("type").GetString() == typeof(Organism).FullName);
        var pending = organismNode.GetProperty("fields").GetProperty("PendingEffects").EnumerateArray().ToArray();
        Check.Equal(2, pending.Length);
        string EffectType(JsonElement reference) => nodes.Single(node => node.GetProperty("id").GetInt32() == reference.GetProperty("reference").GetInt32()).GetProperty("type").GetString();
        Check.Equal(typeof(ChangeFoodEffect).FullName, EffectType(pending[0]));
        Check.Equal(typeof(ChangeAgeEffect).FullName, EffectType(pending[1]));
    }

    private static bool HasNumericField(IEnumerable<JsonElement> nodes, string suffix, int expected) =>
        nodes.Any(node => node.TryGetProperty("fields", out var fields) && fields.EnumerateObject().Any(field =>
            field.Name.EndsWith(suffix, StringComparison.Ordinal) && field.Value.ValueKind == JsonValueKind.Number && field.Value.GetInt32() == expected));

    private static void RejectProductionSnapshot()
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.Production, Seed = 11, InitialPopulationPercent = 0 }, (_, _) => null);
        bool rejected = false;
        try { SimulationSnapshot.Capture(); }
        catch (InvalidOperationException) { rejected = true; }
        Check.True(rejected, "Production trajectory was presented as full reference state");
    }

    private static void AgeCharacterization()
    {
        Reset();
        var organism = new FixtureOrganism();
        organism.SetGenes((owner, index) => new EatDnaElement(owner, index));
        organism.Activate();
        for (int i = 0; i < 8; i++) organism.ExecuteNextDnaSequence(null);
        Check.Equal(0, organism.Age);
        organism.ExecuteNextDnaSequence(null);
        Check.Equal(1, organism.Age);
        Check.Equal(1, organism.SequenceAge);
        Check.Equal(1, organism.PendingEffects.Count);
        Check.Equal((sbyte)-1, (sbyte)organism.PendingEffects[0].Value);
    }

    private static void ReproductionCharacterization()
    {
        Reset();
        var parent = new Organism();
        parent.AddStackedEffect(new BirthEffect(parent, new Organism(), 0));
        parent.ExecuteEffectStack();
        Check.True(parent.HasChild, "Birth fixture failed");
        Check.Equal((byte)2, Check.Field<byte>(parent, "_reproductionsInAge"));
    }

    private static void AgingLearning()
    {
        Reset();
        var organism = new FixtureOrganism();
        organism.SetGenes((owner, index) => new EatDnaElement(owner, index));
        var target = new Organism();
        organism.Activate();
        for (int i = 0; i < 8; i++)
        {
            organism.GetNextDnaSequenceTarget();
            organism.ExecuteNextDnaSequence(target);
            organism.ExecuteEffectStack();
        }
        string before = JsonSerializer.Serialize(Check.Scores(organism));
        organism.GetNextDnaSequenceTarget();
        organism.ExecuteNextDnaSequence(target);
        organism.ExecuteEffectStack();
        Check.Equal(1, organism.Age);
        Check.Equal(before, JsonSerializer.Serialize(Check.Scores(organism)), "An aging tick has no action to reward");
    }

    private static void SetField(object instance, string name, object value)
    {
        for (Type type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(instance, value); return; }
        }
        throw new InvalidOperationException("Missing fixture field: " + name);
    }
}
