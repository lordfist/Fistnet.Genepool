using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna.Effects
{
    public class ChangeHealthEffect : IDnaEffect
    {
        #region DNA effects.

        public EffectTypes Effect
        {
            get { return EffectTypes.HealthChange; }
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

        public ChangeHealthEffect(Organism me, sbyte value, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Value = value;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        #endregion Constructor.
    }
}