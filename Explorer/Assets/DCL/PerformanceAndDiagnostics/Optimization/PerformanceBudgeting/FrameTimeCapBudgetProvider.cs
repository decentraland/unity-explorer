using DCL.Profiling;
using UnityEngine;

namespace DCL.PerformanceBudgeting
{
    public class FrameTimeCapBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly float totalBudgetAvailable;

        // Debug
        public bool SimulateCappedFrameTime { private get; set; }

        public FrameTimeCapBudgetProvider(float budgetCapInMS, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            totalBudgetAvailable = budgetCapInMS * 1000000;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget()
        {
            if (Debug.isDebugBuild && SimulateCappedFrameTime)
                return false;

            return profilingProvider.CurrentFrameTimeValueInNS < totalBudgetAvailable;
        }

        public void ReleaseBudget() { }
    }
}
