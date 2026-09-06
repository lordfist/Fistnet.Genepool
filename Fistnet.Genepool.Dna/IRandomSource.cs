using System;

namespace Fistnet.Genepool.Dna
{
    public interface IRandomSource
    {
        int Next(int exclusiveMaximum);
        long DrawCount { get; }
        string Algorithm { get; }
        string State { get; }
    }

    public sealed class SeededRandomSource : IRandomSource
    {
        private uint state;
        public long DrawCount { get; private set; }
        public string Algorithm => "xorshift32-v1";
        public string State => state.ToString("X8");
        public SeededRandomSource(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x9E3779B9; }
        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            DrawCount++;
            return (int)(state % (uint)exclusiveMaximum);
        }
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;
        private readonly object gate = new object();
        private long drawCount;
        public long DrawCount { get { lock (gate) return drawCount; } }
        public string Algorithm => "System.Random-production";
        public string State => "opaque; no production repeatability claim";
        public SystemRandomSource(int seed) { random = new Random(seed); }
        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            lock (gate) { drawCount++; return random.Next(exclusiveMaximum); }
        }
    }
}
