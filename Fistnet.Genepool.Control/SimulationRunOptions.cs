using System;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control
{
    public enum SimulationMode { Production, DeterministicReference }
    public enum CellExecutionMode { Automatic, Serial, BoundedParallel }

    public sealed class SimulationRunOptions
    {
        public SimulationMode Mode { get; init; } = SimulationMode.Production;
        public int Seed { get; init; } = Environment.TickCount;
        public int InitialPopulationPercent { get; init; } = 10;
        public int InitialCellFood { get; init; } = 3;
        public int FoodRegrowthPerAge { get; init; } = 1;
        public int InitialOrganismFood { get; init; } = 5;
        public FounderRepertoire FounderRepertoire { get; init; } = FounderRepertoire.UnrestrictedRandom;
        public CellExecutionMode CellExecution { get; init; } = CellExecutionMode.Automatic;
        public IRandomSource RandomSource { get; init; }
        public SimulationPolicy Policy { get; init; } = new SimulationPolicy();

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(SimulationMode), Mode) || !Enum.IsDefined(typeof(CellExecutionMode), CellExecution)
                || !Enum.IsDefined(typeof(FounderRepertoire), FounderRepertoire)
                || InitialPopulationPercent < 0 || InitialPopulationPercent > 100
                || InitialCellFood < 0 || InitialCellFood > 10 || FoodRegrowthPerAge < 0 || FoodRegrowthPerAge > 10
                || InitialOrganismFood < 0 || InitialOrganismFood > Organism.MAX_FOOD_CARRY)
                throw new ArgumentOutOfRangeException(nameof(SimulationRunOptions));
            if (Policy == null) throw new ArgumentNullException(nameof(Policy));
            Policy.Validate();
        }
    }
}
