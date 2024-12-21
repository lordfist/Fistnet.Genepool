using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class HealDnaElement : IDnaElement
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
            get { return DnaTypes.Heal; }
        }

        public Organism Me
        {
            get;
            private set;
        }

        public void ExecuteDna(Organism organismAffected)
        {
            if (organismAffected != null
                && !organismAffected.IsDead
                && !this.Me.IsDead
                && this.Me.DnaCode == organismAffected.DnaCode
                && this.Me.FoodBalance >= Math.Abs(Organism.HEAL_POWER_COST)
                && organismAffected.Health < (Organism.OVERWEIGHT_DEATH - Organism.HEAL_POWER))
            {
                organismAffected.AddStackedEffect(new ChangeHealthEffect(organismAffected, Organism.HEAL_POWER, this.DnaSequenceIndex));
                this.Me.AddStackedEffect(new ChangeFoodEffect(this.Me, Organism.HEAL_POWER_COST, this.DnaSequenceIndex));
                organismAffected.AddStackedEffect(new ChangeAgeEffect(organismAffected, Organism.HEAL_AGE_REDUCTION, this.DnaSequenceIndex));
            }
            else if (organismAffected != null
                && !organismAffected.IsDead
                && !this.Me.IsDead
                && this.Me.DnaCode != organismAffected.DnaCode)
            {
                this.Target = Common.TryChangeTarget(this.Target);
            }
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new HealDnaElement(child, this.Target, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private HealDnaElement(Organism me, TargetTypes target, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Target = target;
            this.DnaCode = oldDnaCode;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public HealDnaElement(Organism me, byte dnaSequenceIndex)
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