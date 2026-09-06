using System.Reflection;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class DnaActionTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("Eat requires one reserve and acts only on its owner", "dna-actions", EatContract);
        yield return new("gather credits the beneficiary from its actual bounded cell debit", "dna-actions", GatherContract);
        yield return new("Heal pays once and observes legacy or capped health eligibility", "dna-actions", HealContract);
        yield return new("Infect accepts exactly five of ten chance values and cannot act for free", "dna-actions", InfectionChance);
        yield return new("infection can mutate the recipient slot matching the attacker's active slot", "dna-actions", InfectionSlot);
        yield return new("Evolve preserves its executing slot and limits distinct mutations", "dna-actions", EvolveContract);
        yield return new("birth charges only secured placement and counts each birth once", "dna-actions", BirthContract);
        yield return new("damage-only attacks neither create nor transfer reserves", "dna-actions", DamageOnlyContract);
        yield return new("predation pays once and transfers only bounded victim reserves", "dna-actions", PredationContract);
        yield return new("Move queues its recorded direction and survives integer counter extremes", "dna-actions", MoveContract);
        yield return new("target change accepts exactly four of ten chance values", "dna-actions", TargetChance);
        yield return new("initial random sequence respects its type cap without reroll loops", "dna-actions", InitialSequenceCap);
    }

    private static ActionOutcome Resolve(Organism actor, IDnaElement gene, Organism target,
        Func<Organism, int, int> gather = null, Func<Organism, Organism, bool> birth = null)
    {
        var decision = new ActionDecision(new ActionCandidate(gene, target,
            eligible: actor.IsActionEligible(gene, target)));
        actor.ResolveDecision(decision, gather, birth);
        return decision.Outcome;
    }

    private static void EatContract()
    {
        var actor = new FixtureOrganism(); var other = new FixtureOrganism();
        var gene = new EatDnaElement(actor, 0);
        actor.SetState(food: 0, age: 3);
        gene.ExecuteDna(actor);
        Check.Equal(0, actor.PendingEffects.Count, "unaffordable direct invocation queued a benefit");
        Check.True(!Resolve(actor, gene, actor).Committed, "unaffordable Eat committed");
        Check.Equal(10, actor.Health); Check.Equal(3, actor.Age);
        actor.SetState(food: 1, age: 3);
        gene.ExecuteDna(other);
        Check.Equal(0, other.PendingEffects.Count, "Self action queued on another organism");
        var outcome = Resolve(actor, gene, actor);
        Check.True(outcome.Committed, "paid Eat did not commit");
        Check.Equal(1, outcome.FoodSpent); Check.Equal(1, outcome.Healing);
        Check.Equal(0, actor.FoodBalance); Check.Equal(11, actor.Health); Check.Equal(2, actor.Age);
    }

    private static void GatherContract()
    {
        var actor = new FixtureOrganism(); actor.SetState(food: 7);
        var target = new FixtureOrganism(); target.SetState(food: 9); target.SetAvailableFood(2);
        var gene = new CreateFoodDnaElement(actor, 0);
        int cellFood = 2, callbacks = 0;
        int Gather(Organism beneficiary, int requested)
        {
            callbacks++;
            Check.True(ReferenceEquals(target, beneficiary), "gather debited the actor's cell instead of the beneficiary's");
            int actual = beneficiary.ReceiveFood(Math.Min(requested, cellFood));
            cellFood -= actual; beneficiary.SetAvailableFood(cellFood);
            return actual;
        }
        var outcome = Resolve(actor, gene, target, Gather);
        Check.Equal(1, outcome.FoodGathered); Check.Equal(1, cellFood);
        Check.Equal(7, actor.FoodBalance); Check.Equal(10, target.FoodBalance);
        Check.True(!Resolve(actor, gene, target, Gather).Committed, "full beneficiary gathered");
        target.SetState(food: 0); target.SetAvailableFood(0);
        Check.True(!Resolve(actor, gene, target, Gather).Committed, "empty cell gathered");
        Check.Equal(1, callbacks, "ineligible gather invoked the resource callback");
    }

    private static void HealContract()
    {
        foreach (HealthPolicy policy in Enum.GetValues<HealthPolicy>())
        {
            Common.ConfigurePolicy(new SimulationPolicy { Health = policy });
            var actor = new FixtureOrganism(); var gene = new HealDnaElement(actor, 0);
            var target = new FixtureOrganism(); target.CopyGenes(actor);
            actor.SetState(food: 0); target.SetState(health: 10, age: 7);
            gene.ExecuteDna(target);
            Check.Equal(0, target.PendingEffects.Count, "unpaid Heal queued benefits");
            actor.SetState(food: 1);
            int initialHealth = policy == HealthPolicy.Capped ? 49 : 39;
            target.SetState(health: initialHealth, age: 7);
            var outcome = Resolve(actor, gene, target);
            Check.Equal(1, outcome.FoodSpent); Check.Equal(0, actor.FoodBalance);
            Check.Equal(policy == HealthPolicy.Capped ? 50 : 49, target.Health);
            Check.Equal(2, target.Age); Check.True(!target.IsDead, "legal Heal crossed its vitality boundary");
            actor.SetState(food: 1);
            Check.True(!Resolve(actor, gene, target).Committed, "Heal committed above its eligibility bound");
            Check.Equal(1, actor.FoodBalance, "blocked Heal charged reserves");
        }
        Common.ConfigurePolicy(new SimulationPolicy());
    }

    private static void InfectionChance()
    {
        int successes = 0;
        for (int chance = 0; chance < Organism.INFECTION_CHANCE_SCALE; chance++)
        {
            var actor = new FixtureOrganism(); var target = new FixtureOrganism();
            var gene = new InfectDnaElement(actor, 0);
            Check.Property(target, "DnaCode", actor.DnaCode + 1);
            actor.SetState(food: 1);
            Common.ConfigureRandom(new ScriptedRandomSource(chance), true);
            var outcome = Resolve(actor, gene, target);
            bool expected = chance < Organism.INFECTION_CHANCE;
            Check.Equal(expected, outcome.Committed, "infection chance " + chance);
            Check.Equal(expected ? 1 : 0, outcome.FoodSpent);
            Check.Equal(expected ? 0 : 1, actor.FoodBalance);
            if (outcome.Committed) successes++;
        }
        Check.Equal(5, successes);
        var unpaid = new FixtureOrganism(); var recipient = new FixtureOrganism();
        var infect = new InfectDnaElement(unpaid, 0); unpaid.SetState(food: 0);
        Check.Property(recipient, "DnaCode", unpaid.DnaCode + 1);
        var random = new ScriptedRandomSource(0); Common.ConfigureRandom(random, true);
        infect.ExecuteDna(recipient);
        Check.Equal(0, unpaid.PendingEffects.Count); Check.Equal(0, recipient.PendingEffects.Count);
        Check.Equal(0L, random.DrawCount, "unaffordable infection drew its chance");
    }

    private static void InfectionSlot()
    {
        var actor = new FixtureOrganism(); var target = new FixtureOrganism();
        var gene = new InfectDnaElement(actor, 3); actor.SetState(food: 1);
        var original = target.DnaSequence[3];
        Common.ConfigureRandom(new ScriptedRandomSource(0, 3, 3, 3, 3, 3, 3, 3, 3), true);
        var outcome = Resolve(actor, gene, target);
        Check.Equal(1, outcome.Mutations);
        Check.True(!ReferenceEquals(original, target.DnaSequence[3]), "attacker slot incorrectly protected the recipient's slot");
    }

    private static void EvolveContract()
    {
        var actor = new FixtureOrganism(); actor.SetState(food: 3, age: 3);
        var gene = new EvolveDnaElement(actor, 3); actor.DnaSequence[3] = gene;
        var before = actor.DnaSequence.ToArray();
        Common.ConfigureRandom(new ScriptedRandomSource(3, 1, 1, 3, 6, 6, 7, 7), true);
        var outcome = Resolve(actor, gene, actor);
        Check.Equal(2, outcome.Mutations); Check.Equal(12, actor.Health); Check.Equal(1, actor.Age);
        Check.Equal(3, actor.FoodBalance);
        for (int slot = 0; slot < before.Length; slot++)
            Check.Equal(slot != 1 && slot != 6, ReferenceEquals(before[slot], actor.DnaSequence[slot]), "mutated slot " + slot);
        var other = new FixtureOrganism(); gene.ExecuteDna(other);
        Check.Equal(0, other.PendingEffects.Count, "Evolve affected another organism");
    }

    private static void BirthContract()
    {
        var actor = new FixtureOrganism(); var mate = new FixtureOrganism();
        actor.SetState(food: 10, age: 1); mate.SetState(age: 1);
        var gene = new CombineDnaElement(actor, 0);
        var blocked = Resolve(actor, gene, mate, birth: (_, _) => false);
        Check.True(!blocked.Committed && !blocked.BirthPlaced, "blocked placement reported a birth");
        Check.Equal(0, blocked.FoodSpent); Check.Equal(10, actor.FoodBalance);
        int placements = 0;
        bool Place(Organism parent, Organism partner)
        {
            var child = parent.CommitBirth(partner);
            Check.True(child != null, "eligible placement did not construct a child");
            Check.Equal(2, child.FoodBalance);
            Check.True(!ReferenceEquals(child.DnaSequence[0], parent.DnaSequence[0]), "child retained the parent's mutable gene");
            placements++; return true;
        }
        for (int i = 0; i < Organism.MAX_REPRODUCTIONS_PER_AGE; i++)
        {
            var outcome = Resolve(actor, gene, mate, birth: Place);
            Check.True(outcome.Committed && outcome.BirthPlaced, "secured birth did not commit");
            Check.Equal(2, outcome.FoodSpent);
        }
        Check.True(!Resolve(actor, gene, mate, birth: Place).Committed, "per-age birth cap exceeded");
        Check.Equal(2, placements); Check.Equal(6, actor.FoodBalance);
        var cloneParent = new FixtureOrganism(); cloneParent.SetState(food: 2, age: 1);
        Check.True(cloneParent.CanReproduceWith(cloneParent), "existing self-reproduction route disappeared");
        cloneParent.SetState(food: 1, age: 1);
        var unpaid = new CombineDnaElement(cloneParent, 0); unpaid.ExecuteDna(cloneParent);
        Check.Equal(0, cloneParent.PendingEffects.Count, "unaffordable birth queued");
    }

    private static void DamageOnlyContract()
    {
        Common.ConfigurePolicy(new SimulationPolicy { Attack = AttackPolicy.DamageOnly });
        var actor = new FixtureOrganism(); actor.SetState(food: 0);
        var victim = new FixtureOrganism(); victim.SetState(food: 7);
        var gene = new KillDnaElement(actor, 0);
        var outcome = Resolve(actor, gene, victim);
        Check.True(outcome.Committed && victim.IsDead, "damage-only attack did not resolve");
        Check.Equal(10, outcome.Damage); Check.Equal(0, outcome.FoodSpent); Check.Equal(0, outcome.FoodTransferred);
        Check.Equal(0, actor.FoodBalance); Check.Equal(7, victim.FoodBalance);
        Check.True(!Resolve(actor, gene, victim).Committed, "a second action attacked a corpse");
    }

    private static void PredationContract()
    {
        Common.ConfigurePolicy(new SimulationPolicy { Attack = AttackPolicy.ReserveTransfer });
        var actor = new FixtureOrganism(); actor.SetState(food: 1);
        var victim = new FixtureOrganism(); victim.SetState(food: 8);
        var gene = new KillDnaElement(actor, 0);
        var outcome = Resolve(actor, gene, victim);
        Check.Equal(1, outcome.FoodSpent); Check.Equal(5, outcome.FoodTransferred);
        Check.Equal(5, actor.FoodBalance); Check.Equal(3, victim.FoodBalance);
        Check.True(!Resolve(actor, gene, victim).Committed, "victim transferred reserves twice");
        Check.True(!Resolve(actor, gene, actor).Committed, "predation accepted self as prey");
        actor.SetState(food: 0); victim.SetState(food: 8);
        gene.ExecuteDna(victim);
        Check.Equal(0, victim.PendingEffects.Count, "unpaid predation queued damage");
        Common.ConfigurePolicy(new SimulationPolicy());
    }

    private static void MoveContract()
    {
        var actor = new FixtureOrganism(); var gene = new MoveDnaElement(actor, 0);
        Check.Property(gene, "Target", TargetTypes.BottomRight); Check.Property(gene, "DnaCode", int.MinValue);
        typeof(MoveDnaElement).GetField("_executionNumber", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gene, int.MaxValue);
        var outcome = Resolve(actor, gene, null);
        Check.True(outcome.Committed, "Move request did not commit");
        Check.Equal<TargetTypes?>(TargetTypes.BottomRight, actor.NextRequestedPosition);
        Check.Equal(1, Check.Field<int>(gene, "_executionNumber"));
    }

    private static void TargetChance()
    {
        int changes = 0;
        for (int chance = 0; chance < Organism.TARGET_CHANGE_SCALE; chance++)
        {
            Common.ConfigureRandom(new ScriptedRandomSource(chance, (int)TargetTypes.BottomRight), true);
            var result = Common.TryChangeTarget(TargetTypes.TopLeft);
            Check.Equal(chance < Organism.TARGET_CHANGE_CHANCE ? TargetTypes.BottomRight : TargetTypes.TopLeft, result);
            if (result != TargetTypes.TopLeft) changes++;
        }
        Check.Equal(4, changes);
    }

    private static void InitialSequenceCap()
    {
        var owner = new FixtureOrganism();
        var random = new ScriptedRandomSource(); Common.ConfigureRandom(random, true);
        var sequence = DnaElementFactory.GetRandomDnaSequence(owner, Organism.DNA_SEQUENCE_MAXLENGTH);
        Check.Equal(8, sequence.Count);
        Check.True(sequence.GroupBy(g => g.DnaType).All(group => group.Count() <= Organism.DNA_SEQUENCE_MAXSINGLETYPE), "type cap exceeded");
        Check.Equal(16L, random.DrawCount, "bounded generation unexpectedly retried");
        Check.Equal(4, sequence.Count(g => g.DnaType == DnaTypes.Eat));
        Check.Equal(4, sequence.Count(g => g.DnaType == DnaTypes.Move));
        for (int slot = 0; slot < sequence.Count; slot++)
        { Check.Equal((byte)slot, sequence[slot].DnaSequenceIndex); Check.True(ReferenceEquals(owner, sequence[slot].Me), "wrong gene owner"); }
        bool rejected = false;
        try { DnaElementFactory.GetRandomDnaSequence(owner, 33); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check.True(rejected, "impossible capped sequence was not rejected");
    }
}
