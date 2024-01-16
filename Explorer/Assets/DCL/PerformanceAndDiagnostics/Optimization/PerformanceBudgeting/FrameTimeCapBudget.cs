using DCL.Profiling;
using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly ulong totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;

        public FrameTimeCapBudget(float budgetCapInMS, IProfilingProvider profilingProvider) : this(
            TimeSpan.FromMilliseconds(budgetCapInMS),
            profilingProvider
        ) { }

        public FrameTimeCapBudget(TimeSpan totalBudgetAvailable, IProfilingProvider profilingProvider) : this(
            Convert.ToUInt64(
                totalBudgetAvailable.TotalMilliseconds * 1000000 //converting milliseconds to nanoseconds
            ),
            profilingProvider
        ) { }

        public FrameTimeCapBudget(ulong totalBudgetAvailable, IProfilingProvider profilingProvider)
        {
            this.profilingProvider = profilingProvider;
            this.totalBudgetAvailable = totalBudgetAvailable;
        }

        public bool TrySpendBudget() =>
            profilingProvider.CurrentFrameTimeValueInNS < totalBudgetAvailable;
    }
}
