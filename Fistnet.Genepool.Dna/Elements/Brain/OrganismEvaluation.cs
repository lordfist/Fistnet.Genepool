using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna.Elements.Brain
{
    public static class OrganismEvaluation
    {
        #region Evaluation constant.

        public const float AGE_EFFECT = 0.10f;
        public const float HEALTH_EFFECT = 0.35f;
        public const float FOOD_EFFECT_A = 0.15f;
        public const float FOOD_EFFECT_H = 0.15f;
        public const float CHILD_EFFECT = 0.25f;

        public const float FOOD_EFFECT_A_EMPTY = 0.4f;
        public const float FOOD_EFFECT_H_EMPTY = 0.2f;
        public const float CHILD_EFFECT_EMPTY = 0.4f;

        public const float SELF_KILL_PENALTY = -1.0f;

        #endregion Evaluation constant.

        public static float EvaluateSnapshot(OrganismSnapshot me, OrganismSnapshot other)
        {
            float value = 0;
            if (me.DnaCode == other.DnaCode)
            {
                value += Math.Abs(Common.CalculateDifferenceFromValue(me.Health, other.Health)) * HEALTH_EFFECT;
                value += Math.Abs(Common.CalculateDifferenceFromValue(me.Age, other.Age)) * AGE_EFFECT;
                value += Math.Abs(Common.CalculateDifferenceFromValue(me.AvailableFood, other.AvailableFood)) * FOOD_EFFECT_A;
                value += Math.Abs(Common.CalculateDifferenceFromValue(me.TakenFood, other.TakenFood)) * FOOD_EFFECT_H;
                if (me.HasChild)
                    value += CHILD_EFFECT;
                else if (other.HasChild)
                    value += CHILD_EFFECT;
            }
            else
            {
                value += Common.CalculateDifferenceFromValue(me.Health, other.Health) * HEALTH_EFFECT;
                value += Common.CalculateDifferenceFromValue(me.Age, other.Age) * AGE_EFFECT;
                value += Common.CalculateDifferenceFromValue(me.AvailableFood, other.AvailableFood) * FOOD_EFFECT_A;
                value += Common.CalculateDifferenceFromValue(me.TakenFood, other.TakenFood) * FOOD_EFFECT_H;
                if (me.HasChild)
                    value += CHILD_EFFECT;
            }
            return value;
        }

        public static float EvaluateEmptySpace(OrganismSnapshot me, OrganismSnapshot meOld)
        {
            float value = 0;
            if (me == null || meOld == null)
                return 0;

            value += Common.CalculateChange(me.AvailableFood, meOld.AvailableFood) * FOOD_EFFECT_A_EMPTY;
            value += Common.CalculateChange(me.TakenFood, meOld.TakenFood) * FOOD_EFFECT_H_EMPTY;

            if (me.HasChild)
                value += CHILD_EFFECT;

            return value;
        }
    }
}