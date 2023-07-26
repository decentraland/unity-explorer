using ECS.Profiling;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class FrameTimeCapBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly IProfilingProvider profilingProvider;

        private readonly float totalBudgetAvailable;

        public FrameTimeCapBudgetProvider(float budgetCap, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            totalBudgetAvailable = budgetCap * 1000000;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget() =>
            profilingProvider.GetCurrentFrameTimeValueInNS() < totalBudgetAvailable;

        public void ReleaseBudget() { }
    }
}
