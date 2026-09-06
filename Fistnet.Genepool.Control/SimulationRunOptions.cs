using System;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control
{
    public enum SimulationMode { Production, DeterministicReference }

    public sealed class SimulationRunOptions
    {
        public SimulationMode Mode { get; init; } = SimulationMode.Production;
        public int Seed { get; init; } = Environment.TickCount;
        public int InitialPopulationPercent { get; init; } = 10;
        public IRandomSource RandomSource { get; init; }
    }
}
