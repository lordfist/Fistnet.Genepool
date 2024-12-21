using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class InfectDnaElement : IDnaElement
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
            get { return DnaTypes.Infect; }
        }

        public Organism Me
        {
            get;
            private set;
        }

        public void ExecuteDna(Organism organismAffected)
        {
            if (organismAffected != null && !organismAffected.IsDead
                && !this.Me.IsDead
                && Common.GetRandomIntegerSeed(Organism.INFECTION_CHANCE_SCALE) <= Organism.INFECTION_CHANCE)
            {
                organismAffected.AddStackedEffect(new MutateEffect(organismAffected, this.DnaCode, this.DnaSequenceIndex));
                this.Me.AddStackedEffect(new ChangeFoodEffect(this.Me, Organism.INFECT_POWER_COST, this.DnaSequenceIndex));
            }
            else if (organismAffected != null && !organismAffected.IsDead
                && !this.Me.IsDead && this.Me.DnaCode == organismAffected.DnaCode)
            {
                this.Target = Common.TryChangeTarget(this.Target);
            }
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new InfectDnaElement(child, this.Target, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private InfectDnaElement(Organism me, TargetTypes target, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Target = target;
            this.DnaCode = oldDnaCode;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public InfectDnaElement(Organism me, byte dnaSequenceIndex)
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