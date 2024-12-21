using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class EvolveDnaElement : IDnaElement
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
            get { return TargetTypes.Self; }
        }

        public DnaTypes DnaType
        {
            get { return DnaTypes.Evolve; }
        }

        public Organism Me
        {
            get;
            private set;
        }

        public void ExecuteDna(Organism organismAffected)
        {
            if (organismAffected != null && !organismAffected.IsDead)
            {
                organismAffected.AddStackedEffect(new MutateEffect(organismAffected, this.DnaCode, this.DnaSequenceIndex));

                if (organismAffected.Health <= Organism.INITAL_HEALTH)
                    organismAffected.AddStackedEffect(new ChangeHealthEffect(organismAffected, Organism.MUTATE_HEAL_POWER, this.DnaSequenceIndex));

                organismAffected.AddStackedEffect(new ChangeAgeEffect(organismAffected, Organism.MUTATE_AGE_REDUCTION, this.DnaSequenceIndex));

                if (organismAffected.FoodBalance < 0)
                    organismAffected.AddStackedEffect(new ChangeFoodEffect(organismAffected, Organism.MUTATE_FOOD_GATHER, this.DnaSequenceIndex));
            }
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new EvolveDnaElement(child, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private EvolveDnaElement(Organism me, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.DnaCode = oldDnaCode;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public EvolveDnaElement(Organism me, byte dnaSequenceIndex)
        {
            if (me == null)
                throw new ArgumentNullException("Self cannot be null when adding DNA sequence.");

            this.Me = me;
            this.DnaCode = Common.GetRandomIntegerSeed();
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        #endregion Constructor.
    }
}