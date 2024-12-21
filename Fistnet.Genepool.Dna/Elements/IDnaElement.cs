using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna.Elements
{
    public interface IDnaElement
    {
        int DnaCode { get; }

        byte DnaSequenceIndex { get; }

        TargetTypes Target { get; }

        DnaTypes DnaType { get; }

        Organism Me { get; }

        void ExecuteDna(Organism organismAffected);

        IDnaElement CopyToChild(Organism child);
    }
}