using System;
using System.Collections.Generic;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Presentation
{
    // A dialog draft: editing and loading presets never touches the running world or its RNG.
    public sealed class SimulationSettings
    {
        public static IReadOnlyList<string> PresetNames { get; } = Array.AsReadOnly(new[]
        {
            "Current settings", "Random defaults", "Exploratory learning only", "Capped health only",
            "Reserve-transfer predation only", "Gather + reproduce repertoire", "Food regrowth 2"
        });
        private readonly SimulationRunOptions original;
        private SimulationPolicy policy;
        public int Density { get; set; }
        public int Seed { get; set; }
        public bool RandomSeed { get; set; } = true;
        public int CellFood { get; set; }
        public int Regrowth { get; set; }
        public int Reserves { get; set; }
        public FounderRepertoire FounderDna { get; set; }
        public LearningPolicy Learning { get; set; }
        public HealthPolicy Health { get; set; }
        public AttackPolicy Attack { get; set; }
        public SimulationPolicy LoadedPolicy => policy;

        public SimulationSettings(SimulationRunOptions current)
        {
            original = current ?? new SimulationRunOptions();
            ApplyPreset(0);
        }

        public void ApplyPreset(int index)
        {
            if (index < 0 || index >= PresetNames.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var options = index == 0 ? original : new SimulationRunOptions();
            policy = options.Policy;
            Density = options.InitialPopulationPercent; Seed = options.Seed;
            CellFood = options.InitialCellFood; Regrowth = options.FoodRegrowthPerAge;
            Reserves = options.InitialOrganismFood; FounderDna = options.FounderRepertoire;
            Learning = policy.Learning; Health = policy.Health; Attack = policy.Attack;
            RandomSeed = true;
            if (index == 2) Learning = LearningPolicy.BoundedExploratory;
            if (index == 3) Health = HealthPolicy.Capped;
            if (index == 4) Attack = AttackPolicy.ReserveTransfer;
            if (index == 5) FounderDna = FounderRepertoire.GatherAndReproduce;
            if (index == 6) Regrowth = 2;
        }

        // The caller generates a seed only when accepting a random-seed dialog.
        public SimulationRunOptions CreateOptions(int generatedSeed)
        {
            var options = new SimulationRunOptions
            {
                Mode = original.Mode, CellExecution = original.CellExecution,
                Seed = RandomSeed ? generatedSeed : Seed,
                InitialPopulationPercent = Density, InitialCellFood = CellFood,
                FoodRegrowthPerAge = Regrowth, InitialOrganismFood = Reserves,
                FounderRepertoire = FounderDna,
                Policy = new SimulationPolicy
                {
                    Learning = Learning, Health = Health, Attack = Attack,
                    ExternalContextLimit = policy.ExternalContextLimit, ExplorationPercent = policy.ExplorationPercent,
                    MaximumHealth = policy.MaximumHealth, AttackCost = policy.AttackCost, AttackFoodLimit = policy.AttackFoodLimit
                }
            };
            options.Validate();
            return options;
        }
    }
}
