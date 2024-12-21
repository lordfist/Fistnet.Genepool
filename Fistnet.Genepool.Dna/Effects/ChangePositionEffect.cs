using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna.Effects
{
    public class ChangePositionEffect : IDnaEffect
    {
        #region DNA effects.

        public EffectTypes Effect
        {
            get { return EffectTypes.Movement; }
        }

        public object Value
        {
            get;
            private set;
        }

        public byte DnaSequenceIndex
        {
            get;
            private set;
        }

        public Organism Me
        {
            get;
            private set;
        }

        #endregion DNA effects.

        #region Constructor.

        public ChangePositionEffect(Organism me, TargetTypes value, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Value = value;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        #endregion Constructor.
    }
}