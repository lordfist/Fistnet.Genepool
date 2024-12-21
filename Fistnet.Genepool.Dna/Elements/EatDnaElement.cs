using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class EatDnaElement : IDnaElement
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
            get { return DnaTypes.Eat; }
        }

        public Organism Me
        {
            get;
            private set;
        }

        public void ExecuteDna(Organism organismAffected)
        {
            if (organismAffected != null && !organismAffected.IsDead
                && organismAffected.FoodBalance >= Organism.FEED_FOOD_COST)
            {
                organismAffected.AddStackedEffect(new ChangeFoodEffect(organismAffected, Organism.FEED_FOOD_COST, this.DnaSequenceIndex));
                organismAffected.AddStackedEffect(new ChangeHealthEffect(organismAffected, Organism.FEED_HEAL, this.DnaSequenceIndex));
                organismAffected.AddStackedEffect(new ChangeAgeEffect(organismAffected, Organism.FEED_AGE_REDUCTION, this.DnaSequenceIndex));
            }
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new EatDnaElement(child, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private EatDnaElement(Organism me, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.DnaCode = oldDnaCode;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public EatDnaElement(Organism me, byte dnaSequenceIndex)
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