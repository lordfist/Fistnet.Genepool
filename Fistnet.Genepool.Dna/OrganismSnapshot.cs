using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna
{
    public class OrganismSnapshot
    {
        public long DnaCode { get; protected set; }

        #region Age.

        public int SequenceAge { get; protected set; }

        public int Age { get; protected set; }

        #endregion Age.

        #region Health.

        public sbyte Health { get; protected set; }

        public bool IsDead
        {
            get
            {
                if (this.Health <= 0 || this.Health >= Organism.OVERWEIGHT_DEATH)
                    return true;
                else
                    return false;
            }
        }

        #endregion Health.

        #region Food.

        public sbyte FoodBalance { get; protected set; }

        public byte AvailableFood { get; protected set; }

        public byte TakenFood { get; protected set; }

        #endregion Food.

        #region Child.

        public bool HasChild { get; protected set; }

        #endregion Child.

        #region Public constructorn.

        public OrganismSnapshot()
        {
        }

        public OrganismSnapshot(Organism organism)
        {
            this.DnaCode = organism.DnaCode;
            this.SequenceAge = organism.SequenceAge;
            this.Age = organism.Age;
            this.AvailableFood = organism.AvailableFood;
            this.FoodBalance = organism.FoodBalance;
            this.HasChild = organism.HasChild;
            this.Health = organism.Health;
            this.TakenFood = organism.TakenFood;
        }

        #endregion Public constructorn.
    }
}