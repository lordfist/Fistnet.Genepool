using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Dna.Elements.Brain;

namespace Fistnet.Genepool.Dna
{
    public class Organism : OrganismSnapshot
    {
        #region Constants.

        public const sbyte INITAL_FOOD_BALANCE = 5;
        public const sbyte INITAL_HEALTH = 10;
        public const sbyte OVERWEIGHT_DEATH = INITAL_HEALTH * 5;

        public const byte DNA_SEQUENCE_MAXLENGTH = 8;
        public const byte DNA_SEQUENCE_MAXMUTATE = DNA_SEQUENCE_MAXLENGTH / 4;

        public const byte DNA_SEQUENCE_MAXSINGLETYPE = DNA_SEQUENCE_MAXLENGTH / 2;

        public const byte MAX_AGE = 255;

        public const sbyte KILL_POWER = -1 * (INITAL_HEALTH + 1);
        public const sbyte KILL_OK_FOOD_EXTRA = 5;

        public const sbyte HEAL_POWER = INITAL_HEALTH;
        public const sbyte HEAL_AGE_REDUCTION = -5;
        public const sbyte HEAL_POWER_COST = -1;

        public const sbyte FEED_AGE_REDUCTION = -1;
        public const sbyte FEED_HEAL = 1;
        public const sbyte FEED_FOOD_COST = -1;

        public const sbyte MUTATE_HEAL_POWER = 2;
        public const sbyte MUTATE_FOOD_GATHER = 1;
        public const sbyte MUTATE_AGE_REDUCTION = -2;

        public const sbyte FOOD_GATHER = 3;
        public const sbyte MAX_FOOD_CARRY = 10;

        public const sbyte FOOD_REDUCTION_PER_AGE = -1;
        public const sbyte FOOD_REDUCTION_PER_BIRTH = -2;
        public const byte MAX_REPRODUCTIONS_PER_AGE = 2;

        public const byte INFECTION_CHANCE = 5; // chance 50%
        public const byte INFECTION_CHANCE_SCALE = 10;
        public const sbyte INFECT_POWER_COST = -1;

        public const byte TARGET_CHANGE_CHANCE = 4; // chance 40%
        public const byte TARGET_CHANGE_SCALE = 10;

        #endregion Constants.

        #region Dna sequence.

        public List<IDnaElement> DnaSequence { get; protected set; }

        #endregion Dna sequence.

        #region Activate/deactivate.

        private bool _isActive;

        public void Activate()
        {
            this._isActive = true;
        }

        public void Deactivate()
        {
            this._isActive = false;
        }

        #endregion Activate/deactivate.

        #region Position.

        public TargetTypes? NextRequestedPosition { get; private set; }

        public void MoveDone()
        {
            this.NextRequestedPosition = null;
        }

        #endregion Position.

        #region Food creation and consumpsion.

        public byte GetAndResetTakenFood()
        {
            byte taken = this.TakenFood;
            this.TakenFood = 0;
            return taken;
        }

        public void SetAvailableFood(byte food)
        {
            this.AvailableFood = food;
        }

        #endregion Food creation and consumpsion.

        #region Child.

        private Organism _child;

        public Organism Child
        {
            get { return this._child; }
            private set
            {
                this._child = value;
                this.HasChild = (value != null);
            }
        }

        #endregion Child.

        #region Effects stack.

        public ConcurrentBag<IDnaEffect> EffectsStack { get; private set; }

        public void AddStackedEffect(IDnaEffect effect)
        {
            if (this.EffectsStack == null)
                this.EffectsStack = new ConcurrentBag<IDnaEffect>();

            this.EffectsStack.Add(effect);
        }

        public void ExecuteEffectStack()
        {
            foreach (IDnaEffect item in this.EffectsStack)
            {
                this.ExecuteSingleEffect(item);
            }
            this.Starved();
            this.Brain.EvaluateResult(this.chosenSequenceIndex, this.chosenTarget);
            this.EffectsStack = new ConcurrentBag<IDnaEffect>();
        }

        private void Starved()
        {
            if (this.FoodBalance < 0)  // STARVING
            {
                this.Health += this.FoodBalance;
                this.FoodBalance = 0;
            }
        }

        private void ExecuteSingleEffect(IDnaEffect effect)
        {
            switch (effect.Effect)
            {
                case EffectTypes.HealthChange:
                    this.Health += (sbyte)effect.Value;
                    break;

                case EffectTypes.FoodChange:
                    sbyte foodChange = (sbyte)effect.Value;
                    if (foodChange > 0)
                    {
                        if (this.AvailableFood < (byte)foodChange)
                            foodChange = (sbyte)this.AvailableFood;

                        this.TakenFood = (byte)foodChange;
                        this.AvailableFood -= (byte)foodChange;
                    }

                    this.FoodBalance += foodChange;
                    break;

                case EffectTypes.Movement:
                    this.NextRequestedPosition = (TargetTypes)effect.Value;
                    break;

                case EffectTypes.Mutate:
                    this.MutateMe((int)effect.Value, effect.DnaSequenceIndex);

                    break;

                case EffectTypes.Birth:
                    if (this.Child == null)
                        this.BirthChild((Organism)effect.Value);
                    this._reproductionsInAge++;

                    break;

                case EffectTypes.AgeChange:
                    this.Age += (sbyte)effect.Value;
                    if (this.Age < 0)
                        this.Age = 0;

                    break;

                default:
                    throw new NotSupportedException("This effect type is not supported");
            }
        }

        #region Birth.

        private byte _reproductionsInAge;

        private void BirthChild(Organism otherParent)
        {
            if (this._reproductionsInAge < Organism.MAX_REPRODUCTIONS_PER_AGE && this.FoodBalance > 0)
                this.Child = new Organism(this, otherParent);
            this._reproductionsInAge++;
        }

        public void ReleaseChild()
        {
            if (this.Child != null)
            {
                this.AddStackedEffect(new ChangeFoodEffect(this, Organism.FOOD_REDUCTION_PER_BIRTH, Organism.DNA_SEQUENCE_MAXLENGTH)); // reduce food, birth was made
                this.Child = null;
            }
        }

        #endregion Birth.

        #region Mutation.

        private void MutateMe(int seed, byte sequenceIndexThatMutates)
        {
            Random random = new Random(seed);
            byte[] randomList = new byte[Organism.DNA_SEQUENCE_MAXLENGTH];

            for (int i = 0; i < Organism.DNA_SEQUENCE_MAXLENGTH; i++)
            {
                randomList[i] = unchecked((byte)random.Next(Organism.DNA_SEQUENCE_MAXLENGTH));
            }

            byte remainingChange = Organism.DNA_SEQUENCE_MAXMUTATE;

            int index = 0;
            while (remainingChange >= 0 && index < randomList.Length)
            {
                if (randomList[index] != sequenceIndexThatMutates)
                {
                    this.DnaSequence[randomList[index]] = DnaElementFactory.GetRandomDnaElement(this, randomList[index]);
                    remainingChange--;
                }

                index++;
            }

            this.DnaCode = Common.CalculateOrganismDnaCode(this.DnaSequence);
        }

        #endregion Mutation.

        #endregion Effects stack.

        #region Execute DNA sequence element.

        public StrategyNetwork Brain { get; private set; }

        public TargetTypes GetNextDnaSequenceTarget()
        {
            byte chosenIndex = 0;
            return this.Brain.ChooseOutput(null, out chosenIndex).Target;
        }

        private byte currentSequenceIndex;
        private byte chosenSequenceIndex;
        private Organism chosenTarget;

        public void ExecuteNextDnaSequence(Organism organismAffected)
        {
            if (!this._isActive)
                return;

            if (this.Age >= Organism.MAX_AGE)
                this.Health = 0;

            if (!this.IsDead && currentSequenceIndex < Organism.DNA_SEQUENCE_MAXLENGTH)
            {
                this.Brain.ChooseOutput(organismAffected, out chosenSequenceIndex).ExecuteDna(organismAffected);
                this.chosenTarget = organismAffected;

                currentSequenceIndex++;
            }
            else if (!this.IsDead && currentSequenceIndex >= Organism.DNA_SEQUENCE_MAXLENGTH)
            {
                this.AddStackedEffect(new ChangeFoodEffect(this, Organism.FOOD_REDUCTION_PER_AGE, Organism.DNA_SEQUENCE_MAXLENGTH)); // reduce health, generation is getting older
                this.Age++; // increase age
                this.SequenceAge++;
                this._reproductionsInAge = 0;
                currentSequenceIndex = 0;  // reboot dna sequence
            }
        }

        #endregion Execute DNA sequence element.

        #region Constructor.

        public Organism()
        {
            this.Health = Organism.INITAL_HEALTH;
            this.DnaSequence = DnaElementFactory.GetRandomDnaSequence(this, Organism.DNA_SEQUENCE_MAXLENGTH);
            this.NextRequestedPosition = null;
            this.FoodBalance = Organism.INITAL_FOOD_BALANCE;
            this.Age = 0;
            this.TakenFood = 0;
            this.EffectsStack = new ConcurrentBag<IDnaEffect>();
            this.DnaCode = Common.CalculateOrganismDnaCode(this.DnaSequence);
            this._reproductionsInAge = 0;
            this.SequenceAge = 0;

            this.Brain = new StrategyNetwork(this);
        }

        public Organism(Organism parent1, Organism parent2)
        {
            this.Health = Organism.INITAL_HEALTH;
            this.DnaSequence = DnaElementFactory.GetDnaSequenceFromParents(this, parent1, parent2, Organism.DNA_SEQUENCE_MAXLENGTH);
            this.NextRequestedPosition = null;
            this.FoodBalance = Math.Abs(Organism.FOOD_REDUCTION_PER_BIRTH);
            this.Age = 0;
            this.EffectsStack = new ConcurrentBag<IDnaEffect>();
            this.DnaCode = Common.CalculateOrganismDnaCode(this.DnaSequence);
            this._reproductionsInAge = 0;

            if (this.DnaCode == parent1.DnaCode)
                this.SequenceAge = parent1.SequenceAge;
            else
                this.SequenceAge = 0;

            this.Brain = new StrategyNetwork(this);
            this.Brain.LearnFromParent(parent1);
            this.Brain.LearnFromParent(parent2);
        }

        public Organism(List<IDnaElement> dnaSequence)
        {
            this.Health = Organism.INITAL_HEALTH;
            this.DnaSequence = dnaSequence;
            this.NextRequestedPosition = null;
            this.FoodBalance = Organism.INITAL_FOOD_BALANCE;
            this.Age = 0;
            this.EffectsStack = new ConcurrentBag<IDnaEffect>();
            this.DnaCode = Common.CalculateOrganismDnaCode(this.DnaSequence);
            this._reproductionsInAge = 0;

            this.Brain = new StrategyNetwork(this);
        }

        #endregion Constructor.
    }
}