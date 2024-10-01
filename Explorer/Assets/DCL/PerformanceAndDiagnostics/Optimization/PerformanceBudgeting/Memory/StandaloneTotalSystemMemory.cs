using UnityEngine;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class StandaloneTotalSystemMemoryCap : ISystemMemoryCap
    {
        public int MemoryCapInMB => SystemInfo.systemMemorySize;
    }
}
