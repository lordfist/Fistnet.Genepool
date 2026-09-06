using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Dna.Elements.Brain;

namespace Fistnet.Genepool.Dna
{
    public class Organism : OrganismSnapshot
    {
        #region Constants.

        public const sbyte INITAL_FOOD_BALANCE = 5;
        public const sbyte INITAL_HEALTH = 10;
        public const sbyte OVERWEIGHT_DEATH = INITAL_HEALTH * 5;

        public const byte DNA_SEQUENCE_MAXLENGTH = 8;
        public const byte DNA_SEQUENCE_MAXMUTATE = DNA_SEQUENCE_MAXLENGTH / 4;

        public const byte DNA_SEQUENCE_MAXSINGLETYPE = DNA_SEQUENCE_MAXLENGTH / 2;

        public const byte MAX_AGE = 255;

        public const sbyte KILL_POWER = -1 * (INITAL_HEALTH + 1);
        public const sbyte KILL_OK_FOOD_EXTRA = 5;

        public const sbyte HEAL_POWER = INITAL_HEALTH;
        public const sbyte HEAL_AGE_REDUCTION = -5;
        public const sbyte HEAL_POWER_COST = -1;

        public const sbyte FEED_AGE_REDUCTION = -1;
        public const sbyte FEED_HEAL = 1;
        public const sbyte FEED_FOOD_COST = -1;

        public const sbyte MUTATE_HEAL_POWER = 2;
        public const sbyte MUTATE_FOOD_GATHER = 1;
        public const sbyte MUTATE_AGE_REDUCTION = -2;

        public const sbyte FOOD_GATHER = 3;
        public const sbyte MAX_FOOD_CARRY = 10;

        public const sbyte FOOD_REDUCTION_PER_AGE = -1;
        public const sbyte FOOD_REDUCTION_PER_BIRTH = -2;
        public const byte MAX_REPRODUCTIONS_PER_AGE = 2;

        public const byte INFECTION_CHANCE = 5; // chance 50%
        public const byte INFECTION_CHANCE_SCALE = 10;
        public const sbyte INFECT_POWER_COST = -1;

        public const byte TARGET_CHANGE_CHANCE = 4; // chance 40%
        public const byte TARGET_CHANGE_SCALE = 10;

        #endregion Constants.


        public List<IDnaElement> DnaSequence { get; protected set; }
        public StrategyNetwork Brain { get; private set; }
        private bool _isActive;
        private byte currentSequenceIndex;
        private byte _reproductionsInAge;
        public void Activate() => _isActive = true;
        public void Deactivate() => _isActive = false;
        public TargetTypes? NextRequestedPosition { get; private set; }
        public void MoveDone() => NextRequestedPosition = null;
        public int FoodCapacity => Math.Max(0, MAX_FOOD_CARRY - FoodBalance);
        public int GetAndResetTakenFood() { int taken = TakenFood; TakenFood = 0; return taken; }
        public void SetAvailableFood(int food) => AvailableFood = Math.Clamp(food, 0, MAX_FOOD_CARRY);
        public int ReceiveFood(int requested)
        {
            int actual = Math.Min(Math.Max(0, requested), FoodCapacity);
            FoodBalance += actual;
            TakenFood += actual;
            return actual;
        }
        // Pending construction is retired: a child exists only after a secured placement.
        public Organism Child => null;
        public void ReleaseChild() { }
        public GeneIdentity LastAction { get; private set; }
        public ActionOutcome LastOutcome { get; private set; }

        public ConcurrentBag<IDnaEffect> EffectsStack { get; private set; } = new ConcurrentBag<IDnaEffect>();
        private List<IDnaEffect> _referenceEffects = new List<IDnaEffect>();
        [ThreadStatic] private static List<IDnaEffect> capturedEffects;
        public IReadOnlyList<IDnaEffect> PendingEffects => Common.IsReferenceMode ? _referenceEffects.ToArray() : EffectsStack.ToArray();
        public void AddStackedEffect(IDnaEffect effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (capturedEffects != null) capturedEffects.Add(effect);
            else { EffectsStack.Add(effect); if (Common.IsReferenceMode) _referenceEffects.Add(effect); }
            SimulationDiagnostics.Current?.EffectQueued(this, effect);
        }
        private void ClearEffects() { EffectsStack = new ConcurrentBag<IDnaEffect>(); _referenceEffects.Clear(); }
        public void ExecuteEffectStack(Func<Organism, int, int> gather = null, Func<Organism, Organism, bool> birth = null)
        {
            // Compatibility/system-effect entry point. Board DNA actions use ResolveDecision atomically.
            if (EffectsStack.IsEmpty && _referenceEffects.Count == 0) { Starved(); return; }
            foreach (IDnaEffect effect in PendingEffects)
                ApplyEffect(effect, null, gather, birth);
            ClearEffects();
            Starved();
        }
        private void Starved()
        {
            if (FoodBalance >= 0) return;
            var before = new DiagnosticState(this);
            Health = SaturatingAdd(Health, FoodBalance);
            FoodBalance = 0;
            SimulationDiagnostics.Current?.Starvation(this, before);
        }
        private static int SaturatingAdd(int value, int delta) => (int)Math.Clamp((long)value + delta, int.MinValue, int.MaxValue);
        private void ChangeHealth(int delta)
        {
            Health = SaturatingAdd(Health, delta);
            if (HealthRule == HealthPolicy.Capped) Health = Math.Min(Health, Common.Policy.MaximumHealth);
        }
        private void ApplyEffect(IDnaEffect effect, ActionOutcome outcome, Func<Organism, int, int> gather,
            Func<Organism, Organism, bool> birth)
        {
            Organism recipient = effect.Me;
            DiagnosticState before = new DiagnosticState(recipient);
            int oldHealth = recipient.Health;
            switch (effect.Effect)
            {
                case EffectTypes.HealthChange:
                    recipient.ChangeHealth(Convert.ToInt32(effect.Value));
                    if (outcome != null)
                    { outcome.Damage += Math.Max(0, oldHealth - Math.Max(0, recipient.Health)); outcome.Healing += Math.Max(0, recipient.Health - oldHealth); }
                    break;
                case EffectTypes.FoodChange:
                    int food = Convert.ToInt32(effect.Value);
                    if (food > 0)
                    {
                        int actual;
                        if (gather != null) actual = gather(recipient, food);
                        else { actual = recipient.ReceiveFood(Math.Min(food, recipient.AvailableFood)); recipient.AvailableFood -= actual; }
                        if (outcome != null) outcome.FoodGathered += actual;
                    }
                    else recipient.FoodBalance = SaturatingAdd(recipient.FoodBalance, food);
                    break;
                case EffectTypes.Movement:
                    recipient.NextRequestedPosition = (TargetTypes)effect.Value;
                    break;
                case EffectTypes.Mutate:
                    int mutations = recipient.MutateMe(Convert.ToInt32(effect.Value),
                        ReferenceEquals(recipient, this) ? effect.DnaSequenceIndex : byte.MaxValue);
                    if (outcome != null) outcome.Mutations += mutations;
                    break;
                case EffectTypes.Birth:
                    bool placed = birth != null && birth(recipient, (Organism)effect.Value);
                    if (outcome != null) { outcome.BirthPlaced = placed; if (placed) outcome.FoodSpent += -FOOD_REDUCTION_PER_BIRTH; }
                    break;
                case EffectTypes.AgeChange:
                    recipient.Age = Math.Max(0, SaturatingAdd(recipient.Age, Convert.ToInt32(effect.Value)));
                    break;
                default: throw new NotSupportedException("Unknown effect type.");
            }
            SimulationDiagnostics.Current?.EffectApplied(recipient, effect, before);
        }

        public bool CanReproduceWith(Organism other) => !IsDead && other != null && !other.IsDead
            && Age >= 1 && Age < MAX_AGE && other.Age >= 1 && other.Age < MAX_AGE
            && FoodBalance >= -FOOD_REDUCTION_PER_BIRTH && _reproductionsInAge < MAX_REPRODUCTIONS_PER_AGE;
        public Organism CommitBirth(Organism other)
        {
            if (!CanReproduceWith(other)) return null;
            var child = new Organism(this, other);
            FoodBalance += FOOD_REDUCTION_PER_BIRTH;
            _reproductionsInAge++;
            HasChild = true;
            SimulationDiagnostics.Current?.ChildCreated(this, other, child);
            return child;
        }
        private int MutateMe(int seed, byte protectedSlot)
        {
            IRandomSource random = Common.CreateLocalRandom(seed);
            byte[] candidates = new byte[DNA_SEQUENCE_MAXLENGTH];
            for (int i = 0; i < candidates.Length; i++) candidates[i] = (byte)random.Next(DNA_SEQUENCE_MAXLENGTH);
            var selected = new HashSet<byte>();
            foreach (byte slot in candidates)
            {
                if (slot == protectedSlot || !selected.Add(slot)) continue;
                DnaSequence[slot] = DnaElementFactory.GetRandomDnaElement(this, slot);
                Brain.InvalidateGene(slot);
                if (selected.Count == DNA_SEQUENCE_MAXMUTATE) break;
            }
            DnaCode = Common.CalculateOrganismDnaCode(DnaSequence);
            return selected.Count;
        }

        public bool IsActionEligible(IDnaElement gene, Organism target)
        {
            if (IsDead || gene == null) return false;
            switch (gene.DnaType)
            {
                case DnaTypes.Move: return true; // Blocked attempts remain observable, including Self.
                case DnaTypes.Eat: return ReferenceEquals(target, this) && FoodBalance >= -FEED_FOOD_COST;
                case DnaTypes.Evolve: return ReferenceEquals(target, this);
                case DnaTypes.GenerateFood: return target != null && !target.IsDead && target.FoodCapacity > 0 && target.AvailableFood > 0;
                case DnaTypes.Kill: return target != null && !target.IsDead && (Common.Policy.Attack == AttackPolicy.DamageOnly
                    || (!ReferenceEquals(target, this) && FoodBalance >= Common.Policy.AttackCost));
                case DnaTypes.Heal: return target != null && !target.IsDead && target.DnaCode == DnaCode && FoodBalance >= -HEAL_POWER_COST
                    && target.Health < (target.HealthRule == HealthPolicy.Capped ? Common.Policy.MaximumHealth : OVERWEIGHT_DEATH - HEAL_POWER);
                case DnaTypes.CombineDna: return CanReproduceWith(target);
                case DnaTypes.Infect: return target != null && !target.IsDead && FoodBalance >= -INFECT_POWER_COST;
                default: return false;
            }
        }
        public ActionDecision PrepareSeason(IReadOnlyList<ActionCandidate> candidates)
        {
            if (!_isActive) return null;
            LifetimeSeasons++;
            HasChild = false;
            LastAction = null; LastOutcome = null;
            if (Age >= MAX_AGE)
            {
                var before = new DiagnosticState(this); Health = 0;
                SimulationDiagnostics.Current?.AgeLimit(this, before);
            }
            if (IsDead) return null;
            if (currentSequenceIndex >= DNA_SEQUENCE_MAXLENGTH)
            {
                using (SimulationDiagnostics.Current?.BeginSystemEffect(this, "aging-cost"))
                    AddStackedEffect(new ChangeFoodEffect(this, FOOD_REDUCTION_PER_AGE, DNA_SEQUENCE_MAXLENGTH));
                Age++; SequenceAge++; _reproductionsInAge = 0; currentSequenceIndex = 0;
                return null;
            }
            currentSequenceIndex++;
            return Brain.Choose(candidates, Common.Policy);
        }
        public void ResolveDecision(ActionDecision decision, Func<Organism, int, int> gather,
            Func<Organism, Organism, bool> birth)
        {
            if (decision != null && (decision.Completed || decision.Outcome.Attempted))
                throw new InvalidOperationException("Cannot resolve a decision twice.");
            if (decision != null && !ReferenceEquals(decision.Candidate.Gene.Me, this))
                throw new InvalidOperationException("Decision belongs to another actor.");
            ExecuteEffectStack(gather, birth); // This turn's aging/system effects; no learning occurs here.
            if (decision == null) return;
            ActionCandidate candidate = decision.Candidate;
            ActionOutcome outcome = decision.Outcome;
            outcome.Attempted = true;
            if (!IsActionEligible(candidate.Gene, candidate.Target)) { outcome.Status = "ineligible_at_resolution"; return; }
            if (capturedEffects != null) throw new InvalidOperationException("Nested action transaction.");
            var effects = new List<IDnaEffect>();
            capturedEffects = effects;
            try
            {
                using (SimulationDiagnostics.Current?.BeginAction(this, candidate.Gene, candidate.Target))
                    candidate.Gene.ExecuteDna(candidate.Target);
            }
            finally { capturedEffects = null; }
            var costs = new Dictionary<Organism, int>();
            foreach (var effect in effects)
                if (effect.Effect == EffectTypes.FoodChange && Convert.ToInt32(effect.Value) < 0)
                { costs.TryGetValue(effect.Me, out int existing); costs[effect.Me] = checked(existing - Convert.ToInt32(effect.Value)); }
            bool predator = candidate.Identity.Type == DnaTypes.Kill && Common.Policy.Attack == AttackPolicy.ReserveTransfer;
            if (predator) { costs.TryGetValue(this, out int oldCost); costs[this] = oldCost + Common.Policy.AttackCost; }
            if (costs.Any(pair => pair.Key.IsDead || pair.Key.FoodBalance < pair.Value))
            { outcome.Status = "insufficient_reserves"; return; }
            if (predator) { FoodBalance -= Common.Policy.AttackCost; outcome.FoodSpent += Common.Policy.AttackCost; }
            foreach (var effect in effects)
            {
                if (effect.Effect == EffectTypes.FoodChange && Convert.ToInt32(effect.Value) < 0)
                    outcome.FoodSpent -= Convert.ToInt32(effect.Value);
                ApplyEffect(effect, outcome, gather, birth);
            }
            if (predator && candidate.Target.Health <= 0 && !IsDead && outcome.Damage > 0)
            {
                // Only this direct lethal transaction transfers reserves; later attacks cannot target a corpse.
                int transfer = Math.Min(Common.Policy.AttackFoodLimit, Math.Min(FoodCapacity, Math.Max(0, candidate.Target.FoodBalance)));
                candidate.Target.FoodBalance -= transfer; FoodBalance += transfer; outcome.FoodTransferred = transfer;
            }
            outcome.Committed = effects.Count > 0 && (candidate.Identity.Type != DnaTypes.CombineDna || outcome.BirthPlaced);
            outcome.Status = outcome.Committed ? "committed" : "no_effect";
            SimulationDiagnostics.Current?.Count("actions.transactions_completed");
            SimulationDiagnostics.Current?.Count("food.transaction_spent", outcome.FoodSpent);
            SimulationDiagnostics.Current?.Count("food.transaction_gathered", outcome.FoodGathered);
            SimulationDiagnostics.Current?.Count("food.predation_transferred", outcome.FoodTransferred);
        }
        public void FinishSeason(ActionDecision decision, bool actorPresent, bool targetPresent)
        {
            if (decision == null) return;
            var outcome = decision.Outcome;
            outcome.ActorPresent = actorPresent; outcome.TargetPresent = targetPresent;
            outcome.ActorAfter = this.CreateSnapshot(); outcome.TargetAfter = decision.Candidate.Target?.CreateSnapshot();
            Brain.Complete(decision, outcome);
            LastAction = decision.Candidate.Identity; LastOutcome = outcome;
        }

        // Retained for small manual/legacy callers. Board uses PrepareSeason/ResolveDecision/FinishSeason.
        public void ExecuteNextDnaSequence(Organism target)
        {
            var candidates = DnaSequence.Select(g => new ActionCandidate(g, g.Target == TargetTypes.Self ? this : target,
                eligible: IsActionEligible(g, g.Target == TargetTypes.Self ? this : target))).ToList();
            var decision = PrepareSeason(candidates);
            ResolveDecision(decision, null, null);
            FinishSeason(decision, !IsDead, target != null && !target.IsDead);
        }
        public Organism()
        {
            Id = Common.NextOrganismId(); Health = HealthRule == HealthPolicy.Capped ? Math.Min(INITAL_HEALTH, Common.Policy.MaximumHealth) : INITAL_HEALTH; FoodBalance = INITAL_FOOD_BALANCE;
            DnaSequence = DnaElementFactory.GetRandomDnaSequence(this, DNA_SEQUENCE_MAXLENGTH);
            DnaCode = Common.CalculateOrganismDnaCode(DnaSequence); Brain = new StrategyNetwork(this);
        }
        public Organism(Organism parent1, Organism parent2)
        {
            Id = Common.NextOrganismId(); Parent1Id = parent1.Id; Parent2Id = parent2.Id;
            Generation = Math.Max(parent1.Generation, parent2.Generation) + 1;
            Health = HealthRule == HealthPolicy.Capped ? Math.Min(INITAL_HEALTH, Common.Policy.MaximumHealth) : INITAL_HEALTH; FoodBalance = -FOOD_REDUCTION_PER_BIRTH;
            DnaSequence = DnaElementFactory.GetDnaSequenceFromParents(this, parent1, parent2, DNA_SEQUENCE_MAXLENGTH);
            DnaCode = Common.CalculateOrganismDnaCode(DnaSequence);
            SequenceAge = DnaCode == parent1.DnaCode ? parent1.SequenceAge : 0;
            Brain = new StrategyNetwork(this); Brain.LearnFromParents(parent1, parent2);
        }
        public Organism(List<IDnaElement> dnaSequence)
        {
            Id = Common.NextOrganismId(); Health = HealthRule == HealthPolicy.Capped ? Math.Min(INITAL_HEALTH, Common.Policy.MaximumHealth) : INITAL_HEALTH; FoodBalance = INITAL_FOOD_BALANCE;
            DnaSequence = dnaSequence.Select(g => g.CopyToChild(this)).ToList();
            DnaCode = Common.CalculateOrganismDnaCode(DnaSequence); Brain = new StrategyNetwork(this);
        }
    }
}
