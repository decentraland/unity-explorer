using DCL.PerformanceBudgeting.Memory;
using DCL.Profiling;
using System.Collections.Generic;
using UnityEngine;
using static DCL.PerformanceBudgeting.MemoryUsageStatus;

namespace DCL.PerformanceBudgeting
{
    public enum MemoryUsageStatus
    {
        Normal,
        Warning,
        Critical,
        Full,
    }

    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly Dictionary<MemoryUsageStatus, float> p = new()
        {
            { Warning, 0.5f },
            { Critical, 0.8f },
            { Full, 0.95f },
        };

        private readonly IProfilingProvider profilingProvider;
        private readonly ISystemMemory systemMemory;

        public MemoryBudgetProvider(IProfilingProvider profilingProvider)
        {
            systemMemory = new SystemMemoryMock(profilingProvider, 64000); // new StandaloneSystemMemory();

            this.profilingProvider = profilingProvider;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE;

            return usedMemory switch
                   {
                       _ when usedMemory > systemMemory.GetTotalSizeInMB() * p[Full] => Full,
                       _ when usedMemory > systemMemory.GetTotalSizeInMB() * p[Critical] => Critical,
                       _ when usedMemory > systemMemory.GetTotalSizeInMB() * p[Warning] => Warning,
                       _ => Normal,
                   };
        }

        private void LogLimits() =>
            Debug.Log($"VV: {systemMemory.GetTotalSizeInMB() * p[Warning]} {systemMemory.GetTotalSizeInMB() * p[Critical]}  {systemMemory.GetTotalSizeInMB() * p[Full]}");

        public bool TrySpendBudget() =>

            // true;
            GetMemoryUsageStatus() != Full;

        public void ReleaseBudget() { }
    }
}
