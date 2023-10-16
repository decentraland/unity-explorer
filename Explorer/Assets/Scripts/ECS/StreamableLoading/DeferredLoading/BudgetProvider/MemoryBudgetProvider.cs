using ECS.Profiling;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly long budgetCapInBytes;
        private readonly IProfilingProvider profilingProvider;

        public MemoryBudgetProvider(float budgetCapInMB, IProfilingProvider profilingProvider)
        {
            budgetCapInBytes = (long)(budgetCapInMB * ProfilingProvider.BYTES_IN_MEGABYTE);
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget() =>
            profilingProvider.TotalUsedMemoryInBytes < budgetCapInBytes;

        public void ReleaseBudget() { }
    }
}
