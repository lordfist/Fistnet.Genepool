using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class CombineDnaElement : IDnaElement
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
            get { return DnaTypes.CombineDna; }
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
                && this.Me.Age >= 1 && this.Me.Age < Organism.MAX_AGE
                && organismAffected.Age >= 1
                && organismAffected.Age < Organism.MAX_AGE)
            {
                this.Me.AddStackedEffect(new BirthEffect(this.Me, organismAffected, this.DnaSequenceIndex));
            }
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new CombineDnaElement(child, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private CombineDnaElement(Organism me, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.DnaCode = oldDnaCode;
            this.Target = Common.GetRandomTarget(this.DnaCode);
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public CombineDnaElement(Organism me, byte dnaSequenceIndex)
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