using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fistnet.Genepool.Dna
{
    public class OrganismSnapshot
    {
        public long DnaCode { get; protected set; }
        public long Id { get; protected set; }
        public long Parent1Id { get; protected set; }
        public long Parent2Id { get; protected set; }
        public long LifetimeSeasons { get; protected set; }
        public int Generation { get; protected set; }
        public HealthPolicy HealthRule { get; protected set; } = Common.Policy.Health;

        #region Age.

        public int SequenceAge { get; protected set; }

        public int Age { get; protected set; }

        #endregion Age.

        #region Health.

        public int Health { get; protected set; }

        public bool IsDead
        {
            get
            {
                if (this.Health <= 0 || (this.HealthRule == HealthPolicy.LegacyOverweight && this.Health >= Organism.OVERWEIGHT_DEATH))
                    return true;
                else
                    return false;
            }
        }

        #endregion Health.

        #region Food.

        public int FoodBalance { get; protected set; }

        public int AvailableFood { get; protected set; }

        public int TakenFood { get; protected set; }

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
            this.Id = organism.Id;
            this.Parent1Id = organism.Parent1Id;
            this.Parent2Id = organism.Parent2Id;
            this.LifetimeSeasons = organism.LifetimeSeasons;
            this.Generation = organism.Generation;
            this.HealthRule = organism.HealthRule;
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
