using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FistCore.Common;

namespace Fistnet.Genepool.Dna.Effects
{
    public class BirthEffect : IDnaEffect
    {
        #region DNA effects.

        public EffectTypes Effect
        {
            get { return EffectTypes.Birth; }
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

        public BirthEffect(Organism me, Organism parent, byte dnaSequenceIndex)
        {
            this.Me = me;
            this.Value = parent;
            this.DnaSequenceIndex = dnaSequenceIndex;
        }

        #endregion Constructor.
    }
}