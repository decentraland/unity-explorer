using UnityEngine;

namespace DCL.PerformanceBudgeting
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public long TotalSizeInMB => SystemInfo.systemMemorySize;
    }
}
