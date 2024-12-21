using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class CreateFoodDnaElement : IDnaElement
    {
        #region DNA setup

        public int DnaCode
        {
            get;
            private set;
        }

        public byte DnaSequenceIndex
        {
            get;
            private set;
        }

        public TargetTypes Target
        {
            get;
            private set;
        }

        public DnaTypes DnaType
        {
            get { return DnaTypes.GenerateFood; }
        }

        public Organism Me
        {
            get;
            private set;
        }

        public void ExecuteDna(Organism organismAffected)
        {
            if (organismAffected != null && !organismAffected.IsDead && organismAffected.FoodBalance < Organism.MAX_FOOD_CARRY)
            {
                sbyte foodGathered = Organism.FOOD_GATHER;
                if (organismAffected.FoodBalance + foodGathered > Organism.MAX_FOOD_CARRY)
                    foodGathered = (sbyte)(Organism.MAX_FOOD_CARRY - organismAffected.FoodBalance);

                organismAffected.AddStackedEffect(new ChangeFoodEffect(organismAffected, foodGathered, this.DnaSequenceIndex));
            }
            else
                this.Target = Common.TryChangeTarget(this.Target);
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new CreateFoodDnaElement(child, this.Target, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private CreateFoodDnaElement(Organism me, TargetTypes target, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Target = target;
            this.DnaCode = oldDnaCode;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public CreateFoodDnaElement(Organism me, byte dnaSequenceIndex)
        {
            if (me == null)
                throw new ArgumentNullException("Self cannot be null when adding DNA sequence.");

            this.Me = me;
            this.DnaCode = Common.GetRandomIntegerSeed();
            this.Target = Common.GetRandomTarget(this.DnaCode);
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        #endregion Constructor.
    }
}