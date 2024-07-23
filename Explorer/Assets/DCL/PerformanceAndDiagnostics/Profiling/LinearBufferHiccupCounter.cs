namespace DCL.Profiling
{
    public class LinearBufferHiccupCounter
    {
        private readonly LinearBufferCounter counter;
        private readonly int hiccupThresholdInNS;

        public ulong HiccupsCountInBuffer { get; private set; }
        public long MinFrameTimeInNS => counter.MinValue;
        public long MaxFrameTimeInNS => counter.MaxValue;
        public int BufferSize { get; private set; }

        public LinearBufferHiccupCounter(int bufferSize, int hiccupThresholdInNs)
        {
            BufferSize = bufferSize;

            counter = new LinearBufferCounter(bufferSize);
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
    }
}
