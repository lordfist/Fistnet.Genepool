using System;

namespace Fistnet.Genepool.Dna.Elements.Brain
{
    public static class OrganismEvaluation
    {
        public const float AGE_EFFECT = 0.10f;
        public const float HEALTH_EFFECT = 0.35f;
        public const float FOOD_EFFECT_A = 0.15f;
        public const float FOOD_EFFECT_H = 0.15f;
        public const float CHILD_EFFECT = 0.25f;
        public const float SELF_KILL_PENALTY = -1.0f;

        // Frozen Part 2 adapter, identical for both learning policies:
        // .15*gathered + .15*(transferred-spent) + .25*placed birth
        // + .35*(actor health after-before) + .10*(actor age before-after)
        // - 1 if the actor is dead or absent after the shared turn.
        // Health/age are associative whole-turn feedback, not exclusive causal credit.
        // Available cell food, queued effects, HasChild, damage, movement and target
        // similarity earn no independent bonus. The numerical weights are retained,
        // but changed inputs break comparability with the old proxy reward. These
        // are uncalibrated model choices, not a claim of optimal ecological fitness.
        public static float EvaluateCompleted(ActionDecision decision, ActionOutcome outcome)
        {
            if (decision?.Candidate?.ActorBefore == null || outcome?.ActorAfter == null)
                throw new ArgumentException("Completed learning requires before and after actor snapshots.");
            if (outcome.FoodGathered < 0 || outcome.FoodTransferred < 0 || outcome.FoodSpent < 0)
                throw new ArgumentOutOfRangeException(nameof(outcome), "Committed resource counts must be nonnegative.");
            var before = decision.Candidate.ActorBefore;
            var after = outcome.ActorAfter;
            double value = .15 * outcome.FoodGathered + .15 * ((double)outcome.FoodTransferred - outcome.FoodSpent)
                + (outcome.BirthPlaced ? .25 : 0)
                + .35 * ((double)after.Health - before.Health)
                + .10 * ((double)before.Age - after.Age);
            if (!outcome.ActorPresent || after.IsDead) value -= 1;
            return FiniteScore(value);
        }

        public static float FiniteScore(double value)
        {
            if (!double.IsFinite(value)) throw new InvalidOperationException("Learning score must be finite.");
            return (float)Math.Clamp(value, -(double)float.MaxValue, float.MaxValue);
        }
    }
}
