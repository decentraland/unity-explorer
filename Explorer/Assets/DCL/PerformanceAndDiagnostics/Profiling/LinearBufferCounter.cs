namespace DCL.Profiling
{
    public class LinearBufferCounter
    {
        private readonly long[] values;
        private readonly int maxBufferSize;

        private int tailIndex;
        public long Tail => values[tailIndex];
        public long MinValue => CalculateMinValue();
        public long MaxValue => CalculateMaxValue();

        public LinearBufferCounter(int bufferSize)
        {
            values = new long[bufferSize];
            maxBufferSize = bufferSize;
        }

        public void AddDeltaTime(long valueInSeconds)
        {
            values[tailIndex] = valueInSeconds;
            tailIndex = CircularIncrement(tailIndex);
        }

        private long CalculateMinValue()
        {
            long minValue = values[0];
            for (int i = 1; i < maxBufferSize; i++)
            {
                if (values[i] < minValue)
                    minValue = values[i];
            }
            return minValue;
        }

        private long CalculateMaxValue()
        {
            long maxValue = values[0];
            for (int i = 1; i < maxBufferSize; i++)
            {
                if (values[i] > maxValue)
                    maxValue = values[i];
            }
            return maxValue;
        }

        private int CircularIncrement(int id) =>
            (id + 1) % maxBufferSize;
    }
}
