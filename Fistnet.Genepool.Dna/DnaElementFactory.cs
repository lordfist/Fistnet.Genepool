using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Dna
{
    public static class DnaElementFactory
    {
        public static IDnaElement ChooseRandomDnaElement(Organism owner, out  byte elementIndex)
        {
            int seed = Common.GetRandomIntegerSeed();

            byte dnaTypeSelected = (byte)unchecked((byte)seed % Enum.GetValues(typeof(DnaTypes)).Length);
            elementIndex = dnaTypeSelected;

            return owner.DnaSequence[dnaTypeSelected];
        }

        public static IDnaElement GetRandomDnaElement(Organism owner, byte sequenceIndex)
        {
            int seed = Common.GetRandomIntegerSeed();

            DnaTypes dnaTypeSelected = (DnaTypes)(unchecked((byte)seed % Enum.GetValues(typeof(DnaTypes)).Length));

            return CreateDnaElement(owner, sequenceIndex, dnaTypeSelected);
        }

        private static IDnaElement CreateDnaElement(Organism owner, byte sequenceIndex, DnaTypes dnaTypeSelected)
        {
            IDnaElement dnaElement;

            switch (dnaTypeSelected)
            {
                case DnaTypes.Eat:
                    dnaElement = new EatDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.Move:
                    dnaElement = new MoveDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.GenerateFood:
                    dnaElement = new CreateFoodDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.Kill:
                    dnaElement = new KillDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.CombineDna:
                    dnaElement = new CombineDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.Evolve:
                    dnaElement = new EvolveDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.Heal:
                    dnaElement = new HealDnaElement(owner, sequenceIndex);
                    break;

                case DnaTypes.Infect:
                    dnaElement = new InfectDnaElement(owner, sequenceIndex);
                    break;

                default:
                    throw new NotSupportedException("This dna type is not supported in generation.");
            }

            return dnaElement;
        }

        public static List<IDnaElement> GetRandomDnaSequence(Organism owner, byte sequenceLength)
        {
            DnaTypes[] types = Enum.GetValues<DnaTypes>();
            if (sequenceLength > types.Length * Organism.DNA_SEQUENCE_MAXSINGLETYPE)
                throw new ArgumentOutOfRangeException(nameof(sequenceLength), "Sequence exceeds the per-type capacity.");
            List<IDnaElement> dnaSequence = new List<IDnaElement>();

            for (byte i = 0; i < sequenceLength; i++)
            {
                DnaTypes[] allowed = types.Where(type => dnaSequence.CountOfType(type) < Organism.DNA_SEQUENCE_MAXSINGLETYPE).ToArray();
                DnaTypes selected = allowed[Common.GetRandomIntegerSeed(allowed.Length)];
                dnaSequence.Add(CreateDnaElement(owner, i, selected));
            }

            return dnaSequence;
        }

        public static List<IDnaElement> GetFounderDnaSequence(Organism owner, FounderRepertoire repertoire)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (repertoire == FounderRepertoire.UnrestrictedRandom)
                return GetRandomDnaSequence(owner, Organism.DNA_SEQUENCE_MAXLENGTH);
            if (repertoire != FounderRepertoire.GatherAndReproduce)
                throw new ArgumentOutOfRangeException(nameof(repertoire));

            // Reserve both types before filling random slots, so a mandatory slot
            // cannot exceed the existing per-type cap even when it comes last.
            int gatherSlot = Common.GetRandomIntegerSeed(Organism.DNA_SEQUENCE_MAXLENGTH);
            int reproductionSlot = Common.GetRandomIntegerSeed(Organism.DNA_SEQUENCE_MAXLENGTH - 1);
            if (reproductionSlot >= gatherSlot) reproductionSlot++;
            DnaTypes[] types = Enum.GetValues<DnaTypes>();
            var counts = types.ToDictionary(type => type, _ => 0);
            counts[DnaTypes.GenerateFood] = counts[DnaTypes.CombineDna] = 1;
            var sequence = new List<IDnaElement>(Organism.DNA_SEQUENCE_MAXLENGTH);
            for (byte slot = 0; slot < Organism.DNA_SEQUENCE_MAXLENGTH; slot++)
            {
                DnaTypes selected;
                if (slot == gatherSlot) selected = DnaTypes.GenerateFood;
                else if (slot == reproductionSlot) selected = DnaTypes.CombineDna;
                else
                {
                    DnaTypes[] allowed = types.Where(type => counts[type] < Organism.DNA_SEQUENCE_MAXSINGLETYPE).ToArray();
                    selected = allowed[Common.GetRandomIntegerSeed(allowed.Length)];
                    counts[selected]++;
                }
                // Existing gene constructors retain random directions. The preset
                // guarantees these action types, not a usable target or an outcome.
                sequence.Add(CreateDnaElement(owner, slot, selected));
            }
            return sequence;
        }

        public static List<IDnaElement> GetDnaSequenceFromParents(Organism owner, Organism parent1, Organism parent2, byte sequenceLength)
        {
            List<IDnaElement> dnaSequence = new List<IDnaElement>();
            int sequenceChoice = Common.GetRandomIntegerSeed();
            if (sequenceChoice % 2 == 0)
            {
                for (byte i = 0; i < sequenceLength; i++)
                {
                    if (Common.GetRandomIntegerSeed(1000) == 500)
                        dnaSequence.Add(DnaElementFactory.GetRandomDnaElement(owner, i));   // birth defect 1:1000
                    else
                    {
                        if (i % 2 == 0)
                            dnaSequence.Add(parent1.DnaSequence[i].CopyToChild(owner));
                        else
                            dnaSequence.Add(parent2.DnaSequence[i].CopyToChild(owner));
                    }
                }
            }
            else
            {
                for (byte i = 0; i < sequenceLength; i++)
                {
                    if (Common.GetRandomIntegerSeed(1000) == 500)
                        dnaSequence.Add(DnaElementFactory.GetRandomDnaElement(owner, i));   // birth defect 1:1000
                    else
                    {
                        if (i < sequenceLength / 2)
                            dnaSequence.Add(parent1.DnaSequence[i].CopyToChild(owner));
                        else
                            dnaSequence.Add(parent2.DnaSequence[i].CopyToChild(owner));
                    }
                }
            }

            return dnaSequence;
        }

        public static int CountOfType(this List<IDnaElement> sequence, DnaTypes type)
        {
            return sequence.Count((element) => { return element.DnaType == type; });
        }
    }
}
