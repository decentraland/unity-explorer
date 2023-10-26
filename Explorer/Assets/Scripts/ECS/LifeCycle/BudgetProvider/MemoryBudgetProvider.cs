using ECS.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
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
        private readonly IProfilingProvider profilingProvider;

        private readonly ISystemMemory systemMemory;

        public MemoryBudgetProvider(IProfilingProvider profilingProvider)
        {
            systemMemory = new SystemMemoryMock(5000);
            Debug.Log($"{systemMemory.TotalSizeInMB * 0.8} {systemMemory.TotalSizeInMB * 0.9}  {systemMemory.TotalSizeInMB * 0.95}");

            this.profilingProvider = profilingProvider;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE;

            return usedMemory switch
                   {
                       _ when usedMemory > systemMemory.TotalSizeInMB * 0.95 => MemoryUsageStatus.Full,
                       _ when usedMemory > systemMemory.TotalSizeInMB * 0.9 => MemoryUsageStatus.Critical,
                       _ when usedMemory > systemMemory.TotalSizeInMB * 0.8 => MemoryUsageStatus.Warning,
                       _ => MemoryUsageStatus.Normal,
                   };
        }

        public bool TrySpendBudget<TAsset>()
        {
            // RequestedMemoryCounter += memoryEstimationMap[typeof(TAsset)];
            return TrySpendBudget();
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != MemoryUsageStatus.Full;

        public void ReleaseBudget() { }
    }
}
