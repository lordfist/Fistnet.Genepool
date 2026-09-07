using System;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Dna
{
    public enum LearningPolicy { RepairedLegacy, BoundedExploratory }
    public enum HealthPolicy { LegacyOverweight, Capped }
    public enum AttackPolicy { DamageOnly, ReserveTransfer }

    // Candidate settings are opt-in. They are not calibrated ecological defaults.
    public sealed class SimulationPolicy
    {
        public LearningPolicy Learning { get; init; } = LearningPolicy.RepairedLegacy;
        public HealthPolicy Health { get; init; } = HealthPolicy.LegacyOverweight;
        public AttackPolicy Attack { get; init; } = AttackPolicy.DamageOnly;
        public int ExternalContextLimit { get; init; } = 64;
        public int ExplorationPercent { get; init; } = 10;
        public int MaximumHealth { get; init; } = 50;
        public int AttackCost { get; init; } = 1;
        public int AttackFoodLimit { get; init; } = 5;
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(LearningPolicy), Learning) || !Enum.IsDefined(typeof(HealthPolicy), Health)
                || !Enum.IsDefined(typeof(AttackPolicy), Attack) || ExternalContextLimit < 1 || ExternalContextLimit > 4096
                || ExplorationPercent < 0 || ExplorationPercent > 100 || MaximumHealth < 1 || MaximumHealth > 10000
                || AttackCost < 0 || AttackCost > Organism.MAX_FOOD_CARRY || AttackFoodLimit < 0 || AttackFoodLimit > Organism.MAX_FOOD_CARRY)
                throw new ArgumentOutOfRangeException(nameof(SimulationPolicy));
        }
    }

    public sealed class GeneIdentity
    {
        public byte Slot { get; }
        public int Code { get; }
        public DnaTypes Type { get; }
        public TargetTypes Target { get; }
        public string Implementation { get; }
        public GeneIdentity(IDnaElement gene)
        { Slot = gene.DnaSequenceIndex; Code = gene.DnaCode; Type = gene.DnaType; Target = gene.Target; Implementation = gene.GetType().FullName; }
        public bool Matches(IDnaElement gene) => gene != null && Slot == gene.DnaSequenceIndex && Code == gene.DnaCode
            && Type == gene.DnaType && Target == gene.Target && Implementation == gene.GetType().FullName;
    }

    public sealed class ActionCandidate
    {
        public IDnaElement Gene { get; }
        public GeneIdentity Identity { get; }
        public Organism Target { get; }
        public int ActorX { get; }
        public int ActorY { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public bool InBounds { get; }
        public bool Eligible { get; }
        public string Context { get; }
        public OrganismSnapshot ActorBefore { get; }
        public OrganismSnapshot TargetBefore { get; }
        public ActionCandidate(IDnaElement gene, Organism target, int actorX = -1, int actorY = -1,
            int targetX = -1, int targetY = -1, bool inBounds = true, bool eligible = true)
        {
            Gene = gene; Identity = new GeneIdentity(gene); Target = target;
            ActorX = actorX; ActorY = actorY; TargetX = targetX; TargetY = targetY; InBounds = inBounds; Eligible = eligible;
            Context = ReferenceEquals(gene.Me, target) ? "Self" : !inBounds ? "OutOfBounds" : target == null ? "Empty" : "E:" + target.DnaCode;
            ActorBefore = gene.Me.CreateSnapshot(); TargetBefore = target?.CreateSnapshot();
        }
    }

    public sealed class ActionDecision
    {
        public string ChoiceLabel { get; set; }
        public ActionCandidate Candidate { get; }
        public ActionOutcome Outcome { get; } = new ActionOutcome();
        public bool Completed { get; set; }
        public ActionDecision(ActionCandidate candidate) { Candidate = candidate; }
    }

    // Persist only values. Decisions retain live references only until the shared turn finishes.
    public sealed class ActionOutcome
    {
        public string Status { get; set; } = "selected";
        public bool Attempted { get; set; }
        public bool Committed { get; set; }
        public bool Moved { get; set; }
        public bool BirthPlaced { get; set; }
        public int FoodSpent { get; set; }
        public int FoodGathered { get; set; }
        public int FoodTransferred { get; set; }
        public int Damage { get; set; }
        public int Healing { get; set; }
        public int Mutations { get; set; }
        public bool ActorPresent { get; set; }
        public bool TargetPresent { get; set; }
        public bool StaleCreditDiscarded { get; set; }
        public int DestinationX { get; set; } = -1;
        public int DestinationY { get; set; } = -1;
        public OrganismSnapshot ActorAfter { get; set; }
        public OrganismSnapshot TargetAfter { get; set; }
        public float Reward { get; set; }
    }
}
