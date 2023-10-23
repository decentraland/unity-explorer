using ECS.Profiling;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly long budgetCapInBytes;
        private readonly IProfilingProvider profilingProvider;

        public MemoryBudgetProvider(ISystemMemory systemMemory, IProfilingProvider profilingProvider)
        {
            budgetCapInBytes = 3000 * ProfilingProvider.BYTES_IN_MEGABYTE;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget() =>
            profilingProvider.TotalUsedMemoryInBytes < budgetCapInBytes;

        public void ReleaseBudget() { }
    }
}
