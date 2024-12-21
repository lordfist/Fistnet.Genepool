using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements
{
    public class MoveDnaElement : IDnaElement
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
            get { return DnaTypes.Move; }
        }

        public Organism Me
        {
            get;
            private set;
        }

        private int _executionNumber;

        public void ExecuteDna(Organism organismAffected)
        {
            if (!this.Me.IsDead)
            {
                this.Me.AddStackedEffect(new ChangePositionEffect(this.Me, this.Target, this.DnaSequenceIndex));
                this._executionNumber++;
                if (Math.Abs(this.DnaCode) % this._executionNumber == 10)
                {
                    this.Target = Common.GetRandomTarget();
                    this._executionNumber = 0;
                }
            }
        }

        public IDnaElement CopyToChild(Organism child)
        {
            return new MoveDnaElement(child, this.Target, this.DnaCode, this.DnaSequenceIndex);
        }

        #endregion DNA setup

        #region Constructor.

        private MoveDnaElement(Organism me, TargetTypes target, int oldDnaCode, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Target = target;
            this.DnaCode = oldDnaCode;
            this._executionNumber = 0;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        public MoveDnaElement(Organism me, byte dnaSequenceIndex)
        {
            if (me == null)
                throw new ArgumentNullException("Self cannot be null when adding DNA sequence.");

            this.Me = me;
            this.DnaCode = Common.GetRandomIntegerSeed();
            this.Target = Common.GetRandomTarget(this.DnaCode);
            this._executionNumber = 0;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        #endregion Constructor.
    }
}