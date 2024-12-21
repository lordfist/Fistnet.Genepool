using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using FistCore.Common;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Dna
{
    public enum DnaTypes : byte
    {
        Eat = 0,
        Move = 1,
        GenerateFood = 2,
        Kill = 3,
        CombineDna = 4,
        Evolve = 5,
        Heal = 6,
        Infect = 7
    }

    public enum TargetTypes : byte
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        MiddleLeft = 3,
        Self = 4,
        MiddleRight = 5,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8
    }

    public enum EffectTypes : byte
    {
        HealthChange = 0,
        FoodChange = 1,
        Movement = 2,
        Mutate = 3,
        Birth = 4,
        AgeChange = 5
    }

    public static class Common
    {
        static Common()
        {
            int seed = unchecked((int)DateTime.Now.Ticks - Environment.TickCount);
            random = new Random(seed);
        }

        private static Random random;

        public static long CalculateOrganismDnaCode(List<IDnaElement> dnaSequence)
        {
            long fullDnaCode = 0;
            foreach (IDnaElement item in dnaSequence)
            {
                fullDnaCode += item.DnaCode;
            }

            return fullDnaCode;
        }

        public static List<Color> GetOrganismColors(Organism organism)
        {
            List<Color> colors = new List<Color>();
            int colorNumber = Organism.DNA_SEQUENCE_MAXLENGTH / 8;

            for (int colorIndex = 0; colorIndex < colorNumber; colorIndex++)
            {
                int colorValue = 0;

                for (int i = 0; i < 8; i++)
                {
                    int exactIndex = colorIndex * 8 + i;

                    colorValue = colorValue << 3;
                    colorValue = colorValue + (byte)organism.DnaSequence[exactIndex].DnaType;
                }

                Color color = Color.FromArgb(colorValue);
                colors.Add(color);
            }

            return colors;
        }

        public static int GetRandomIntegerSeed()
        {
            int seed = unchecked((int)DateTime.Now.Ticks - Environment.TickCount);
            int value = 0;

            value = random.Next();
            lock (random)
            {
                if (value % random.Next(1, 11) == 1)
                    random = new Random(unchecked((int)DateTime.Now.Ticks - Environment.TickCount));
            }
            return random.Next();
        }

        public static int GetRandomIntegerSeed(int maxNumber)
        {
            int seed = unchecked((int)DateTime.Now.Ticks - Environment.TickCount);
            int value = 0;

            value = random.Next();
            lock (random)
            {
                if (value % random.Next(1, 11) == 1)
                    random = new Random(unchecked((int)DateTime.Now.Ticks - Environment.TickCount));
            }
            return random.Next(maxNumber);
        }

        public static TargetTypes TryChangeTarget(TargetTypes oldTarget)
        {
            if (Common.GetRandomIntegerSeed(Organism.TARGET_CHANGE_SCALE) <= Organism.TARGET_CHANGE_CHANCE)
                return (TargetTypes)(Common.GetRandomIntegerSeed() % 9);
            else
                return oldTarget;
        }

        public static TargetTypes GetRandomTarget()
        {
            return (TargetTypes)(Common.GetRandomIntegerSeed() % 9);
        }

        public static TargetTypes GetRandomTarget(int seed)
        {
            return (TargetTypes)(seed % 9);
        }

        public static TargetTypes GetOppositeTarget(TargetTypes target)
        {
            switch (target)
            {
                case TargetTypes.TopLeft:
                    return TargetTypes.BottomRight;

                case TargetTypes.TopCenter:
                    return TargetTypes.BottomCenter;

                case TargetTypes.TopRight:
                    return TargetTypes.BottomLeft;

                case TargetTypes.MiddleLeft:
                    return TargetTypes.MiddleRight;

                case TargetTypes.Self:
                    return TargetTypes.Self;

                case TargetTypes.MiddleRight:
                    return TargetTypes.MiddleLeft;

                case TargetTypes.BottomLeft:
                    return TargetTypes.TopRight;

                case TargetTypes.BottomCenter:
                    return TargetTypes.TopCenter;

                case TargetTypes.BottomRight:
                    return TargetTypes.TopLeft;

                default:
                    return target;
            }
        }

        #region Status snapshot.

        public static OrganismSnapshot CreateSnapshot(this Organism organism)
        {
            return new OrganismSnapshot(organism);
        }

        #endregion Status snapshot.

        #region Calculate change and difference.

        public static float CalculateChange(int currentValue, int prevousValue)
        {
            if (currentValue == prevousValue && prevousValue == 0)
                return 0;

            return (float)(currentValue - prevousValue) / (float)prevousValue;
        }

        public static float CalculateDifferenceFromValue(int value, int otherValue)
        {
            if (value == otherValue && otherValue == 0)
                return 0;

            float sumValues = (Math.Abs(value) + Math.Abs(otherValue));

            return (value / (float)sumValues) - 0.5f;
        }

        #endregion Calculate change and difference.
    }
}