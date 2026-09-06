using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control.Rules;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class ActionTransactionTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("actions: each candidate keeps its own self, neighbor and border target", "action-transactions", CandidateTargets);
        yield return new("actions: self consumption never affects the neighbor", "action-transactions", SelfIsolation);
        yield return new("actions: rejected duplicate and foreign decisions cannot drain queued effects", "action-transactions", RejectedDecisionPreservesEffects);
        yield return new("food: repeated uptake is bounded, cumulative and already debited", "action-transactions", FoodTransactions);
        yield return new("food: pending effects use the actual source-cell transaction", "action-transactions", PendingFoodUsesSourceCell);
        yield return new("food: competing gatherers debit the recipient cell once", "action-transactions", CompetingGatherers);
        yield return new("food: gathering before movement never charges the destination", "action-transactions", GatherThenMove);
        yield return new("food: death returns only remaining reserves without deferred debt", "action-transactions", GatherThenDeath);
        yield return new("birth: uniform available-neighbor selection excludes self and borders", "action-transactions", UniformAvailableNeighbors);
        yield return new("birth: successful placements charge and increment once", "action-transactions", BirthAccounting);
        yield return new("birth: blocked placement consumes no fertility, reserves or construction draws", "action-transactions", BlockedBirth);
        yield return new("birth: newborns first act in the next season", "action-transactions", NewbornBoundary);
        yield return new("movement: blocked requests expire and cannot execute in a later turn", "action-transactions", BlockedMoveExpires);
        yield return new("movement: boundary fallback records the actual destination", "action-transactions", MovementFallback);
        yield return new("movement: entering an original actor's vacated cell never moves twice", "action-transactions", VacatedCellMovesOnce);
        yield return new("outcomes: deaths and movement finish before retained actor evaluation", "action-transactions", CompletedOutcomes);
        yield return new("reference: repaired action resolution repeats with the same seed and policy", "action-transactions", ReproducibleResolution);
    }

    private static void Reset(int seed = 71) => Board.Reset(new SimulationRunOptions
    { Mode = SimulationMode.DeterministicReference, Seed = seed, InitialPopulationPercent = 0 }, (_, _) => null);

    private static FixtureOrganism Actor(DnaTypes type, TargetTypes target = TargetTypes.Self, sbyte food = 5, int age = 1)
    {
        var actor = new FixtureOrganism();
        actor.SetState(food: food, age: age);
        actor.SetGenes((me, slot) =>
        {
            IDnaElement gene = type switch
            {
                DnaTypes.Eat => new EatDnaElement(me, slot),
                DnaTypes.Move => new MoveDnaElement(me, slot),
                DnaTypes.GenerateFood => new CreateFoodDnaElement(me, slot),
                DnaTypes.CombineDna => new CombineDnaElement(me, slot),
                DnaTypes.Kill => new KillDnaElement(me, slot),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            if (type != DnaTypes.Eat) Check.Property(gene, "Target", target);
            return gene;
        });
        return actor;
    }

    private static void CandidateTargets()
    {
        Reset();
        var actor = Actor(DnaTypes.Move);
        actor.DnaSequence[0] = new EatDnaElement(actor, 0);
        Check.Property(actor.DnaSequence[1], "Target", TargetTypes.TopLeft);
        var gather = new CreateFoodDnaElement(actor, 2);
        Check.Property(gather, "Target", TargetTypes.MiddleRight);
        actor.DnaSequence[2] = gather;
        var neighbor = Actor(DnaTypes.Move);
        var square = Board.BoardElement[0, 0];
        square.AddOccupant(actor); Board.BoardElement[1, 0].AddOccupant(neighbor);
        var candidates = ExecuteOrganismSequenceRule.BuildCandidates(square);
        Check.Equal(8, candidates.Count);
        Check.True(ReferenceEquals(actor, candidates[0].Target), "Self was resolved to a neighbor");
        Check.Equal("Self", candidates[0].Context);
        Check.True(!candidates[1].InBounds && candidates[1].Target == null, "Border was silently wrapped");
        Check.Equal("OutOfBounds", candidates[1].Context);
        Check.True(ReferenceEquals(neighbor, candidates[2].Target), "Gather received another gene's target");
        Check.Equal(1, candidates[2].TargetX); Check.Equal(0, candidates[2].TargetY);
    }

    private static void SelfIsolation()
    {
        Reset();
        var actor = Actor(DnaTypes.Eat); var neighbor = Actor(DnaTypes.Move);
        Board.BoardElement[10, 10].AddOccupant(actor); Board.BoardElement[11, 10].AddOccupant(neighbor);
        Board.ExecuteSingleSeason(true);
        Check.Equal(11, actor.Health); Check.Equal(4, actor.FoodBalance);
        Check.Equal(10, neighbor.Health); Check.Equal(5, neighbor.FoodBalance);
        Check.Equal(DnaTypes.Eat, actor.LastAction.Type);
        Check.Equal(1, actor.LastOutcome.FoodSpent);
        Check.True(actor.LastOutcome.ActorPresent && actor.LastOutcome.TargetPresent, "Completed self outcome missing");
    }

    private static void RejectedDecisionPreservesEffects()
    {
        Reset();
        var actor = Actor(DnaTypes.Eat); var foreignActor = Actor(DnaTypes.Eat);
        var square = Board.BoardElement[10, 10]; var foreignSquare = Board.BoardElement[12, 10];
        square.AddOccupant(actor); foreignSquare.AddOccupant(foreignActor);
        actor.Activate(); foreignActor.Activate();
        var decision = actor.PrepareSeason(ExecuteOrganismSequenceRule.BuildCandidates(square));
        var foreign = foreignActor.PrepareSeason(ExecuteOrganismSequenceRule.BuildCandidates(foreignSquare));
        Check.True(decision != null && foreign != null, "Eligible fixture did not select decisions");
        actor.ResolveDecision(decision, null, null);
        Check.Equal(11, actor.Health); Check.Equal(4, actor.FoodBalance);
        actor.AddStackedEffect(new ChangeHealthEffect(actor, 2, 0));
        int callbackCalls = 0;
        foreach (ActionDecision rejected in new[] { decision, foreign })
        {
            bool threw = false;
            try { actor.ResolveDecision(rejected, (_, _) => { callbackCalls++; return 0; }, (_, _) => false); }
            catch (InvalidOperationException) { threw = true; }
            Check.True(threw, "Duplicate or foreign decision was accepted");
            Check.Equal(11, actor.Health, "Rejected call drained its queued health effect");
            Check.Equal(4, actor.FoodBalance, "Rejected call charged reserves");
            Check.Equal(1, actor.PendingEffects.Count, "Rejected call changed the pending queue");
            Check.Equal(0, callbackCalls, "Rejected call reached a shared-world callback");
        }
        actor.ExecuteEffectStack();
        Check.Equal(13, actor.Health, "Rejected calls lost or duplicated the pending effect");
        actor.FinishSeason(decision, true, true);
        foreignActor.ResolveDecision(foreign, null, null); foreignActor.FinishSeason(foreign, true, true);
    }

    private static void PendingFoodUsesSourceCell()
    {
        Reset();
        var actor = Actor(DnaTypes.Move); var square = Board.BoardElement[10, 10]; square.AddOccupant(actor);
        actor.SetAvailableFood(square.FoodRemaining);
        actor.AddStackedEffect(new ChangeFoodEffect(actor, 2, 0));
        actor.AddStackedEffect(new ChangeFoodEffect(actor, 2, 1));
        int callbackCalls = 0;
        actor.ResolveDecision(null, (recipient, requested) =>
        {
            callbackCalls++;
            Check.True(ReferenceEquals(actor, recipient), "Pending transfer changed recipient identity");
            return square.GatherFood(recipient, requested);
        }, (_, _) => false);
        Check.Equal(2, callbackCalls, "Pending effects bypassed the source-cell callback");
        Check.Equal(8, actor.FoodBalance); Check.Equal(3, actor.TakenFood);
        Check.Equal((byte)0, square.FoodRemaining); Check.Equal(0, actor.AvailableFood);
        Check.Equal(0, actor.PendingEffects.Count);
        new ExecuteOgranismSetup().Execute(square);
        Check.Equal((byte)0, square.FoodRemaining, "Already committed uptake was debited again");
    }

    private static void FoodTransactions()
    {
        Reset();
        var actor = Actor(DnaTypes.Move, food: 7);
        var square = Board.BoardElement[10, 10]; square.AddOccupant(actor);
        Check.Equal(2, square.GatherFood(actor, 2));
        Check.Equal(1, square.GatherFood(actor, 3));
        Check.Equal(0, square.GatherFood(actor, 1));
        Check.Equal(10, actor.FoodBalance); Check.Equal(3, actor.TakenFood);
        Check.Equal((byte)0, square.FoodRemaining); Check.Equal(0, actor.AvailableFood);
        new ExecuteOgranismSetup().Execute(square);
        Check.Equal(0, actor.TakenFood); Check.Equal((byte)0, square.FoodRemaining);
        Check.Equal(0, square.GatherFood(Actor(DnaTypes.Move), 2), "Nonoccupant withdrew food");
        square.SetFoodToMax();
        Check.Equal(0, square.GatherFood(actor, 3), "Carried capacity was exceeded");
        Check.Equal(BoardSquare.MAX_FOOD, square.FoodRemaining);
    }

    private static void CompetingGatherers()
    {
        Reset();
        var left = Actor(DnaTypes.GenerateFood, TargetTypes.MiddleRight);
        var right = Actor(DnaTypes.GenerateFood, TargetTypes.MiddleLeft);
        var recipient = Actor(DnaTypes.Move);
        Board.BoardElement[10, 10].AddOccupant(left); Board.BoardElement[12, 10].AddOccupant(right);
        Board.BoardElement[11, 10].AddOccupant(recipient);
        Board.ExecuteSingleSeason(true);
        Check.Equal(8, recipient.FoodBalance); Check.Equal(3, recipient.TakenFood);
        Check.Equal((byte)0, Board.BoardElement[11, 10].FoodRemaining);
        Check.Equal((byte)3, Board.BoardElement[10, 10].FoodRemaining);
        Check.Equal((byte)3, Board.BoardElement[12, 10].FoodRemaining);
        Check.Equal(3, left.LastOutcome.FoodGathered + right.LastOutcome.FoodGathered);
    }

    private static void GatherThenMove()
    {
        Reset();
        var gatherer = Actor(DnaTypes.GenerateFood, TargetTypes.MiddleRight);
        var mover = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        Board.BoardElement[10, 10].AddOccupant(gatherer); Board.BoardElement[11, 10].AddOccupant(mover);
        Board.ExecuteSingleSeason(true);
        Check.True(ReferenceEquals(Board.BoardElement[12, 10].Occupant, mover), "Recipient did not move");
        Check.Equal(8, mover.FoodBalance); Check.Equal(3, mover.TakenFood);
        Check.Equal((byte)0, Board.BoardElement[11, 10].FoodRemaining);
        Check.Equal((byte)3, Board.BoardElement[12, 10].FoodRemaining);
        new ExecuteOgranismSetup().Execute(Board.BoardElement[12, 10]);
        Check.Equal((byte)3, Board.BoardElement[12, 10].FoodRemaining, "Destination was debited next turn");
    }

    private static void GatherThenDeath()
    {
        Reset();
        var actor = Actor(DnaTypes.Move); var square = Board.BoardElement[10, 10]; square.AddOccupant(actor);
        Check.Equal(3, square.GatherFood(actor, 3));
        actor.SetState(health: 0, food: 8);
        new ExecuteOrganismDeadRule().Execute(square);
        Check.True(!square.IsOccupied, "Dead recipient was retained");
        Check.Equal((byte)8, square.FoodRemaining, "Remaining reserves were returned to wrong cell");
        new ExecuteOrganismDeadRule().Execute(square);
        Check.Equal((byte)8, square.FoodRemaining, "Death return duplicated");
    }

    private static void UniformAvailableNeighbors()
    {
        Reset();
        var square = Board.BoardElement[50, 50]; square.AddOccupant(Actor(DnaTypes.Move));
        TargetTypes[] neighbors = Enum.GetValues<TargetTypes>().Where(t => t != TargetTypes.Self).ToArray();
        for (int choice = 0; choice < neighbors.Length; choice++)
        {
            Common.ConfigureRandom(new ScriptedRandomSource(choice), true);
            Check.True(ReferenceEquals(square.GetNeightbor(neighbors[choice]), square.GetRandomEmptyNeighbor()),
                "A legal neighbor has no corresponding uniform index");
            Check.Equal(1L, Common.RandomSource.DrawCount);
        }
        // Removing every other candidate leaves only available cells in the draw range.
        foreach (var direction in neighbors.Where((_, i) => i % 2 == 0)) square.GetNeightbor(direction).AddOccupant(Actor(DnaTypes.Move));
        var remaining = neighbors.Where((_, i) => i % 2 != 0).ToArray();
        for (int choice = 0; choice < remaining.Length; choice++)
        {
            Common.ConfigureRandom(new ScriptedRandomSource(choice), true);
            Check.True(ReferenceEquals(square.GetNeightbor(remaining[choice]), square.GetRandomEmptyNeighbor()), "Occupied neighbor entered draw");
        }
        var corner = Board.BoardElement[0, 0]; corner.AddOccupant(Actor(DnaTypes.Move));
        foreach (int choice in new[] { 0, 1, 2 })
        {
            Common.ConfigureRandom(new ScriptedRandomSource(choice), true);
            var result = corner.GetRandomEmptyNeighbor();
            Check.True(result != null && result != corner && result.Position.X <= 1 && result.Position.Y <= 1,
                "Border or Self entered birth draw");
        }
    }

    private static void BirthAccounting()
    {
        Reset();
        var parent = Actor(DnaTypes.CombineDna, food: 10); var square = Board.BoardElement[10, 10]; square.AddOccupant(parent);
        Check.True(square.TryPlaceChild(parent), "First eligible birth failed");
        Check.Equal(8, parent.FoodBalance);
        Check.True(square.TryPlaceChild(parent), "Second birth consumed fertility twice");
        Check.Equal(6, parent.FoodBalance);
        Check.True(!square.TryPlaceChild(parent), "Fertility limit was exceeded");
        Check.Equal(6, parent.FoodBalance); Check.Equal(3, Board.BoardElement.Cast<BoardSquare>().Count(s => s.IsOccupied));
        Check.Equal(0, parent.PendingEffects.Count, "Placed birth left a deferred cost");
        Check.True(parent.Child == null, "Placed child retained as pending construction");
        foreach (var child in Board.BoardElement.Cast<BoardSquare>().Where(s => s.IsOccupied).Select(s => s.Occupant).Where(o => o != parent))
        { Check.Equal(2, child.FoodBalance); Check.Equal(1, child.Generation); Check.Equal(0L, child.LifetimeSeasons); }
    }

    private static void BlockedBirth()
    {
        Reset();
        var parent = Actor(DnaTypes.CombineDna); var square = Board.BoardElement[10, 10]; square.AddOccupant(parent);
        foreach (TargetTypes direction in Enum.GetValues<TargetTypes>().Where(t => t != TargetTypes.Self))
            square.GetNeightbor(direction).AddOccupant(Actor(DnaTypes.Move));
        long draws = Common.RandomSource.DrawCount;
        Check.True(!square.TryPlaceChild(parent), "Crowded birth succeeded");
        Check.Equal(draws, Common.RandomSource.DrawCount, "Blocked placement constructed a child or drew a location");
        Check.Equal(5, parent.FoodBalance); Check.True(parent.CanReproduceWith(parent), "Blocked birth consumed fertility");
        Check.True(parent.Child == null, "Blocked birth retained an unplaced child");
        square.GetNeightbor(TargetTypes.TopLeft).RemoveOccupant();
        Check.True(square.TryPlaceChild(parent), "Prior failed request prevented later explicit birth");
        Check.Equal(3, parent.FoodBalance);
    }

    private static void NewbornBoundary()
    {
        Reset();
        var parent = Actor(DnaTypes.CombineDna); Board.BoardElement[10, 10].AddOccupant(parent);
        Board.ExecuteSingleSeason(true);
        var child = Board.BoardElement.Cast<BoardSquare>().Where(s => s.IsOccupied).Select(s => s.Occupant).Single(o => o != parent);
        Check.Equal(0L, child.LifetimeSeasons); Check.True(child.LastAction == null && child.LastOutcome == null, "Newborn acted in birth season");
        Check.Equal(1L, parent.LifetimeSeasons); Check.True(parent.LastOutcome.BirthPlaced, "Placed birth outcome missing");
        Check.Equal(3, parent.FoodBalance); Check.Equal(2, parent.LastOutcome.FoodSpent);
        Board.ExecuteSingleSeason(true);
        Check.Equal(1L, child.LifetimeSeasons, "Newborn did not enter next season's cohort");
    }

    private static void BlockedMoveExpires()
    {
        Reset();
        var mover = Actor(DnaTypes.Move, TargetTypes.MiddleRight); var square = Board.BoardElement[10, 10]; square.AddOccupant(mover);
        Board.BoardElement[11, 10].AddOccupant(Actor(DnaTypes.Move));
        Board.ExecuteSingleSeason(true);
        Check.True(!mover.NextRequestedPosition.HasValue, "Blocked request leaked past movement phase");
        Check.True(!mover.LastOutcome.Moved && !mover.LastOutcome.Committed, "Blocked request counted as successful movement");
        Check.Equal("movement.blocked", mover.LastOutcome.Status);
        Board.BoardElement[11, 10].RemoveOccupant();
        mover.SetGenes((me, slot) => new EatDnaElement(me, slot));
        Board.ExecuteSingleSeason(true);
        Check.True(ReferenceEquals(square.Occupant, mover), "Old blocked movement executed with a later Eat action");
    }

    private static void MovementFallback()
    {
        Reset();
        var mover = Actor(DnaTypes.Move, TargetTypes.TopLeft); Board.BoardElement[0, 0].AddOccupant(mover);
        Board.ExecuteSingleSeason(true);
        Check.True(ReferenceEquals(Board.BoardElement[1, 1].Occupant, mover), "Opposite edge fallback changed");
        Check.Equal(TargetTypes.TopLeft, mover.LastAction.Target);
        Check.True(mover.LastOutcome.Moved, "Successful fallback missing from outcome");
        Check.Equal(1, mover.LastOutcome.DestinationX); Check.Equal(1, mover.LastOutcome.DestinationY);
        Check.True(!mover.NextRequestedPosition.HasValue, "Successful move retained request");
    }

    private static void VacatedCellMovesOnce()
    {
        Reset();
        var following = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        var leading = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        Board.BoardElement[10, 10].AddOccupant(following); Board.BoardElement[11, 10].AddOccupant(leading);
        // Two gene selections, then the two-actor shuffle chooses the leading actor first.
        Common.ConfigureRandom(new ScriptedRandomSource(0, 0, 0), true);
        Board.ExecuteSingleSeason(true);
        Check.True(ReferenceEquals(Board.BoardElement[11, 10].Occupant, following), "Follower failed to enter the vacated origin");
        Check.True(ReferenceEquals(Board.BoardElement[12, 10].Occupant, leading), "Leading actor moved more than one cell");
        Check.True(!Board.BoardElement[10, 10].IsOccupied && !Board.BoardElement[13, 10].IsOccupied,
            "A vacated cell caused duplicate occupancy or an extra movement");
        Check.True(following.LastOutcome.Moved && leading.LastOutcome.Moved, "Expected one move per original actor");
        Check.Equal(11, following.LastOutcome.DestinationX); Check.Equal(12, leading.LastOutcome.DestinationX);
        Check.True(!following.NextRequestedPosition.HasValue && !leading.NextRequestedPosition.HasValue,
            "Completed movement retained a request");
        Check.Equal(1, Check.Field<int>(following.DnaSequence[following.LastAction.Slot], "_executionNumber"));
        Check.Equal(1, Check.Field<int>(leading.DnaSequence[leading.LastAction.Slot], "_executionNumber"));
        Check.Equal(2, Board.BoardOrganismCount);
    }

    private static void CompletedOutcomes()
    {
        Reset();
        var attacker = Actor(DnaTypes.Kill, TargetTypes.MiddleRight); var victim = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        Board.BoardElement[10, 10].AddOccupant(attacker); Board.BoardElement[11, 10].AddOccupant(victim);
        Board.ExecuteSingleSeason(true);
        Check.True(victim.IsDead && !Board.BoardElement[11, 10].IsOccupied && !Board.BoardElement[12, 10].IsOccupied,
            "Dead original actor moved after removal");
        Check.True(!attacker.LastOutcome.TargetPresent && attacker.LastOutcome.TargetAfter.IsDead,
            "Attack credit used a partial target state");
        Check.True(victim.LastOutcome != null && !victim.LastOutcome.ActorPresent && victim.LastOutcome.ActorAfter.IsDead,
            "Removed actor was skipped during completion");
        Check.True(!victim.NextRequestedPosition.HasValue && !victim.LastOutcome.Committed,
            "Dead mover retained successful movement credit");
    }

    private static void ReproducibleResolution()
    {
        (string hash, long draws) Run()
        {
            Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 83,
                InitialPopulationPercent = 0 }, (x, y) => x >= 10 && x <= 12 && y >= 10 && y <= 12 ? new Organism() : null);
            for (int i = 0; i < 16; i++) Board.ExecuteSingleSeason(true);
            return (SimulationSnapshot.Capture().Sha256, Common.RandomSource.DrawCount);
        }
        var first = Run(); var second = Run();
        Check.Equal(first.hash, second.hash); Check.Equal(first.draws, second.draws);
    }
}
