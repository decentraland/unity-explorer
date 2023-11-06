using UnityEngine;

namespace DCL.PerformanceBudgeting.Memory
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public long GetTotalSizeInMB() =>
            SystemInfo.systemMemorySize;
    }
}
