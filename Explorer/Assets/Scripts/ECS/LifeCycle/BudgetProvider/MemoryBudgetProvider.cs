using ECS.Profiling;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly Dictionary<Type, int> memoryEstimationMap;

        private readonly long budgetCapInBytes;
        private readonly IProfilingProvider profilingProvider;

        private readonly ISystemMemory systemMemory;

        public int RequestedMemoryCounter { get; private set; }

        public MemoryBudgetProvider(Dictionary<Type, int> memoryEstimationMap, IProfilingProvider profilingProvider)
        {
            // systemMemory = new DesktopSystemMemory();
            budgetCapInBytes = 3000 * ProfilingProvider.BYTES_IN_MEGABYTE;

            this.memoryEstimationMap = memoryEstimationMap;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget<TAsset>()
        {
            RequestedMemoryCounter += memoryEstimationMap[typeof(TAsset)];
            return TrySpendBudget();
        }

        public bool TrySpendBudget() =>
            true;

        // profilingProvider.TotalUsedMemoryInBytes < budgetCapInBytes;

        public void ReleaseBudget() { }

        public void FlushRequestedMemoryCounter() =>
            RequestedMemoryCounter = 0;
    }
}
