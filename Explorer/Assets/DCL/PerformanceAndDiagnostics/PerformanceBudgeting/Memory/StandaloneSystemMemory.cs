using UnityEngine;

namespace DCL.PerformanceBudgeting
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public ulong TotalSizeInMB => (ulong)SystemInfo.systemMemorySize;
    }
}
