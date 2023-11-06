using DCL.Profiling;

namespace DCL.PerformanceBudgeting
{
    public class FrameTimeCapBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly IProfilingProvider profilingProvider;

        private readonly float totalBudgetAvailable;

        public FrameTimeCapBudgetProvider(float budgetCapInMS, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            totalBudgetAvailable = budgetCapInMS * 1000000;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget() =>
            profilingProvider.GetCurrentFrameTimeValueInNS() < totalBudgetAvailable;

        public void ReleaseBudget() { }
    }
}
