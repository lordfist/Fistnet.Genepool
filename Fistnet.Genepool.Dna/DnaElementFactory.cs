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
            List<IDnaElement> dnaSequence = new List<IDnaElement>();

            for (byte i = 0; i < sequenceLength; i++)
            {
                IDnaElement chosenElement = DnaElementFactory.GetRandomDnaElement(owner, i);

                if (dnaSequence.CountOfType(chosenElement.DnaType) > Organism.DNA_SEQUENCE_MAXSINGLETYPE)
                    chosenElement = DnaElementFactory.GetRandomDnaElement(owner, i);

                dnaSequence.Add(chosenElement);
            }

            return dnaSequence;
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