using UnityEngine;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public int TotalSizeInMB => SystemInfo.systemMemorySize; 
    }
}
