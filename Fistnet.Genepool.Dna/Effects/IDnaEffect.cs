using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna.Effects
{
    public interface IDnaEffect
    {
        EffectTypes Effect { get; }

        object Value { get; }

        byte DnaSequenceIndex { get; }

        Organism Me { get; }
    }
}