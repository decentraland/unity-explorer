namespace DCL.Profiling
{
    /// <summary>
    ///     Calculated in Nanoseconds
    /// </summary>
    public class HiccupsCounter
    {
        private readonly ulong hiccupThresholdInNS;

        private bool isCleared;

        public ulong Amount { get; private set; }
        public ulong SumTime { get; private set; }

        public ulong Min { get; private set; }
        public ulong Max { get; private set; }

        public float Avg => Amount == 0 ? 0 : SumTime / (float)Amount;

        public HiccupsCounter(ulong hiccupThresholdMs)
        {
            hiccupThresholdInNS = hiccupThresholdMs * 1_000_000;
            Clear();
        }

        public void CheckForHiccup(ulong frameTime)
        {
            if (frameTime > hiccupThresholdInNS)
            {
                Amount++;
                SumTime += frameTime;

                if (isCleared)
                {
                    Min = frameTime;
                    Max = frameTime;

                    isCleared = false;
                    return;
                }

                if (frameTime > Max) Max = frameTime;
                else if (frameTime < Min) Min = frameTime;
            }
        }

        public void Clear()
        {
            Amount = 0;
            SumTime = 0;

            Min = 0;
            Max = 0;

            isCleared = true;
        }
    }
}
