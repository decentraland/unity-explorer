using UnityEngine;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public ulong TotalSizeInMB => (ulong)SystemInfo.systemMemorySize;
    }
}
