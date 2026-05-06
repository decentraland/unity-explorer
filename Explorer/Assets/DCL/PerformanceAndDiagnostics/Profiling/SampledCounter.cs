using System.Threading;

namespace DCL.Profiling
{
    /// <summary>
    ///     Thread-safe counter that maintains a running total alongside a fixed-capacity ring buffer
    ///     of recent samples. Writers call <see cref="Add" /> from any thread; readers compute
    ///     min/max/avg/hiccups via <see cref="ComputeStats" /> or copy the chronological tail with
    ///     <see cref="CopySnapshot" />.
    /// </summary>
    public sealed class SampledCounter
    {
        public const int BUFFER_CAPACITY = 256;

        private readonly long[] samples = new long[BUFFER_CAPACITY];
        private readonly object lockObject = new ();

        private int writeIndex;
        private int sampleCount;

        private long total;

        public long Total => Interlocked.Read(ref total);

        public void Add(long value)
        {
            Interlocked.Add(ref total, value);

            lock (lockObject)
            {
                samples[writeIndex] = value;
                writeIndex = (writeIndex + 1) % BUFFER_CAPACITY;
                if (sampleCount < BUFFER_CAPACITY) sampleCount++;
            }
        }

        /// <summary>
        ///     Computes aggregate stats across all live samples. <paramref name="hiccupThreshold" />
        ///     is the lower bound for a sample to be counted as a hiccup.
        /// </summary>
        public Stats ComputeStats(long hiccupThreshold)
        {
            lock (lockObject)
            {
                int count = sampleCount;
                if (count == 0) return default;

                long min = long.MaxValue;
                long max = long.MinValue;
                long sum = 0;
                var hiccups = 0;

                for (var i = 0; i < count; i++)
                {
                    long value = samples[i];
                    if (value < min) min = value;
                    if (value > max) max = value;
                    sum += value;
                    if (value > hiccupThreshold) hiccups++;
                }

                return new Stats(count, min, max, sum, hiccups);
            }
        }

        /// <summary>
        ///     Copies recent samples in chronological (oldest-first) order into <paramref name="dst" />.
        ///     Returns the number of samples written. Caller's buffer must be at least
        ///     <see cref="BUFFER_CAPACITY" />.
        /// </summary>
        public int CopySnapshot(long[] dst)
        {
            lock (lockObject)
            {
                int count = sampleCount;
                if (count == 0) return 0;

                if (count < BUFFER_CAPACITY)
                {
                    for (var i = 0; i < count; i++)
                        dst[i] = samples[i];
                }
                else
                {
                    int start = writeIndex;

                    for (var i = 0; i < BUFFER_CAPACITY; i++)
                        dst[i] = samples[(start + i) % BUFFER_CAPACITY];
                }

                return count;
            }
        }

        public readonly struct Stats
        {
            public readonly int Count;
            public readonly long Min;
            public readonly long Max;
            public readonly long Sum;
            public readonly int Hiccups;

            public Stats(int count, long min, long max, long sum, int hiccups)
            {
                Count = count;
                Min = min;
                Max = max;
                Sum = sum;
                Hiccups = hiccups;
            }

            public float Avg => Count == 0 ? 0f : (float)Sum / Count;
        }
    }
}
