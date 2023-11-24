using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public ulong TotalSizeInMB => (ulong)SystemInfo.systemMemorySize;
    }
}
