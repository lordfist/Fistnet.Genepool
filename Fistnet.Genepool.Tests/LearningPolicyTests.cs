using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Dna.Elements.Brain;

namespace Fistnet.Genepool.Tests;

internal static class LearningPolicyTests
{
    private static FixtureOrganism Actor()
    {
        var actor = new FixtureOrganism();
        actor.SetGenes((me, slot) => new MoveDnaElement(me, slot));
        actor.SetState();
        actor.Brain.SynchronizeGenes();
        return actor;
    }

    private static FixtureOrganism Target(long code)
    {
        var target = new FixtureOrganism(); target.SetState();
        Check.Property(target, "DnaCode", code);
        return target;
    }

    private static ActionOutcome Outcome(Organism actor) =>
        new() { ActorPresent = true, ActorAfter = actor.CreateSnapshot(), Attempted = true };

    private static void Complete(Organism actor, ActionDecision decision) => actor.Brain.Complete(decision, Outcome(actor));
    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check.True(rejected, "Invalid learning operation was accepted");
    }

    private static SimulationPolicy Bounded(int limit = 64, int exploration = 10) =>
        new() { Learning = LearningPolicy.BoundedExploratory, ExternalContextLimit = limit, ExplorationPercent = exploration };

    private static void Visit(Organism actor, Organism target, SimulationPolicy policy, byte slot = 0, bool inBounds = true)
    {
        var decision = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[slot], target, inBounds: inBounds) }, policy);
        Complete(actor, decision);
    }

    public static IEnumerable<TestCase> Cases()
    {
        yield return new("each gene competes using its own resolved target context", "learning", () =>
        {
            var actor = Actor(); var first = Target(1010); var second = Target(2020);
            Check.Scores(actor)["E:1010"] = new() { [0] = 2, [1] = 100 };
            Check.Scores(actor)["E:2020"] = new() { [0] = 100, [1] = 1 };
            var random = new ScriptedRandomSource(); Common.ConfigureRandom(random, true);
            var choice = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], first),
                new ActionCandidate(actor.DnaSequence[1], second) }, new SimulationPolicy());
            Check.Equal((byte)0, choice.Candidate.Identity.Slot);
            Check.Equal(0L, random.DrawCount, "Greedy control unexpectedly drew random numbers");
            Check.True(ReferenceEquals(first, choice.Candidate.Target), "Chosen target was changed");
            Complete(actor, choice);
        });
        yield return new("self identity is distinct from a same-DNA neighbour and empty boundaries", "learning", () =>
        {
            var actor = Actor(); var neighbour = Target(actor.DnaCode);
            Check.Equal("Self", new ActionCandidate(actor.DnaSequence[0], actor).Context);
            Check.Equal("E:" + actor.DnaCode, new ActionCandidate(actor.DnaSequence[0], neighbour).Context);
            Check.Equal("Empty", new ActionCandidate(actor.DnaSequence[0], null).Context);
            Check.Equal("OutOfBounds", new ActionCandidate(actor.DnaSequence[0], null, inBounds: false).Context);
            Visit(actor, actor, Bounded()); Visit(actor, neighbour, Bounded());
            Visit(actor, null, Bounded()); Visit(actor, null, Bounded(), inBounds: false);
            Check.Equal(4, actor.Brain.LearningContextCount);
            Check.Equal(1, actor.Brain.ExternalLearningContextCount);
        });
        yield return new("one immutable selected gene is completed exactly once", "learning", () =>
        {
            var actor = Actor();
            var candidate = new ActionCandidate(actor.DnaSequence[0], null);
            var choice = actor.Brain.Choose(new[] { candidate }, new SimulationPolicy());
            Reject(() => actor.Brain.Choose(new[] { candidate }, new SimulationPolicy()));
            Check.Equal(0, actor.Brain.LearningEntryCount, "Selection learned before outcomes completed");
            actor.SetState(health: 12);
            Check.Equal(10, choice.Candidate.ActorBefore.Health, "Decision snapshot changed with actor");
            Complete(actor, choice);
            Check.True(choice.Completed, "Completion flag absent");
            Reject(() => Complete(actor, choice));
            Check.True(Math.Abs(Check.Scores(actor)["Empty"][0] - .7f) < .000001f, "Whole-turn health feedback was not credited once");
            var noChoice = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], null, eligible: false) }, Common.Policy);
            Check.True(noChoice == null, "Ineligible gene selected");
        });
        yield return new("reward uses committed transfers birth and whole-turn actor snapshots only", "learning", () =>
        {
            var actor = Actor(); actor.SetState(health: 10, age: 8);
            var choice = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], null) }, Common.Policy);
            actor.SetState(health: 12, age: 5); actor.SetAvailableFood(999);
            var outcome = Outcome(actor);
            outcome.FoodGathered = 4; outcome.FoodTransferred = 3; outcome.FoodSpent = 2;
            outcome.BirthPlaced = true; outcome.Moved = true; outcome.Damage = 999;
            actor.Brain.Complete(choice, outcome);
            Check.True(Math.Abs(outcome.Reward - 2f) < .000001f, "Frozen scalar adapter changed");
            var removed = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], null) }, Common.Policy);
            var removedOutcome = Outcome(actor); removedOutcome.ActorPresent = false;
            actor.Brain.Complete(removed, removedOutcome);
            Check.Equal(-1f, removedOutcome.Reward, "Removed actor penalty omitted");
            var other = Target(444);
            var targetChoice = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[1], other) }, Common.Policy);
            other.SetState(health: 0);
            var targetOutcome = Outcome(actor); targetOutcome.TargetAfter = other.CreateSnapshot(); targetOutcome.Damage = 10;
            actor.Brain.Complete(targetChoice, targetOutcome);
            Check.Equal(0f, targetOutcome.Reward, "Damage or target comparison added an unfrozen reward");
        });
        yield return new("gene target mutation invalidates old scores and discards pending credit", "learning", () =>
        {
            var actor = Actor();
            Check.Scores(actor)["Empty"] = new() { [0] = 9, [1] = 4 };
            var choice = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], null) }, Common.Policy);
            int oldCode = choice.Candidate.Identity.Code;
            var target = actor.DnaSequence[0].Target == TargetTypes.Self ? TargetTypes.TopLeft : TargetTypes.Self;
            Check.Property(actor.DnaSequence[0], "Target", target);
            var result = Outcome(actor); actor.Brain.Complete(choice, result);
            Check.True(result.StaleCreditDiscarded, "Mutated action received old credit");
            Check.True(!Check.Scores(actor)["Empty"].ContainsKey(0), "Old target score remained");
            Check.Equal(4f, Check.Scores(actor)["Empty"][1], "Unchanged gene score was removed");
            Check.Equal(oldCode, choice.Candidate.Identity.Code);
            actor.Brain.InvalidateGene(1);
            Check.Equal(0, actor.Brain.LearningEntryCount);
        });
        yield return new("repaired legacy and optional exploration have explicit distinct selection rules", "learning", () =>
        {
            var actor = Actor();
            Check.Scores(actor)["Empty"] = new() { [0] = 2 };
            var candidates = new[] { new ActionCandidate(actor.DnaSequence[0], null), new ActionCandidate(actor.DnaSequence[1], null) };
            var legacy = actor.Brain.Choose(candidates, new SimulationPolicy());
            Check.Equal((byte)0, legacy.Candidate.Identity.Slot); Complete(actor, legacy);
            var unseen = actor.Brain.Choose(candidates, Bounded());
            Check.Equal((byte)1, unseen.Candidate.Identity.Slot); Complete(actor, unseen);
            Common.ConfigureRandom(new ScriptedRandomSource(0, 1), true);
            var explore = actor.Brain.Choose(candidates, Bounded());
            Check.Equal((byte)1, explore.Candidate.Identity.Slot); Complete(actor, explore);
            Common.ConfigureRandom(new ScriptedRandomSource(10), true);
            var greedy = actor.Brain.Choose(candidates, Bounded());
            Check.Equal((byte)0, greedy.Candidate.Identity.Slot); Complete(actor, greedy);
            Check.Scores(actor)["Empty"][1] = 2;
            Common.ConfigureRandom(new ScriptedRandomSource(99, 1), true);
            var tie = actor.Brain.Choose(candidates, Bounded());
            Check.Equal((byte)1, tie.Candidate.Identity.Slot); Complete(actor, tie);
            Check.Scores(actor)["Empty"][0] = -1; Check.Scores(actor)["Empty"][1] = -2;
            Common.ConfigureRandom(new ScriptedRandomSource(1), true);
            var negative = actor.Brain.Choose(candidates, new SimulationPolicy());
            Check.Equal((byte)1, negative.Candidate.Identity.Slot); Complete(actor, negative);
        });
        yield return new("bounded history evicts least recently selected external context and pins special contexts", "learning", () =>
        {
            var actor = Actor(); var policy = Bounded(2, 0);
            var first = Target(11); var second = Target(22); var third = Target(33);
            Visit(actor, first, policy); Visit(actor, second, policy); Visit(actor, first, policy);
            Visit(actor, actor, policy); Visit(actor, null, policy); Visit(actor, null, policy, inBounds: false);
            Visit(actor, third, policy);
            Check.True(Check.Scores(actor).ContainsKey("E:11") && Check.Scores(actor).ContainsKey("E:33")
                && !Check.Scores(actor).ContainsKey("E:22"), "LRU evicted a recently selected context");
            Check.Equal(5, actor.Brain.LearningContextCount);
            var defaultActor = Actor();
            for (int i = 1; i <= 65; i++) Visit(defaultActor, Target(i), Bounded(exploration: 0));
            Check.Equal(64, defaultActor.Brain.ExternalLearningContextCount);
            Check.True(!Check.Scores(defaultActor).ContainsKey("E:1"), "Default 64-context limit not enforced");
        });
        yield return new("two-parent inheritance averages independently matching genes with a bounded union", "learning", () =>
        {
            Common.ConfigurePolicy(Bounded(2, 0));
            var first = Actor(); var second = Actor(); second.CopyGenes(first);
            var child = Actor(); child.CopyGenes(first);
            Check.Scores(first)["Self"] = new() { [0] = 2, [1] = 3 };
            Check.Scores(second)["Self"] = new() { [0] = 6 };
            Visit(first, Target(1), Common.Policy); Visit(first, Target(2), Common.Policy);
            Visit(second, Target(3), Common.Policy); Visit(second, Target(4), Common.Policy);
            child.Brain.LearnFromParents(first, second);
            Check.Equal(4f, Check.Scores(child)["Self"][0]);
            Check.Equal(3f, Check.Scores(child)["Self"][1]);
            Check.Equal(2, child.Brain.ExternalLearningContextCount);
            Check.True(Check.Scores(child).ContainsKey("E:2") && Check.Scores(child).ContainsKey("E:4"),
                "Bounded child history did not alternate newest parent contexts");
            Check.Scores(child)["Self"][0] = 99;
            Check.Equal(2f, Check.Scores(first)["Self"][0], "Child shares parent scores");
            Check.Equal(6f, Check.Scores(second)["Self"][0], "Child shares second parent scores");
        });
        yield return new("learning arithmetic saturates finite scores and rejects nonfinite input", "learning", () =>
        {
            Check.Equal(float.MaxValue, OrganismEvaluation.FiniteScore((double)float.MaxValue * 2));
            Check.Equal(-float.MaxValue, OrganismEvaluation.FiniteScore(-(double)float.MaxValue * 2));
            Reject(() => OrganismEvaluation.FiniteScore(double.NaN));
            var actor = Actor();
            Check.Scores(actor)["Empty"] = new() { [0] = float.NaN };
            Reject(() => actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], null) }, Common.Policy));
            Check.Scores(actor)["Empty"][0] = float.MaxValue;
            var choice = actor.Brain.Choose(new[] { new ActionCandidate(actor.DnaSequence[0], null) }, Common.Policy);
            var result = Outcome(actor); result.FoodGathered = int.MaxValue;
            actor.Brain.Complete(choice, result);
            Check.True(float.IsFinite(Check.Scores(actor)["Empty"][0]), "Score became infinite");
        });
    }
}
