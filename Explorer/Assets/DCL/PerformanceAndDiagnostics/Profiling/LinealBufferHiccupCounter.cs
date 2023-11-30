namespace DCL.PerformanceAndDiagnostics.Profiling
{
    public class LinealBufferHiccupCounter
    {
        private readonly LinealBufferFPSCounter counter;
        private readonly int hiccupThresholdInNS;

        public ulong HiccupsCountInBuffer { get; private set; }

        public LinealBufferHiccupCounter(int bufferSize, int hiccupThresholdInNs)
        {
            counter = new LinealBufferFPSCounter(bufferSize);
            hiccupThresholdInNS = hiccupThresholdInNs;
        }

        public void AddDeltaTime(long valueInNanoSeconds)
        {
            if (IsHiccup(counter.Tail))
                HiccupsCountInBuffer -= 1;

            if (IsHiccup(valueInNanoSeconds))
                HiccupsCountInBuffer += 1;

            counter.AddDeltaTime(valueInNanoSeconds);
        }

        private bool IsHiccup(long value) =>
            value > hiccupThresholdInNS;

        private class LinealBufferFPSCounter
        {
            private readonly long[] Values;
            private readonly int maxBufferSize;

            private int tailIndex;

            public long Tail => Values[tailIndex];

            public LinealBufferFPSCounter(int bufferSize)
            {
                Values = new long[bufferSize];
                maxBufferSize = bufferSize;
            }

            public void AddDeltaTime(long valueInSeconds)
            {
                Values[tailIndex] = valueInSeconds;
                tailIndex = CircularIncrement(tailIndex);
            }

            private int CircularIncrement(int id) =>
                (id + 1) % maxBufferSize;
        }
    }
}
