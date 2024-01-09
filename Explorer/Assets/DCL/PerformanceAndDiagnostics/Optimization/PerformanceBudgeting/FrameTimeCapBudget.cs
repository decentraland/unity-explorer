using DCL.Profiling;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly float totalBudgetAvailable;

        public FrameTimeCapBudget(float budgetCapInMS, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            totalBudgetAvailable = budgetCapInMS * 1000000;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget() =>
            profilingProvider.CurrentFrameTimeValueInNS < totalBudgetAvailable;
    }
}
