using ECS.Profiling;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public enum MemoryEstimation
    {
        Gltf = 1,
        Texture = 2,
        AssetBundle = 5,
    }

    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly long budgetCapInBytes;
        private readonly IProfilingProvider profilingProvider;

        private readonly ISystemMemory systemMemory;

        public int RequestedMemoryCounter { get; private set; }

        public MemoryBudgetProvider(IProfilingProvider profilingProvider)
        {
            // systemMemory = new DesktopSystemMemory();

            budgetCapInBytes = 3000 * ProfilingProvider.BYTES_IN_MEGABYTE;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget<TAsset>(MemoryEstimation estimation)
        {
            RequestedMemoryCounter += (int)estimation;
            return profilingProvider.TotalUsedMemoryInBytes < budgetCapInBytes;
        }

        public bool TrySpendBudget() =>
            profilingProvider.TotalUsedMemoryInBytes < budgetCapInBytes;

        public void ReleaseBudget() { }

        public void FlushRequestedMemoryCounter() =>
            RequestedMemoryCounter = 0;
    }
}
