using System;

namespace Fistnet.Genepool.Visualization
{
    public readonly struct PopulationSample
    {
        public int Season { get; }
        public int Population { get; }

        public PopulationSample(int season, int population)
        {
            Season = season;
            Population = population;
        }
    }

    /// <summary>Observations supplied after completed seasons; never advances the simulation.</summary>
    public sealed class PopulationHistory
    {
        public const int Capacity = 256;

        private readonly object sync = new object();
        private readonly PopulationSample[] samples = new PopulationSample[Capacity];
        private int start;
        private int count;

        public int Count
        {
            get { lock (sync) { return count; } }
        }

        /// <summary>Repeated observations of a season replace its sample; reset before a new run.</summary>
        public void Add(int season, int population)
        {
            if (season < 0)
                throw new ArgumentOutOfRangeException(nameof(season));
            if (population < 0)
                throw new ArgumentOutOfRangeException(nameof(population));

            lock (sync)
            {
                PopulationSample sample = new PopulationSample(season, population);
                if (count > 0)
                {
                    int latest = (start + count - 1) % Capacity;
                    if (season < samples[latest].Season)
                        throw new ArgumentException("Reset history before recording an earlier season.", nameof(season));
                    if (season == samples[latest].Season)
                    {
                        samples[latest] = sample;
                        return;
                    }
                }

                samples[(start + count) % Capacity] = sample;
                if (count == Capacity)
                    start = (start + 1) % Capacity;
                else
                    count++;
            }
        }

        public void Reset()
        {
            lock (sync)
            {
                Array.Clear(samples, 0, samples.Length);
                start = 0;
                count = 0;
            }
        }

        /// <summary>Returns independent values in oldest-to-newest order.</summary>
        public PopulationSample[] Snapshot()
        {
            lock (sync)
            {
                PopulationSample[] result = new PopulationSample[count];
                for (int i = 0; i < count; i++)
                    result[i] = samples[(start + i) % Capacity];
                return result;
            }
        }
    }
}
